# -*- coding: utf-8 -*-
"""mdn_io — a byte-exact reader/writer for MGO2 / MGS4 `.mdn` mesh files.

Design note (this is the whole trick):
-------------------------------------
We do NOT need to understand every field to write a valid file. We model the
file as an ORDERED, GAPLESS list of segments covering [0, filesize). Segments
whose element layout we know (bones, meshes, faces, ...) are parsed into
records; segments we don't understand are carried as opaque bytes. The writer
re-lays-out the segments in order and recomputes every offset and size in the
header from where things ACTUALLY landed.

That gives two things:
  1. A round-trip identity test (parse -> write == original bytes) that proves
     the layout model is complete. Anything we mis-model shows up immediately.
  2. Edits become safe: change a segment's payload, and offsets downstream fix
     themselves instead of silently desyncing.

Header (big-endian, 0x60 bytes), all u32 unless noted:
  0x00 magic 'MDN '      0x04 filename hash
  0x08 nBone   0x0c nMeshGroup  0x10 nMesh      0x14 nFaceIndex
  0x18 nVertDef 0x1c nMaterial  0x20 nTexture   0x24 nBonePallet
  0x28 oBone   0x2c oMeshGroup  0x30 oVertIndex 0x34 oFaceIndex
  0x38 oVertDef 0x3c oMaterial  0x40 oTexture   0x44 oBonePallet
  0x48 oVertBuffer 0x4c sVertBuffer 0x50 oFaceBuffer 0x54 sFaceBuffer
  0x58 (zero)      0x5c filesize

Strides measured across all 1311 .mdn files on disk, unanimous unless noted:
  Bone 80, MeshGroup 16, VertIndex 80, FaceIndex 16, VertDef 48,
  Material 112, Texture 32, BonePallet 40 (BonePallet is last before the
  vertex buffer, so its measured gap includes alignment padding; 40 is the
  floor and matches the independent empirical finding in the project notes).
"""
import struct

MAGIC = b'MDN '
HEADER_SIZE = 0x60

COUNT_FIELDS = ['Bone', 'MeshGroup', 'Mesh', 'FaceIndex',
                'VertDef', 'Material', 'Texture', 'BonePallet']
# table name -> (header offset slot index, count field governing it, stride)
TABLES = [
    ('Bone',       'Bone',       80),
    ('MeshGroup',  'MeshGroup',  16),
    ('VertIndex',  'Mesh',       80),
    ('FaceIndex',  'FaceIndex',  16),
    ('VertDef',    'VertDef',    48),
    ('Material',   'Material',  112),
    ('Texture',    'Texture',    32),
    ('BonePallet', 'BonePallet', 40),
]


class MdnError(Exception):
    pass


class Segment:
    """One contiguous run of bytes in the file."""
    __slots__ = ('kind', 'name', 'data', 'offset', 'align')

    def __init__(self, kind, name, data, offset, align=1):
        self.kind = kind        # 'header' | 'table' | 'buffer' | 'pad'
        self.name = name
        self.data = bytearray(data)
        self.offset = offset    # offset in the ORIGINAL file (informational)
        self.align = align

    def __repr__(self):
        return f'<Segment {self.kind}:{self.name} @0x{self.offset:x} len={len(self.data)}>'


class Mdn:
    def __init__(self):
        self.hash = 0
        self.counts = {}
        self.segments = []      # ordered, gapless
        self.tail = b''         # bytes after filesize, if any (normally none)

    # ---------- reading ----------
    @classmethod
    def parse(cls, data):
        if len(data) < HEADER_SIZE:
            raise MdnError('too small to hold a header')
        if data[0:4] != MAGIC:
            raise MdnError(f'bad magic {data[0:4]!r}, expected {MAGIC!r}')
        m = cls()
        v = struct.unpack_from('>22I', data, 8)
        m.hash = struct.unpack_from('>I', data, 4)[0]
        for i, n in enumerate(COUNT_FIELDS):
            m.counts[n] = v[i]
        offs = {n: v[8 + i] for i, (n, _, _) in enumerate(TABLES)}
        oVB, sVB, oFB, sFB, zero, filesize = v[16:22]
        m._zero = zero

        if filesize != len(data):
            raise MdnError(f'header filesize 0x{filesize:x} != actual 0x{len(data):x}')

        # Build the region list: (start, length, kind, name)
        regions = []
        for name, cnt_field, stride in TABLES:
            c = m.counts[cnt_field]
            if c == 0:
                continue
            regions.append((offs[name], c * stride, 'table', name))
        if sVB:
            regions.append((oVB, sVB, 'buffer', 'VertBuffer'))
        if sFB:
            regions.append((oFB, sFB, 'buffer', 'FaceBuffer'))
        regions.sort()

        # Walk the file, emitting pad segments for anything not claimed.
        segs = [Segment('header', 'header', data[:HEADER_SIZE], 0)]
        cur = HEADER_SIZE
        for start, length, kind, name in regions:
            if start < cur:
                raise MdnError(f'region {name} at 0x{start:x} overlaps previous end 0x{cur:x}')
            if start > cur:
                segs.append(Segment('pad', f'pad_before_{name}', data[cur:start], cur))
            segs.append(Segment(kind, name, data[start:start + length], start))
            cur = start + length
        if cur < len(data):
            segs.append(Segment('pad', 'pad_tail', data[cur:], cur))
        m.segments = segs
        return m

    # ---------- convenience accessors ----------
    def segment(self, name):
        for s in self.segments:
            if s.name == name:
                return s
        return None

    def table(self, name):
        """Yield (index, memoryview) per element of a known table."""
        seg = self.segment(name)
        if seg is None:
            return []
        stride = dict((n, st) for n, _, st in TABLES)[name]
        return [(i, bytes(seg.data[i * stride:(i + 1) * stride]))
                for i in range(len(seg.data) // stride)]

    # ---------- writing ----------
    def build(self):
        """Re-emit the file, recomputing every header offset/size from the
        actual segment layout. Segment order is preserved."""
        offs = {}
        sizes = {}
        cur = HEADER_SIZE
        body = bytearray()
        for s in self.segments:
            if s.kind == 'header':
                continue
            if s.align > 1 and cur % s.align:
                pad = s.align - (cur % s.align)
                body += b'\x00' * pad
                cur += pad
            if s.kind in ('table', 'buffer'):
                offs[s.name] = cur
                sizes[s.name] = len(s.data)
            body += s.data
            cur += len(s.data)

        filesize = HEADER_SIZE + len(body)
        out = bytearray(HEADER_SIZE)
        out[0:4] = MAGIC
        struct.pack_into('>I', out, 4, self.hash)
        # An EMPTY table still carries a pointer to where it *would* have
        # started: the offset of the next non-empty table in table order, or
        # the vertex buffer if none follows. Verified 898/898 zero-count
        # tables across the corpus, no exceptions.
        oVB_final = offs.get('VertBuffer', 0)
        for i, (name, cnt_field, _) in enumerate(TABLES):
            if self.counts[cnt_field]:
                continue
            nxt = None
            for name2, cf2, _ in TABLES[i + 1:]:
                if self.counts[cf2]:
                    nxt = offs.get(name2)
                    break
            offs[name] = oVB_final if nxt is None else nxt

        vals = [self.counts[n] for n in COUNT_FIELDS]
        for name, cnt_field, _ in TABLES:
            vals.append(offs.get(name, 0))
        vals += [offs.get('VertBuffer', 0), sizes.get('VertBuffer', 0),
                 offs.get('FaceBuffer', 0), sizes.get('FaceBuffer', 0),
                 self._zero, filesize]
        struct.pack_into('>22I', out, 8, *vals)
        return bytes(out + body)


def load(path):
    with open(path, 'rb') as f:
        return Mdn.parse(f.read())


def roundtrip_ok(path):
    with open(path, 'rb') as f:
        orig = f.read()
    return Mdn.parse(orig).build() == orig


# ---------------------------------------------------------------------------
# Structured access + safe editing
# ---------------------------------------------------------------------------
# VertIndex record (80 bytes), one per sub-mesh:
#   +0x00 MeshGroupIndex  +0x04 unknown        +0x08 FaceSectionCount
#   +0x0c FaceSectionStart +0x10 VertId        +0x14 BonePalletId
#   +0x18 VertCount       +0x1c zero
#   +0x20 Max[4]  +0x30 Min[4]  +0x40 Pos[4]   (floats)
#
# Measured facts (whole corpus):
#   * Max/Min are EXACTLY the componentwise max/min of the sub-mesh's raw
#     vertex positions -- 5302/5302 sub-meshes matched at delta == 0.0.
#   * The W component of all three vectors is always 1.0.
#   * Pos xyz is always 0. It is not derived from the box, so leave it be.
VI_STRIDE = 80
VI_VERTCOUNT = 0x18
VI_MAX = 0x20
VI_MIN = 0x30

# VertDef record (48 bytes), one per sub-mesh:
#   +0x00 zero  +0x04 DefCount  +0x08 Size (this sub-mesh's stride)  +0x0c Start
#   +0x10 attribute ids (DefCount bytes)  +0x20 attribute offsets (DefCount bytes)
VD_STRIDE = 48
ATTR_POSITION = 0x10
ATTR_UV = 0x78


class MeshView:
    """Read/write access to one sub-mesh's vertex positions."""

    def __init__(self, mdn, index):
        self.mdn = mdn
        self.index = index
        vd = mdn.segment('VertDef')
        vi = mdn.segment('VertIndex')
        if vd is None or vi is None:
            raise MdnError('file has no VertDef/VertIndex table')
        d = bytes(vd.data[index * VD_STRIDE:(index + 1) * VD_STRIDE])
        self.defcount, self.stride, self.start = struct.unpack_from('>3I', d, 4)
        self.attrs = list(d[0x10:0x10 + self.defcount])
        self.attr_offs = list(d[0x20:0x20 + self.defcount])
        r = bytes(vi.data[index * VI_STRIDE:(index + 1) * VI_STRIDE])
        self.vert_count = struct.unpack_from('>I', r, VI_VERTCOUNT)[0]

    def _pos_off(self):
        if ATTR_POSITION not in self.attrs:
            return None
        return self.attr_offs[self.attrs.index(ATTR_POSITION)]

    def positions(self):
        """Raw (x, y, z) floats as stored in the file, one tuple per vertex."""
        po = self._pos_off()
        if po is None:
            return []
        vb = bytes(self.mdn.segment('VertBuffer').data)
        return [struct.unpack_from('>3f', vb, self.start + v * self.stride + po)
                for v in range(self.vert_count)]

    def set_positions(self, pts):
        """Overwrite positions IN PLACE. Vertex count must not change -- this
        touches only the 12 position bytes per vertex, leaving UVs, normals,
        weights, faces and every table byte-identical. This is the path that
        is proven to work in game (the round-lens Otacon glasses)."""
        if len(pts) != self.vert_count:
            raise MdnError(f'expected {self.vert_count} positions, got {len(pts)}')
        po = self._pos_off()
        if po is None:
            raise MdnError('sub-mesh has no position attribute')
        vb = self.mdn.segment('VertBuffer').data
        for v, (x, y, z) in enumerate(pts):
            struct.pack_into('>3f', vb, self.start + v * self.stride + po, x, y, z)


def mesh_views(mdn):
    return [MeshView(mdn, i) for i in range(mdn.counts['Mesh'])]


def recompute_bboxes(mdn):
    """Rewrite every sub-mesh's declared bounding box from its actual vertices.

    Call this after ANY edit that moves vertices. Geometry that pokes outside
    the box it declares is a prime suspect for a mod that installs cleanly and
    then renders as nothing.

    On an unmodified file this is a no-op and the file still round-trips
    byte-identically -- which is how we know the convention is right.
    Returns the number of sub-meshes whose stored box actually changed.
    """
    vi = mdn.segment('VertIndex')
    if vi is None:
        return 0
    changed = 0
    for i in range(mdn.counts['Mesh']):
        pts = MeshView(mdn, i).positions()
        if not pts:
            continue
        base = i * VI_STRIDE
        mx = tuple(max(p[k] for p in pts) for k in range(3))
        mn = tuple(min(p[k] for p in pts) for k in range(3))
        before = bytes(vi.data[base + VI_MAX:base + VI_MIN + 16])
        struct.pack_into('>4f', vi.data, base + VI_MAX, mx[0], mx[1], mx[2], 1.0)
        struct.pack_into('>4f', vi.data, base + VI_MIN, mn[0], mn[1], mn[2], 1.0)
        if bytes(vi.data[base + VI_MAX:base + VI_MIN + 16]) != before:
            changed += 1
    return changed
