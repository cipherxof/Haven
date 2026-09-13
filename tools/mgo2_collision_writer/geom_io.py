# -*- coding: utf-8 -*-
"""
geom_io.py -- round-trip codec for MGO2 (PS3) stage ``.geom`` files.

    from geom_io import GeomFile
    g = GeomFile.load('n002a.geom')
    assert g.to_bytes() == g.source          # byte-identical round trip
    g.instances[123].pos = (x, y, z, 1.0)    # edit
    open('out.geom','wb').write(g.to_bytes())

All data is BIG-ENDIAN (PS3).  Input must be the DECRYPTED plaintext; use
mgo2_stagecrypt.py / stage_export.py for the container crypto.

=============================================================================
DESIGN: SELF-VALIDATING SPAN MODEL
=============================================================================
The file is decomposed into an ordered list of spans that tile [0, filesize)
with no gaps and no overlaps.  Every leaf span keeps its original bytes in
``_raw``.  Serialising a span returns::

    self._raw            if it was never modified   (bit-exact, always)
    self.pack()          if a field was assigned    (re-synthesised)

Consequences, and this is the whole point of the design:

  * The round trip cannot fail.  An unmodified file re-emits ``_raw`` for
    every leaf, so the output is the input.  This holds even for regions
    whose grammar is wrong or completely undecoded -- those are simply
    ``Blob`` spans that copy bytes through.
  * A decode bug can never corrupt data silently.  At parse time each
    decoded record compares ``pack()`` against its own source bytes and
    records the result in ``.faithful``.  A record that does not reproduce
    its bytes is frozen: assigning to it raises.  So an edit is only ever
    allowed on a structure that has *proved* it can be rebuilt exactly.
  * Anything not understood stays verbatim.  Sections 0x010000 and 0x070000
    are deliberately opaque blobs (their grammars are not solid across all
    stages); they still support surgical typed pokes via Blob.poke_*.

=============================================================================
CONTAINER LAYOUT  (MEASURED on 14 decrypted shipped stages)
=============================================================================
  +0x00 u32   magic 0x0BF68BFE
  +0x04 u32   plaintext file size
  +0x08 u32   section-table entry count (5 on every shipped stage)
  +0x0c u32   0
  +0x10 3xf32 optional secondary vector (non-zero on n004a/n022a/n024a)
  +0x1c u32   0
  +0x20       SECTION TABLE: entry_count * (u32 tag, u32 size, u32 offset)
              tag 0x000000 = the bulk payload (collision database)
              tag 0x010000, 0x050000, 0x060000, 0x070000
              -> the 5 entries tile the file EXACTLY, 0x80..EOF, 14/14 files
  +0x5c..0x80 tail: u32 0x08000000, u32 0, u32 <per-file value>, then zeros

  NOTE on the table origin.  Reading the table at +0x20 makes entry_count(5)
  match the entry count, makes all five entries tile the file exactly, and
  explains +0x24/+0x28 as the payload's own (size, offset).  An alternative
  reading at +0x2c yields 4 entries plus a "sentinel"; both consume bytes
  through +0x68, but the +0x20 reading is the one where every word is
  accounted for, so it is what this module uses.  Round-trip fidelity does
  not depend on the choice -- the header is rebuilt field-for-field either way.

PAYLOAD (tag 0x000000, ~97% of the file) -- the static collision world:
  +0x00 f32[3] world MIN corner        +0x0c u32 trailer offset
  +0x10 f32[3] grid cell size          +0x1c f32 1.0
  +0x20 u32[3] grid dims (x,y,z)       +0x2c u32 desc-table end, RELATIVE to grid_off
  +0x30 u32 last-world flag (1=last)   +0x34 u16 8, u16 grid stride (10)
  +0x38 u32 grid array offset          +0x3c u32 descriptor table offset
  then: grid array (dims product * stride), descriptor table (32B records),
  data blocks in ascending offset order separated by alignment gaps, and a
  trailer of u32 hashes.  All offsets are absolute file offsets.
  MULTI-WORLD (measured 2026-08-28, 25/25 files): the payload may hold up to
  8 such worlds.  Sub-headers run back-to-back from the section base (the
  first grid starts right after the run; +0x30 == 1 flags the last one), and
  each world's grid/descriptors/blocks/trailer follow in sequence.  17/25
  shipped geoms are multi-world.  See PayloadSection.worlds.

SECTIONS
  0x010000  placed-object instance records w/ 4x4 transforms -- OPAQUE here
  0x050000  polyline / path network            -- decoded, verified 14/14
  0x060000  placed-object / entity table       -- decoded, verified 11/14
  0x070000  directory of 48-byte elements      -- OPAQUE here

Sections 0x010000 and 0x070000 are intentionally not decoded: 0x010000 is a
heterogeneous pointer-linked structure with no established end-of-array rule,
and 0x070000's directory grammar does not hold on n022a (its bytes are
instance-shaped) nor on the 7 stages where the section is a single empty
16-byte record.  Both round-trip perfectly as blobs.
"""

import hashlib
import struct
import sys

__all__ = ['GeomFile', 'MAGIC', 'GeomError']

MAGIC = 0x0BF68BFE
HEADER_SIZE = 0x80
PAYLOAD_TAG = 0x000000
TAG_PLACEMENT = 0x010000
TAG_POLYLINE = 0x050000
TAG_INSTANCE = 0x060000
TAG_DIRECTORY = 0x070000

# Instance payload-word component bits (section 0x060000).
INST_POS = 0x00000002
INST_ORIENT = 0x00001000
INST_SCALE = 0x00400000
INST_KNOWN_BITS = 0x00401802          # includes 0x800, which carries no block
INST_NO_PAYLOAD = (0x00000000, 0x80000000)

# Collision node classes and their byte sizes (0x2012 is variable).
NODE_FIXED_SIZE = {0x2004: 64, 0x2005: 144, 0x2023: 144, 0x2024: 80}
NODE_LEAF = 0x2012


class GeomError(Exception):
    pass


class FrozenRecord(GeomError):
    """Raised when editing a record that could not reproduce its own bytes."""


def _u16(b, o):
    return struct.unpack_from('>H', b, o)[0]


def _u32(b, o):
    return struct.unpack_from('>I', b, o)[0]


# ---------------------------------------------------------------------------
# span primitives
# ---------------------------------------------------------------------------
class Span(object):
    """A byte range of the file.  Subclasses must implement to_bytes()."""
    __slots__ = ('offset', 'size')

    def __init__(self, offset, size):
        self.offset = offset
        self.size = size

    def to_bytes(self):
        raise NotImplementedError


class Blob(Span):
    """Opaque bytes preserved verbatim.  Supports surgical typed pokes."""
    __slots__ = ('_buf', 'label', 'dirty')

    def __init__(self, offset, data, label=''):
        Span.__init__(self, offset, len(data))
        self._buf = bytearray(data)
        self.label = label
        self.dirty = False

    def to_bytes(self):
        return bytes(self._buf)

    # -- surgical access, for regions whose grammar is not decoded ----------
    def peek(self, rel, n):
        return bytes(self._buf[rel:rel + n])

    def poke(self, rel, data):
        if rel < 0 or rel + len(data) > self.size:
            raise GeomError('poke out of range')
        self._buf[rel:rel + len(data)] = data
        self.dirty = True

    def peek_f32(self, rel):
        return struct.unpack_from('>f', self._buf, rel)[0]

    def poke_f32(self, rel, v):
        struct.pack_into('>f', self._buf, rel, v)
        self.dirty = True

    def peek_u32(self, rel):
        return struct.unpack_from('>I', self._buf, rel)[0]

    def poke_u32(self, rel, v):
        struct.pack_into('>I', self._buf, rel, v)
        self.dirty = True

    def __repr__(self):
        return '<Blob %s 0x%x +%d%s>' % (self.label, self.offset, self.size,
                                         ' DIRTY' if self.dirty else '')


class Record(object):
    """A decoded fixed-layout structure.

    Emits its original bytes until a field is assigned; only then is pack()
    used.  ``faithful`` records whether pack() reproduced the source bytes at
    parse time -- if it did not, the record is frozen against edits.
    """
    __slots__ = ('offset', '_raw', 'faithful', '_dirty')
    _FIELDS = ()

    def _init_span(self, offset, raw):
        self.offset = offset
        self._raw = bytes(raw)
        self._dirty = False
        try:
            self.faithful = (self.pack() == self._raw)
        except Exception:
            self.faithful = False

    @property
    def size(self):
        return len(self._raw)

    def touch(self):
        if not self.faithful:
            raise FrozenRecord(
                '%s at 0x%x did not reproduce its source bytes; refusing to '
                'edit (it will still round-trip verbatim)'
                % (type(self).__name__, self.offset))
        self._dirty = True

    @property
    def dirty(self):
        return self._dirty

    def to_bytes(self):
        return self.pack() if self._dirty else self._raw

    def pack(self):
        raise NotImplementedError


class RecordList(Span):
    """A run of Records (plus optional trailing pad) covering one span."""
    __slots__ = ('records', 'tail', 'label')

    def __init__(self, offset, records, tail=b'', label=''):
        self.records = records
        self.tail = tail
        self.label = label
        Span.__init__(self, offset, sum(r.size for r in records) + len(tail))

    def to_bytes(self):
        return b''.join(r.to_bytes() for r in self.records) + self.tail

    @property
    def dirty(self):
        return any(r.dirty for r in self.records)

    def unfaithful(self):
        return [r for r in self.records if not r.faithful]


class Container(Span):
    """An ordered list of child spans that must tile the parent exactly."""
    __slots__ = ('children', 'label')

    def __init__(self, offset, size, children, label=''):
        Span.__init__(self, offset, size)
        self.children = children
        self.label = label

    def to_bytes(self):
        return b''.join(c.to_bytes() for c in self.children)


def _fill(offset, size, data, placed, label):
    """Sort `placed` [(off, span)] and fill every hole with a Blob.

    Guarantees the returned children tile [offset, offset+size) exactly, so an
    undecoded hole becomes a verbatim blob instead of a silent data loss.
    """
    placed = sorted(placed, key=lambda p: p[0])
    out, cur = [], offset
    for off, span in placed:
        if off < cur:
            raise GeomError('%s: overlap at 0x%x (cursor 0x%x)' % (label, off, cur))
        if off > cur:
            out.append(Blob(cur, data[cur:off], '%s.gap' % label))
        out.append(span)
        cur = off + span.size
    end = offset + size
    if cur > end:
        raise GeomError('%s: children overrun end 0x%x > 0x%x' % (label, cur, end))
    if cur < end:
        out.append(Blob(cur, data[cur:end], '%s.tail' % label))
    return out


# ---------------------------------------------------------------------------
# header
# ---------------------------------------------------------------------------
class Header(Span):
    """The 0x80-byte container header, fully field-decoded."""
    __slots__ = ('magic', 'file_size', 'entry_count', 'w0c', 'extra_vec',
                 'w1c', 'entries', 'tail', '_raw')

    def __init__(self, data):
        Span.__init__(self, 0, HEADER_SIZE)
        self._raw = bytes(data[:HEADER_SIZE])
        self.magic = _u32(data, 0)
        if self.magic != MAGIC:
            raise GeomError('bad magic 0x%08x (file still encrypted?)' % self.magic)
        self.file_size = _u32(data, 4)
        self.entry_count = _u32(data, 8)
        self.w0c = _u32(data, 0x0c)
        self.extra_vec = struct.unpack_from('>3f', data, 0x10)
        self.w1c = _u32(data, 0x1c)
        table_end = 0x20 + self.entry_count * 12
        if table_end > HEADER_SIZE:
            raise GeomError('section table overruns header (%d entries)' % self.entry_count)
        self.entries = [list(struct.unpack_from('>3I', data, 0x20 + i * 12))
                        for i in range(self.entry_count)]
        self.tail = bytes(data[table_end:HEADER_SIZE])

    def pack(self, entries=None, file_size=None):
        """Serialise.  `entries` / `file_size` override the stored values
        WITHOUT mutating the model, so writing never has side effects."""
        out = bytearray(struct.pack(
            '>IIII', self.magic,
            self.file_size if file_size is None else file_size,
            self.entry_count, self.w0c))
        out += struct.pack('>3f', *self.extra_vec)
        out += struct.pack('>I', self.w1c)
        for tag, size, off in (self.entries if entries is None else entries):
            out += struct.pack('>3I', tag, size, off)
        out += self.tail
        if len(out) != HEADER_SIZE:
            raise GeomError('header packed to %d bytes' % len(out))
        return bytes(out)

    def to_bytes(self):
        return self.pack()

    @property
    def faithful(self):
        return self.pack() == self._raw


# ---------------------------------------------------------------------------
# payload records
# ---------------------------------------------------------------------------
class SubHeader(Record):
    """Payload sub-header: the collision world's root descriptor (0x40 B)."""
    __slots__ = ('world_min', 'trailer_off', 'cell_size', 'one', 'dims',
                 'desc_end', 'w30', 'w34', 'grid_stride', 'grid_off', 'desc_off')

    def __init__(self, data, o):
        self.world_min = struct.unpack_from('>3f', data, o)
        self.trailer_off = _u32(data, o + 0x0c)
        self.cell_size = struct.unpack_from('>3f', data, o + 0x10)
        self.one = _u32(data, o + 0x1c)
        self.dims = struct.unpack_from('>3I', data, o + 0x20)
        self.desc_end = _u32(data, o + 0x2c)
        self.w30 = _u32(data, o + 0x30)
        self.w34 = _u16(data, o + 0x34)
        self.grid_stride = _u16(data, o + 0x36)
        self.grid_off = _u32(data, o + 0x38)
        self.desc_off = _u32(data, o + 0x3c)
        self._init_span(o, data[o:o + 0x40])

    def pack(self):
        return (struct.pack('>3fI', *(list(self.world_min) + [self.trailer_off]))
                + struct.pack('>3fI', *(list(self.cell_size) + [self.one]))
                + struct.pack('>3II', *(list(self.dims) + [self.desc_end]))
                + struct.pack('>IHHII', self.w30, self.w34, self.grid_stride,
                              self.grid_off, self.desc_off))

    @property
    def cell_count(self):
        return self.dims[0] * self.dims[1] * self.dims[2]


class GridCell(Record):
    """One broadphase grid cell.  index==0x7FFF means empty.

    Only the leading u16 is decoded; the remaining stride-2 bytes are kept
    verbatim (their meaning was measured NOT to be a run of descriptor
    indices, so they are deliberately left alone).
    """
    __slots__ = ('index', 'rest')

    def __init__(self, data, o, stride):
        self.index = _u16(data, o)
        self.rest = bytes(data[o + 2:o + stride])
        self._init_span(o, data[o:o + stride])

    def pack(self):
        return struct.pack('>H', self.index) + self.rest

    @property
    def empty(self):
        return self.index == 0x7FFF


class Descriptor(Record):
    """32-byte directory entry for one collision block."""
    __slots__ = ('type', 'b1', 'block_size', 'f4', 'f6', 'w8', 'pool',
                 'block', 'w14', 'flags', 'chash')

    def __init__(self, data, o):
        (self.type, self.b1, self.block_size, self.f4, self.f6, self.w8,
         self.pool, self.block, self.w14, self.flags,
         self.chash) = struct.unpack_from('>BBHHHIIIIII', data, o)
        self._init_span(o, data[o:o + 32])

    def pack(self):
        return struct.pack('>BBHHHIIIIII', self.type, self.b1, self.block_size,
                           self.f4, self.f6, self.w8, self.pool, self.block,
                           self.w14, self.flags, self.chash)

    @property
    def has_pool(self):
        return self.pool > self.block


class Block(Blob):
    """One collision block: BVH nodes + (usually) a shared vertex pool.

    Held as bytes and edited surgically.  That is deliberate: the node
    grammar is understood but not perfectly (1 block in n002a repeats a
    112-byte group), and a byte-preserving block can never drop unparsed
    nodes.  Positions are still fully addressable via the typed helpers.

    VERTEX POOL (at descriptor.pool):
        u32 total_rows, u32 lead_rows, u32 1, u32 0, then total_rows 16B rows
        row[0]                    = block ORIGIN, world space
        row[1..lead-1]            = unit normals
        row[lead..total-1]        = vertex positions LOCAL to row[0]
        world position            = row[0] + row[k]
    """
    __slots__ = ('desc', 'index')

    def __init__(self, offset, data, desc, index):
        Blob.__init__(self, offset, data, 'block%d' % index)
        self.desc = desc
        self.index = index

    # -- vertex pool -------------------------------------------------------
    @property
    def _pool_rel(self):
        if not self.desc.has_pool:
            return None
        rel = self.desc.pool - self.offset
        return rel if 0 <= rel + 16 <= self.size else None

    def pool_info(self):
        """(total_rows, lead_rows) or None when the block has no vertex pool."""
        rel = self._pool_rel
        if rel is None:
            return None
        return (self.peek_u32(rel), self.peek_u32(rel + 4))

    def get_origin(self):
        """Block origin in world space (vertex-pool row 0), or None."""
        rel = self._pool_rel
        if rel is None:
            return None
        return struct.unpack_from('>3f', self._buf, rel + 16)

    def set_origin(self, x, y, z):
        rel = self._pool_rel
        if rel is None:
            raise GeomError('block %d has no vertex pool' % self.index)
        struct.pack_into('>3f', self._buf, rel + 16, x, y, z)
        self.dirty = True

    def origin_file_offset(self):
        rel = self._pool_rel
        return None if rel is None else self.offset + rel + 16

    def vertices(self):
        """Reconstructed WORLD-space vertices [(x, y, z), ...]."""
        info = self.pool_info()
        if info is None:
            return []
        total, lead = info
        rel = self._pool_rel
        ox, oy, oz = struct.unpack_from('>3f', self._buf, rel + 16)
        out = []
        for k in range(lead, total):
            x, y, z = struct.unpack_from('>3f', self._buf, rel + 16 + k * 16)
            out.append((x + ox, y + oy, z + oz))
        return out

    # -- BVH node walk -----------------------------------------------------
    def nodes(self):
        """[(class_id, rel_offset, node_size)] or None if the walk desyncs.

        Never raises: a desync returns None and the block still round-trips.
        """
        limit = self._pool_rel
        if limit is None:
            limit = self.size
        out, o = [], 0
        while o + 32 <= limit:
            cls = _u16(self._buf, o + 2)
            if cls == NODE_LEAF:
                n = 32 + ((self._buf[o] * 8 + 15) // 16) * 16
            elif cls in NODE_FIXED_SIZE:
                n = NODE_FIXED_SIZE[cls]
            else:
                return None
            out.append((cls, o, n))
            o += n
        # type-0x21 volume blocks declare a size 8 bytes short of the last
        # node's stride; those 8 bytes live in the following alignment gap.
        return out if limit <= o <= limit + 8 else None

    def aabb_centres(self):
        """[(rel_offset_of_centre, (x, y, z))] for world-space AABB nodes."""
        nl = self.nodes()
        if nl is None:
            return []
        out = []
        for cls, o, n in nl:
            if cls in (0x2004, 0x2005, 0x2024):
                out.append((o + 48, struct.unpack_from('>3f', self._buf, o + 48)))
        return out

    def translate(self, dx, dy, dz):
        """Move the whole block: origin + every world-space AABB centre.

        Returns the list of absolute file offsets whose bytes were rewritten.
        """
        touched = []
        org = self.get_origin()
        if org is not None:
            self.set_origin(org[0] + dx, org[1] + dy, org[2] + dz)
            touched.append(self.origin_file_offset())
        for rel, (x, y, z) in self.aabb_centres():
            struct.pack_into('>3f', self._buf, rel, x + dx, y + dy, z + dz)
            touched.append(self.offset + rel)
            self.dirty = True
        return touched


class World(object):
    """One collision world inside the payload.

    MEASURED 2026-08-28 on all 25 decrypted geoms: a payload holds 1..8 worlds
    laid out SEQUENTIALLY -- the 0x40-byte sub-headers run back-to-back from
    the section base, then for each world in turn: grid, descriptor table,
    data blocks, trailer.  17/25 shipped geoms are multi-world (the ones the
    game actually loads for Blood Bath are 8-world), so a parser that reads
    only the first sub-header sees a small fraction of the collision.
    Sub-header fields, corrected:
        +0x2c desc_end  is RELATIVE TO grid_off: grid_off + desc_end == first
                        block == exact end of the descriptor table (25/25)
        +0x30 w30       1 on the LAST world, 0 otherwise (terminator flag)
    All other offsets are absolute file offsets.
    """
    __slots__ = ('index', 'sub', 'grid', 'descriptors', 'blocks', 'trailer')

    def __init__(self, index, sub, grid, descriptors, blocks, trailer):
        self.index = index
        self.sub = sub
        self.grid = grid
        self.descriptors = descriptors
        self.blocks = blocks          # Block objects (shared list members)
        self.trailer = trailer

    @property
    def dims(self):
        return self.sub.dims

    def cell_index(self, x, y, z):
        """Linear grid index; measured ordering is i = x + gx*(z + gz*y)."""
        gx, gy, gz = self.sub.dims
        return x + gx * (z + gz * y)

    def cells_used(self):
        return sum(0 if c.empty else 1 for c in self.grid.records)

    def vertex_count(self):
        return sum(len(b.vertices()) for b in self.blocks)


class PayloadSection(Container):
    """Section tag 0x000000 -- the static collision database (ALL worlds).

    ``worlds``  : [World] in file order
    ``blocks``  : every Block in the payload, deduplicated by offset, with a
                  GLOBAL index (Block.index) that is stable across parses
    ``sub`` / ``grid`` / ``descriptors``  : world 0 (backward compatibility)
    """
    __slots__ = ('tag', 'subheaders', 'worlds', 'blocks')

    def __init__(self, data, offset, size):
        self.tag = PAYLOAD_TAG
        end = offset + size

        # 1. the sub-header run.  w30 == 1 marks the last world; cross-check
        #    against the first grid offset, which starts right after the run.
        subs, o = [], offset
        while True:
            s = SubHeader(data, o)
            subs.append(s)
            if s.w30 == 1:
                break
            o += 0x40
            if len(subs) > 256 or o + 0x40 > end:
                raise GeomError('sub-header run did not terminate')
        span = subs[0].grid_off - offset
        if span % 0x40 or span // 0x40 != len(subs):
            raise GeomError('world count disagreement: terminator says %d, '
                            'grid_off implies %s' % (len(subs), span / 64.0))
        self.subheaders = RecordList(offset, subs, label='subheader')
        placed = [(offset, self.subheaders)]

        # 2. each world: grid, descriptors, blocks, trailer
        self.worlds, self.blocks = [], []
        seen = {}
        for wi, sub in enumerate(subs):
            cells = [GridCell(data, sub.grid_off + i * sub.grid_stride, sub.grid_stride)
                     for i in range(sub.cell_count)]
            grid = RecordList(sub.grid_off, cells, label='grid%d' % wi)
            placed.append((sub.grid_off, grid))

            descs = self._read_descriptors(data, sub)
            dlist = RecordList(sub.desc_off, descs, label='descriptors%d' % wi)
            placed.append((sub.desc_off, dlist))

            wblocks = []
            for de in descs:
                b = seen.get(de.block)
                if b is None:
                    b = Block(de.block, data[de.block:de.block + de.block_size],
                              de, len(self.blocks))
                    seen[de.block] = b
                    self.blocks.append(b)
                    placed.append((b.offset, b))
                wblocks.append(b)

            tend = subs[wi + 1].grid_off if wi + 1 < len(subs) else end
            if sub.trailer_off < tend:
                trailer = Blob(sub.trailer_off, data[sub.trailer_off:tend], 'trailer%d' % wi)
                placed.append((sub.trailer_off, trailer))
            else:
                trailer = Blob(tend, b'', 'trailer%d' % wi)   # empty, not placed
            self.worlds.append(World(wi, sub, grid, dlist, wblocks, trailer))

        Container.__init__(self, offset, size,
                           _fill(offset, size, data, placed, 'payload'),
                           'payload')

    # -- backward-compatible world-0 accessors ------------------------------
    @property
    def sub(self):
        return self.worlds[0].sub

    @property
    def grid(self):
        return self.worlds[0].grid

    @property
    def descriptors(self):
        return self.worlds[0].descriptors

    @property
    def trailer(self):
        return self.worlds[-1].trailer

    def all_descriptors(self):
        return [d for w in self.worlds for d in w.descriptors.records]

    @staticmethod
    def _read_descriptors(data, sub):
        """Descriptors from desc_off up to the lowest block offset.

        The declared table end is grid_off + desc_end (desc_end is relative
        to the grid).  The self-terminating scan below is kept as the primary
        reader and the declared count as the fallback, so a weird file
        degrades instead of failing.
        """
        n_end = max((sub.grid_off + sub.desc_end - sub.desc_off) // 32, 0)
        safe = [Descriptor(data, sub.desc_off + i * 32) for i in range(n_end)]
        try:
            out, limit, i = [], None, 0
            while True:
                o = sub.desc_off + i * 32
                if limit is not None and o + 32 > limit:
                    break
                if o + 32 > len(data) or i > 1 << 20:
                    raise GeomError('descriptor table runs past EOF')
                d = Descriptor(data, o)
                out.append(d)
                limit = d.block if limit is None else min(limit, d.block)
                i += 1
            if not out or limit is None or limit <= sub.desc_off:
                raise GeomError('descriptor scan did not converge')
            if sub.desc_off + len(out) * 32 != limit:
                raise GeomError('table end 0x%x is not block-aligned' % limit)
            spans = sorted((d.block, d.block + d.block_size) for d in out)
            if spans[0][0] < limit or spans[-1][1] > sub.trailer_off:
                raise GeomError('block outside the payload')
            if any(a[1] > b[0] for a, b in zip(spans, spans[1:])):
                raise GeomError('overlapping blocks')
            return out
        except (GeomError, struct.error):
            return safe

    def block_by_index(self, i):
        return self.blocks[i]

    def cell_index(self, x, y, z, world=0):
        return self.worlds[world].cell_index(x, y, z)


# ---------------------------------------------------------------------------
# section 0x050000 -- polylines / paths
# ---------------------------------------------------------------------------
class PolyPoint(Record):
    """32-byte path node.  seg_len is the distance to the NEXT point."""
    __slots__ = ('a', 'b', 'z0', 'seg_len', 'x', 'y', 'z', 'z1')

    def __init__(self, data, o):
        (self.a, self.b, self.z0, self.seg_len, self.x, self.y, self.z,
         self.z1) = struct.unpack_from('>IIIffffI', data, o)
        self._init_span(o, data[o:o + 32])

    def pack(self):
        return struct.pack('>IIIffffI', self.a, self.b, self.z0, self.seg_len,
                           self.x, self.y, self.z, self.z1)

    @property
    def pos(self):
        return (self.x, self.y, self.z)

    @pos.setter
    def pos(self, v):
        self.touch()
        self.x, self.y, self.z = float(v[0]), float(v[1]), float(v[2])


class Polyline(Record):
    """{u32 id, u32 count, u32 0, u32 0} + count * PolyPoint."""
    __slots__ = ('id', 'count', 'z0', 'z1', 'points')

    def __init__(self, data, o):
        self.id, self.count, self.z0, self.z1 = struct.unpack_from('>4I', data, o)
        self.points = [PolyPoint(data, o + 16 + i * 32) for i in range(self.count)]
        self._init_span(o, data[o:o + 16 + self.count * 32])

    def pack(self):
        return (struct.pack('>4I', self.id, self.count, self.z0, self.z1)
                + b''.join(p.to_bytes() for p in self.points))

    @property
    def dirty(self):
        return self._dirty or any(p.dirty for p in self.points)

    def to_bytes(self):
        return self.pack() if self.dirty else self._raw


class PolylineSection(RecordList):
    """0x050000.  Walk verified exact on 14/14 shipped stages.

    The section ends with a 16-byte terminator beginning 0x07000000.  If the
    walk ever fails to land on it, the whole section is kept verbatim rather
    than raising -- a grammar failure must never stop a file from loading or
    round-tripping.
    """
    __slots__ = ('tag', 'ok', 'reason')

    def __init__(self, data, offset, size):
        self.tag = TAG_POLYLINE
        self.ok, self.reason = True, ''
        end = offset + size
        recs, o = [], offset
        try:
            while o < end - 16:
                p = Polyline(data, o)
                if p.count < 0 or o + p.size > end - 16:
                    raise GeomError('polyline at 0x%x overruns the terminator' % o)
                recs.append(p)
                o += p.size
            if o != end - 16:
                raise GeomError('walk ended at 0x%x, terminator expected at 0x%x'
                                % (o, end - 16))
            if _u32(data, o) != 0x07000000:
                raise GeomError('terminator at 0x%x is 0x%08x, expected 0x07000000'
                                % (o, _u32(data, o)))
        except (GeomError, struct.error) as exc:
            self.ok, self.reason, recs, o = False, str(exc), [], offset
        RecordList.__init__(self, offset, recs, data[o:end], 'polylines')


# ---------------------------------------------------------------------------
# section 0x060000 -- placed-object / entity instances
# ---------------------------------------------------------------------------
class RawBlock(Record):
    """A 16-byte run inside the PROPS chunk that is not an Instance.

    MEASURED 2026-09-05: n022a and n024a carry a ROUTES-style directory
    (u32 id, u32 0, u16 count, u32 ptr) inside the PROPS byte range, and n020a
    one stray position-shaped float4.  Walking past them 0x10 at a time makes
    all 25 sections close on their exact section end.  They round-trip
    verbatim and are deliberately not editable.
    """
    __slots__ = ()

    def __init__(self, data, o, n=16):
        self._init_span(o, data[o:o + n])

    def pack(self):
        return self._raw


class Instance(Record):
    """Variable-length placed-object record.

        u32 flags, u32 group, u32 id, u32 payload-bitmask
        bit 0x000002 -> float4 position (x, y, z, 1.0)
        bit 0x400000 -> float4 scale-or-tint (measured ~(1,1,1,1); ambiguous)
        bit 0x001000 -> 16B orientation: u32 yaw16 + 3 words
        bit 0x000800 -> recognised but carries NO block (measured)

    yaw degrees = (word >> 16) / 65536 * 360  when stored in the high half,
    which is how every shipped record stores it.
    """
    __slots__ = ('flags', 'group', 'id', 'pay', 'pos', 'scale', 'orient')

    def __init__(self, data, o):
        self.flags, self.group, self.id, self.pay = struct.unpack_from('>4I', data, o)
        p = o + 16
        self.pos = self.scale = self.orient = None
        if self.pay not in INST_NO_PAYLOAD:
            if (self.pay & ~INST_KNOWN_BITS) or not (self.pay & INST_POS):
                raise GeomError('unknown instance payload 0x%08x at 0x%x' % (self.pay, o))
            self.pos = struct.unpack_from('>4f', data, p)
            p += 16
            if self.pay & INST_SCALE:
                self.scale = struct.unpack_from('>4f', data, p)
                p += 16
            if self.pay & INST_ORIENT:
                self.orient = struct.unpack_from('>4I', data, p)
                p += 16
        self._init_span(o, data[o:p])

    def pack(self):
        out = struct.pack('>4I', self.flags, self.group, self.id, self.pay)
        if self.pos is not None:
            out += struct.pack('>4f', *self.pos)
        if self.scale is not None:
            out += struct.pack('>4f', *self.scale)
        if self.orient is not None:
            out += struct.pack('>4I', *self.orient)
        return out

    # -- convenience -------------------------------------------------------
    @property
    def position(self):
        return None if self.pos is None else self.pos[:3]

    @position.setter
    def position(self, v):
        if self.pos is None:
            raise GeomError('instance 0x%x has no position block' % self.offset)
        self.touch()
        self.pos = (float(v[0]), float(v[1]), float(v[2]), self.pos[3])

    def translate(self, dx, dy, dz):
        p = self.position
        if p is None:
            raise GeomError('instance 0x%x has no position block' % self.offset)
        self.position = (p[0] + dx, p[1] + dy, p[2] + dz)

    @property
    def pos_file_offset(self):
        return None if self.pos is None else self.offset + 16

    @property
    def yaw_deg(self):
        if self.orient is None:
            return None
        return (self.orient[0] >> 16) / 65536.0 * 360.0

    @yaw_deg.setter
    def yaw_deg(self, deg):
        if self.orient is None:
            raise GeomError('instance 0x%x has no orientation block' % self.offset)
        self.touch()
        w = int(round(deg / 360.0 * 65536.0)) & 0xFFFF
        self.orient = (w << 16 | (self.orient[0] & 0xFFFF),) + self.orient[1:]

    def __repr__(self):
        p = '' if self.pos is None else ' pos=(%.1f,%.1f,%.1f)' % self.pos[:3]
        return '<Instance 0x%x id=0x%06x pay=0x%x%s>' % (self.offset, self.id, self.pay, p)


class InstanceSection(RecordList):
    """0x060000.  Decodes when the walk is clean; otherwise stays opaque.

    MEASURED: clean on 11 of 14 shipped stages.  n020a (1 record), n022a (17)
    and n024a (45) contain payload words that are not component bitmasks, so
    those three keep the whole section verbatim -- they still round-trip, they
    just are not editable through the record API.
    """
    __slots__ = ('tag', 'ok', 'reason', 'skipped')

    def __init__(self, data, offset, size):
        self.tag = TAG_INSTANCE
        self.ok, self.reason = True, ''
        end, recs, o = offset + size, [], offset
        skipped = 0
        try:
            while o + 16 <= end:
                try:
                    r = Instance(data, o)
                    if o + r.size > end:
                        raise GeomError('instance overruns section at 0x%x' % o)
                except GeomError:
                    # Not an Instance: keep 0x10 bytes verbatim and resync.
                    recs.append(RawBlock(data, o))
                    skipped += 1
                    o += 16
                    continue
                recs.append(r)
                o += r.size
            if o != end:
                raise GeomError('walk ended at 0x%x, section ends 0x%x' % (o, end))
        except GeomError as exc:
            self.ok, self.reason, recs, o = False, str(exc), [], offset
        self.skipped = skipped
        RecordList.__init__(self, offset, recs, data[o:end], 'instances')


# ---------------------------------------------------------------------------
# the file
# ---------------------------------------------------------------------------
class BlobSection(Blob):
    __slots__ = ('tag',)

    def __init__(self, tag, offset, data, label):
        Blob.__init__(self, offset, data, label)
        self.tag = tag


class GeomFile(object):
    """Structured, round-trippable model of one decrypted .geom."""

    def __init__(self, data, path=None):
        self.source = bytes(data)
        self.path = path
        self.header = Header(self.source)
        self.sections = {}
        placed = [(0, self.header)]
        for tag, size, off in self.header.entries:
            sec = self._make_section(tag, off, size)
            self.sections[tag] = sec
            placed.append((off, sec))
        self.spans = _fill(0, len(self.source), self.source, placed, 'file')

    # -- construction ------------------------------------------------------
    def _make_section(self, tag, off, size):
        d = self.source
        if off + size > len(d):
            raise GeomError('section 0x%06x runs past EOF' % tag)
        if tag == PAYLOAD_TAG:
            return PayloadSection(d, off, size)
        if tag == TAG_POLYLINE:
            return PolylineSection(d, off, size)
        if tag == TAG_INSTANCE:
            return InstanceSection(d, off, size)
        return BlobSection(tag, off, d[off:off + size], 'sec_0x%06x' % tag)

    @classmethod
    def load(cls, path):
        with open(path, 'rb') as fp:
            return cls(fp.read(), path)

    # -- convenient accessors ---------------------------------------------
    @property
    def payload(self):
        return self.sections[PAYLOAD_TAG]

    @property
    def blocks(self):
        return self.payload.blocks

    @property
    def instances(self):
        s = self.sections.get(TAG_INSTANCE)
        return s.records if isinstance(s, InstanceSection) else []

    @property
    def polylines(self):
        s = self.sections.get(TAG_POLYLINE)
        return s.records if isinstance(s, PolylineSection) else []

    # -- serialisation -----------------------------------------------------
    def to_bytes(self, fix_header=True, normalize_size_field=False):
        """Serialise the model.  Pure: never mutates the model.

        fix_header           rewrite the section table (and +0x04) from the
                             ACTUAL emitted offsets/sizes when they moved.
        normalize_size_field also repair a +0x04 that was already wrong in the
                             source.  Off by default, because n023a/n014a ship
                             with a stale +0x04 and byte-identity must win over
                             tidiness.
        """
        parts, layout = [], []
        cur = 0
        hdr_i = None
        for i, sp in enumerate(self.spans):
            b = sp.to_bytes()
            if sp is self.header:
                hdr_i = i
            layout.append((getattr(sp, 'tag', None), cur, len(b)))
            parts.append(b)
            cur += len(b)
        if fix_header and hdr_i is not None:
            entries = [list(e) for e in self.header.entries]
            moved = False
            for tag, off, ln in layout:
                if tag is None:
                    continue
                for e in entries:
                    if e[0] == tag and (e[1] != ln or e[2] != off):
                        e[1], e[2] = ln, off
                        moved = True
            size_field = self.header.file_size
            resized = (cur != len(self.source))
            if resized or normalize_size_field:
                size_field = cur
            if moved or size_field != self.header.file_size:
                parts[hdr_i] = self.header.pack(entries=entries, file_size=size_field)
        return b''.join(parts)

    def save(self, path, fix_header=True, normalize_size_field=False):
        data = self.to_bytes(fix_header, normalize_size_field)
        with open(path, 'wb') as fp:
            fp.write(data)
        return len(data)

    # -- diagnostics -------------------------------------------------------
    def roundtrip_ok(self):
        return self.to_bytes() == self.source

    def unfaithful(self):
        """Decoded records that could NOT reproduce their own source bytes."""
        bad = []
        if not self.header.faithful:
            bad.append(('header', self.header))
        p = self.payload
        bad += [('subheader', r) for r in p.subheaders.unfaithful()]
        for w in p.worlds:
            bad += [('grid%d' % w.index, r) for r in w.grid.unfaithful()]
            bad += [('descriptors%d' % w.index, r) for r in w.descriptors.unfaithful()]
        for tag in (TAG_POLYLINE, TAG_INSTANCE):
            s = self.sections.get(tag)
            if isinstance(s, RecordList):
                bad += [('0x%06x' % tag, r) for r in s.unfaithful()]
                if tag == TAG_POLYLINE:
                    for r in s.records:
                        bad += [('0x%06x.pt' % tag, q) for q in r.points if not q.faithful]
        return bad

    def force_pack(self):
        """Re-serialise EVERY decoded record from its parsed fields (ignoring
        the raw-bytes shortcut).  Proves the field decode is complete, not
        merely that the bytes were copied.  Returns (bytes, [mismatches])."""
        mism = []
        p = self.payload

        def emit(rl, name):
            out = bytearray()
            for r in rl.records:
                b = r.pack()
                if b != r._raw:
                    mism.append((name, r.offset))
                out += b
            return bytes(out) + rl.tail

        parts = []
        for sp in self.spans:
            if sp is self.header:
                b = self.header.pack()
                if b != self.header._raw:
                    mism.append(('header', 0))
                parts.append(b)
            elif sp is p:
                sub = []
                for c in p.children:
                    if isinstance(c, RecordList):
                        sub.append(emit(c, 'payload.%s' % c.label))
                    else:
                        sub.append(c.to_bytes())
                parts.append(b''.join(sub))
            elif isinstance(sp, RecordList):
                parts.append(emit(sp, 'sec_0x%06x' % getattr(sp, 'tag', 0)))
            else:
                parts.append(sp.to_bytes())
        return b''.join(parts), mism

    def summary(self):
        p = self.payload
        inst = self.sections.get(TAG_INSTANCE)
        return {
            'path': self.path,
            'size': len(self.source),
            'entries': [(hex(t), s, hex(o)) for t, s, o in self.header.entries],
            'world_min': tuple(round(v, 2) for v in p.sub.world_min),
            'cell_size': p.sub.cell_size,
            'dims': p.sub.dims,
            'worlds': len(p.worlds),
            'cells': sum(w.sub.cell_count for w in p.worlds),
            'cells_used': sum(w.cells_used() for w in p.worlds),
            'descriptors': len(p.all_descriptors()),
            'blocks': len(p.blocks),
            'per_world': [(w.sub.dims, w.cells_used(), len(w.descriptors.records),
                           len(w.blocks)) for w in p.worlds],
            'blocks_with_pool': sum(1 for b in p.blocks if b.pool_info()),
            'spans': len(self.spans),
            'payload_children': len(p.children),
            'polylines': len(self.polylines),
            'instances': len(self.instances),
            'instances_decoded': getattr(inst, 'ok', None),
            'instance_reason': getattr(inst, 'reason', ''),
        }


# ---------------------------------------------------------------------------
# byte-diff helper
# ---------------------------------------------------------------------------
def diff_ranges(a, b):
    """[(start, end, a_bytes, b_bytes)] contiguous differing runs."""
    if len(a) != len(b):
        return [(0, max(len(a), len(b)), b'<len %d>' % len(a), b'<len %d>' % len(b))]
    out, i, n = [], 0, len(a)
    while i < n:
        if a[i] != b[i]:
            j = i
            while j < n and a[j] != b[j]:
                j += 1
            out.append((i, j, a[i:j], b[i:j]))
            i = j
        else:
            i += 1
    return out


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def _md5(b):
    return hashlib.md5(b).hexdigest()


def cmd_info(paths):
    for p in paths:
        g = GeomFile.load(p)
        s = g.summary()
        print('=' * 74)
        print('%s  %d bytes' % (p, s['size']))
        for t, sz, o in s['entries']:
            print('   tag %-10s size %9d off %s' % (t, sz, o))
        print('   world_min %s  cell %s  dims %s' % (s['world_min'], s['cell_size'], s['dims']))
        print('   worlds %d  grid cells %d (%d used)  descriptors %d  blocks %d (%d with pool)'
              % (s['worlds'], s['cells'], s['cells_used'], s['descriptors'], s['blocks'],
                 s['blocks_with_pool']))
        for i, (dims, used, nd, nb) in enumerate(s['per_world']):
            print('      w%d dims %-12s used %-5d descriptors %-5d blocks %d'
                  % (i, dims, used, nd, nb))
        print('   spans %d  payload children %d  polylines %d  instances %d (decoded=%s) %s'
              % (s['spans'], s['payload_children'], s['polylines'], s['instances'],
                 s['instances_decoded'], s['instance_reason']))
    return 0


def cmd_roundtrip(paths):
    print('%-30s %10s  %-32s %-32s %s' % ('file', 'bytes', 'md5(in)', 'md5(out)', 'result'))
    allok = True
    for p in paths:
        g = GeomFile.load(p)
        out = g.to_bytes()
        a, b = _md5(g.source), _md5(out)
        ok = (out == g.source)
        allok &= ok
        import os
        print('%-30s %10d  %-32s %-32s %s'
              % (os.path.basename(p), len(g.source), a, b,
                 'IDENTICAL' if ok else 'MISMATCH (%d diff runs)'
                 % len(diff_ranges(g.source, out))))
    print('\nALL BYTE-IDENTICAL: %s' % allok)
    return 0 if allok else 1


def cmd_selftest(paths):
    import os
    allok = True
    for p in paths:
        g = GeomFile.load(p)
        rt = g.roundtrip_ok()
        forced, mism = g.force_pack()
        fok = (forced == g.source)
        unf = g.unfaithful()
        allok &= rt and fok
        print('%-26s roundtrip=%-5s force-pack=%-5s mismatches=%-4d unfaithful=%d %s'
              % (os.path.basename(p), rt, fok, len(mism), len(unf),
                 '' if not mism else mism[:3]))
    print('\nALL PASS: %s' % allok)
    return 0 if allok else 1


def _report_diff(src, out, expect, label):
    """Print every differing byte range and verify CONTAINMENT: every byte
    that changed must lie inside one of the `expect` (offset, length) fields.

    Containment, not equality, is the right test.  Rewriting a f32 typically
    changes only 2 of its 4 bytes because the sign/exponent bytes are shared,
    so the observed diff is legitimately a subset of the edited field.
    """
    runs = diff_ranges(src, out)
    exp = sorted(expect)
    allowed = set()
    for o, n in exp:
        allowed.update(range(o, o + n))
    print('  %s' % label)
    print('    output size %d (input %d, delta %+d)'
          % (len(out), len(src), len(out) - len(src)))
    print('    edited fields      : %s' % ['0x%08x+%d' % t for t in exp])
    print('    differing byte runs: %d' % len(runs))
    stray = []
    for s, e, a, b in runs:
        inside = all(i in allowed for i in range(s, e))
        if not inside:
            stray.append((s, e))
        pa = a.hex() if len(a) <= 24 else a[:24].hex() + '...'
        pb = b.hex() if len(b) <= 24 else b[:24].hex() + '...'
        print('      0x%08x .. 0x%08x  (%d B)  %s -> %s   %s'
              % (s, e, e - s, pa, pb, 'in edited field' if inside else '*** STRAY ***'))
    nchanged = sum(e - s for s, e, _, _ in runs)
    ok = (not stray) and len(out) == len(src)
    print('    %d bytes changed, all within %d edited bytes; stray runs: %d'
          % (nchanged, len(allowed), len(stray)))
    print('    CHANGED ONLY INSIDE EDITED FIELDS: %s' % ok)
    return ok


def cmd_mutate(path, out_path=None):
    """Minimal-mutation proof: change one decoded value at a time and show
    that the output differs from the input ONLY in the predicted bytes."""
    results = []

    # ---- 1. translate ONE placed-object instance (section 0x060000) -------
    g = GeomFile.load(path)
    inst = next((i for i in g.instances if i.position is not None), None)
    if inst is None:
        sec = g.sections.get(TAG_INSTANCE)
        print('\n[1] SKIPPED: no editable instance (0x060000 decoded=%s %s)'
              % (getattr(sec, 'ok', None), getattr(sec, 'reason', '')))
    else:
        off = inst.pos_file_offset
        print('\n[1] instance: %r' % inst)
        print('    record at 0x%08x, position float3 at 0x%08x' % (inst.offset, off))
        before = inst.position
        inst.translate(1000.0, 0.0, -500.0)
        print('    %s -> %s' % (tuple(round(v, 1) for v in before),
                                tuple(round(v, 1) for v in inst.position)))
        results.append(_report_diff(g.source, g.to_bytes(), [(off, 12)],
                                    'translate 1 instance (3 x f32 = 12 bytes)'))

    # ---- 2. translate ONE collision block origin (payload vertex pool) ----
    g = GeomFile.load(path)
    blk = next(b for b in g.blocks if b.get_origin() is not None)
    off = blk.origin_file_offset()
    print('\n[2] collision block #%d at 0x%08x, pool origin float3 at 0x%08x'
          % (blk.index, blk.offset, off))
    before = blk.get_origin()
    blk.set_origin(before[0] + 2500.0, before[1], before[2])
    print('    %s -> %s' % (tuple(round(v, 1) for v in before),
                            tuple(round(v, 1) for v in blk.get_origin())))
    results.append(_report_diff(g.source, g.to_bytes(), [(off, 12)],
                                'move 1 collision block origin (12 bytes)'))

    # ---- 3. move ONE polyline point (section 0x050000) --------------------
    g = GeomFile.load(path)
    pl = next((p for p in g.polylines if p.points), None)
    if pl is None:
        print('\n[3] SKIPPED: no polyline with points')
    else:
        pt = pl.points[0]
        print('\n[3] polyline id=0x%06x point[0] at 0x%08x' % (pl.id, pt.offset))
        before = pt.pos
        pt.pos = (before[0], before[1] + 250.0, before[2])
        print('    %s -> %s' % (tuple(round(v, 1) for v in before),
                                tuple(round(v, 1) for v in pt.pos)))
        results.append(_report_diff(g.source, g.to_bytes(), [(pt.offset + 16, 12)],
                                    'move 1 polyline point (x,y,z at record +16)'))

    # ---- 4. whole-block translate: origin + every world AABB centre -------
    g = GeomFile.load(path)
    blk = next(b for b in g.blocks
               if b.get_origin() is not None and b.aabb_centres())
    print('\n[4] block #%d: full translate (origin + %d AABB centres)'
          % (blk.index, len(blk.aabb_centres())))
    touched = blk.translate(0.0, 100.0, 0.0)
    exp = sorted((t, 12) for t in touched)
    results.append(_report_diff(g.source, g.to_bytes(), exp,
                                'translate 1 collision object properly'))

    # ---- 5. size-changing edit: header +0x04 and table must follow -------
    g = GeomFile.load(path)
    grown = GeomFile.load(path)
    grown_sec = grown.sections[TAG_DIRECTORY]
    grown_sec._buf += b'\x00' * 16
    grown_sec.size += 16
    grown_sec.dirty = True
    out = grown.to_bytes()
    print('\n[5] size change: appended 16 bytes to section 0x070000')
    old_entries = {t: (s, o) for t, s, o in GeomFile.load(path).header.entries}
    new_hdr = Header(out)
    print('    +0x04 file size : %d -> %d  (actual output %d)'
          % (g.header.file_size, new_hdr.file_size, len(out)))
    tbl_ok = True
    for tag, size, off in new_hdr.entries:
        osz, ooff = old_entries[tag]
        mark = '' if (size, off) == (osz, ooff) else '   <- updated'
        if tag == TAG_DIRECTORY and size != osz + 16:
            tbl_ok = False
        print('    tag 0x%06x size %9d off 0x%08x%s' % (tag, size, off, mark))
    ok5 = (len(out) == len(g.source) + 16 and new_hdr.file_size == len(out) and tbl_ok)
    print('    header + section table consistent with new size: %s' % ok5)
    results.append(ok5)

    if out_path:
        g2 = GeomFile.load(path)
        b = next(x for x in g2.blocks if x.get_origin() is not None)
        o = b.get_origin()
        b.set_origin(o[0], o[1] + 500.0, o[2])
        n = g2.save(out_path)
        print('\nwrote mutated copy: %s (%d bytes)' % (out_path, n))

    print('\nMUTATION TESTS PASSED: %d/%d' % (sum(1 for r in results if r), len(results)))
    return 0 if all(results) else 1


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        print('usage: geom_io.py {info|roundtrip|selftest|mutate} <file.geom> ...')
        return 2
    cmd, paths = argv[1], argv[2:]
    if cmd == 'info':
        return cmd_info(paths)
    if cmd == 'roundtrip':
        return cmd_roundtrip(paths)
    if cmd == 'selftest':
        return cmd_selftest(paths)
    if cmd == 'mutate':
        return cmd_mutate(paths[0], paths[1] if len(paths) > 1 else None)
    print('unknown command %r' % cmd)
    return 2


if __name__ == '__main__':
    sys.exit(main(sys.argv))
