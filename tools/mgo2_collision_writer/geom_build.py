# -*- coding: utf-8 -*-
"""geom_build -- synthesise MGO2 stage COLLISION (.geom section 0) from a mesh.

This is the mesh -> .geom writer: the piece that was missing for building a
map from scratch. Everything it emits was measured, not guessed; the
measurements and their sample sizes are cited inline.

WHAT COLLISION ACTUALLY IS HERE
-------------------------------
The .geom does not store a mesh. It stores a pre-built answer to "what is
under my feet?", because the PS3 cannot test the player against every triangle
60 times a second. Three levels of narrowing down:

  1. GRID   - the map volume is diced into boxes (7000 x 10000 x 7000 units by
              default). Arithmetic alone says which box you are in. Each box
              names the first descriptor it owns.
  2. DESCRIPTORS - a flat directory. Each entry points at one data block and
              says how big it is and what surface it is.
  3. BLOCK  - leaf nodes holding up to 33 faces each, plus a shared vertex
              pool the leaves index into.

Every level stores summaries of the level below and the engine TRUSTS them.
Get one wrong and you do not see a glitch, you fall through the floor.

MEASURED FACTS THIS RELIES ON
-----------------------------
* Grid record = u16 + 8 layer slots (stride 10). Slot 1 = solid geometry,
  slot 2 = trigger volumes, 0xff = layer absent, 0x7fff = empty cell.
  A geometry-only occupied cell is  ff 00 ff ff ff ff ff ff.
* Grid linear index: lin = x + gx*(z + gz*y).
* The DESCRIPTOR TABLE is ordered by cells walked in (x, y, z) lexicographic
  order -- a DIFFERENT walk from the grid's own linear order. 83 of 88 shipped
  sub-worlds disagree between the two, so this is not a detail.
* Descriptor type byte: 0x10 = geometry (more follow), 0x11 = geometry (last
  of list), 0x21 = trigger volume. Bit 0 is an end-of-list marker.
* desc.f6 == 65535, desc.w8 == 0, desc.w14 == 0 in 695,105 / 695,105 blocks.
* desc.block_size == the block's real size in 695,105 / 695,105.
* Leaf node header: byte0 = face count, byte1 = 0 (7,268,005/7,268,005),
  +0x02 class 0x2012, +0x04..0x0f zero, +0x14 = (pool_off - node_off) / 16
  (1,827,518 / 1,827,522 = 100.00%), +0x1c = the descriptor's surface id
  (128,517 / 128,598 single-leaf blocks).
* Face row (8 bytes): 4 low index bytes, then 2 bits of HIGH index each packed
  into byte 4 -- indices are 10-BIT, not 8. A face is a TRIANGLE when
  i3 == i0 or i3 == i2, otherwise a quad.
* Vertex pool: u32 total_rows, u32 lead_rows, u32 1, u32 0, then 16-byte rows.
  row[0] = block origin (world space, and it is the CELL'S MIN CORNER),
  rows[1..lead-1] = unit plane normals, rows[lead..] = vertex positions LOCAL
  to the origin. The 4th float of every row is 0.0 (142,571/142,571).

DELIBERATE NON-GOAL
-------------------
This does not reproduce Konami's exact descriptor split, and it does not need
to. Matching their split byte-for-byte is unsolved (88.74% of cells). Being
VALID is a much lower bar than being IDENTICAL: the engine only walks
grid -> descriptors -> blocks, so any self-consistent split works. We emit one
descriptor per occupied cell and accept some extra cell memberships, which is
the benign direction -- an over-populated cell costs a wasted narrow-phase
test, an under-populated one is a fall-through-the-floor bug.
"""
import struct
import math

MAX_FACES_PER_LEAF = 32          # the count byte allows more; 33 is the shipped max
MAX_VERTS_PER_BLOCK = 1024       # hard ceiling: vertex indices are 10-bit
MAX_NORMALS_PER_BLOCK = 256      # the plane index is a single byte

GRID_STRIDE = 10
EMPTY_CELL = 0x7FFF
LAYER_GEOMETRY = 1               # slot index within the 8 layer slots

DESC_GEOM_LAST = 0x11
DESC_F6 = 0xFFFF
NODE_LEAF = 0x2012
FLOAT_ONE_BITS = 0x3F800000

ZERO = bytes(1)
FF = bytes([0xFF])


def _align(v, a):
    return (v + a - 1) // a * a


def _f32(v):
    """Round a Python float to the float32 the file will actually hold."""
    return struct.unpack('>f', struct.pack('>f', v))[0]


class Face(object):
    """One collision face: 3 or 4 world-space vertices, plus a surface flag."""

    __slots__ = ('verts', 'normal', 'flags')

    def __init__(self, verts, normal=None, flags=0):
        if len(verts) not in (3, 4):
            raise ValueError('a collision face must have 3 or 4 vertices')
        self.verts = [tuple(float(c) for c in v) for v in verts]
        self.normal = tuple(float(c) for c in normal) if normal else self._plane_normal()
        self.flags = flags

    def _plane_normal(self):
        (ax, ay, az), (bx, by, bz), (cx, cy, cz) = self.verts[:3]
        ux, uy, uz = bx - ax, by - ay, bz - az
        vx, vy, vz = cx - ax, cy - ay, cz - az
        nx = uy * vz - uz * vy
        ny = uz * vx - ux * vz
        nz = ux * vy - uy * vx
        length = math.sqrt(nx * nx + ny * ny + nz * nz)
        if length == 0.0:
            return (0.0, 1.0, 0.0)
        return (nx / length, ny / length, nz / length)

    def aabb(self):
        xs = [v[0] for v in self.verts]
        ys = [v[1] for v in self.verts]
        zs = [v[2] for v in self.verts]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


class CollisionWorld(object):
    """Builds one collision world's bytes from a face soup."""

    def __init__(self, faces, cell_size=(7000.0, 10000.0, 7000.0),
                 surface_id=0x00840DC2, obj_hash=0x00E832C9, margin=1.0):
        if not faces:
            raise ValueError('no faces')
        self.faces = list(faces)
        self.cell = tuple(float(c) for c in cell_size)
        self.surface_id = surface_id
        self.obj_hash = obj_hash

        lo = [min(v[k] for f in self.faces for v in f.verts) for k in range(3)]
        hi = [max(v[k] for f in self.faces for v in f.verts) for k in range(3)]
        # world_min is stored as float32, so round it here: cell arithmetic
        # must agree exactly with what a parser reads back out of the file.
        self.world_min = tuple(_f32(lo[k] - margin) for k in range(3))
        self.dims = tuple(
            max(1, int(math.floor((hi[k] - self.world_min[k]) / self.cell[k])) + 1)
            for k in range(3))

    # -- spatial assignment -------------------------------------------------
    def _cells_for(self, face):
        """Every grid cell this face's AABB touches.

        AABB overlap, deliberately: exact triangle-vs-cell SAT was measured
        STRICTLY WORSE on every stage tested (n023a 93.69% vs 99.54%) because
        it invents false negatives -- and a false negative here is a hole in
        the floor.
        """
        lo, hi = face.aabb()
        spans = []
        for k in range(3):
            a = int(math.floor((lo[k] - self.world_min[k]) / self.cell[k]))
            b = int(math.floor((hi[k] - self.world_min[k]) / self.cell[k]))
            a = max(0, min(a, self.dims[k] - 1))
            b = max(0, min(b, self.dims[k] - 1))
            spans.append(range(a, b + 1))
        return [(x, y, z) for x in spans[0] for y in spans[1] for z in spans[2]]

    def _assign(self):
        buckets = {}
        for f in self.faces:
            for c in self._cells_for(f):
                buckets.setdefault(c, []).append(f)
        return buckets

    # -- block emission -----------------------------------------------------
    def _build_block(self, cell, faces):
        """Bytes for one block: leaf nodes, then the shared vertex pool."""
        origin = tuple(_f32(self.world_min[k] + cell[k] * self.cell[k])
                       for k in range(3))

        verts, vmap, normals, nmap, rows = [], {}, [], {}, []
        for f in faces:
            key = tuple(round(c, 6) for c in f.normal)
            if key not in nmap:
                nmap[key] = len(normals)
                normals.append(f.normal)
            idx = []
            for v in f.verts:
                if v not in vmap:
                    vmap[v] = len(verts)
                    verts.append(v)
                idx.append(vmap[v])
            if len(idx) == 3:
                idx.append(idx[0])           # triangle marker: i3 == i0
            rows.append((idx, nmap[key], f.flags))

        if len(verts) > MAX_VERTS_PER_BLOCK or len(normals) > MAX_NORMALS_PER_BLOCK:
            return None                      # caller must use a smaller cell

        chunks = [rows[i:i + MAX_FACES_PER_LEAF]
                  for i in range(0, len(rows), MAX_FACES_PER_LEAF)]
        node_sizes = [32 + _align(len(c) * 8, 16) for c in chunks]
        pool_rel = sum(node_sizes)

        out = bytearray()
        node_off = 0
        for ci, chunk in enumerate(chunks):
            body = bytearray()
            for idx, pidx, flags in chunk:
                high = (((idx[0] >> 8) & 3)
                        | (((idx[1] >> 8) & 3) << 2)
                        | (((idx[2] >> 8) & 3) << 4)
                        | (((idx[3] >> 8) & 3) << 6))
                body += struct.pack('>BBBBBBH',
                                    idx[0] & 0xFF, idx[1] & 0xFF,
                                    idx[2] & 0xFF, idx[3] & 0xFF,
                                    high, pidx, flags)
            body += ZERO * (_align(len(body), 16) - len(body))

            hdr = bytearray(32)
            hdr[0] = len(chunk)
            hdr[1] = 0
            struct.pack_into('>H', hdr, 2, NODE_LEAF)
            struct.pack_into('>I', hdr, 0x10, self.obj_hash)
            struct.pack_into('>I', hdr, 0x14, (pool_rel - node_off) // 16)
            struct.pack_into('>I', hdr, 0x18, 1)
            struct.pack_into('>I', hdr, 0x1C, self.surface_id)
            out += bytes(hdr) + bytes(body)
            node_off += node_sizes[ci]

        total_rows = 1 + len(normals) + len(verts)
        pool = bytearray(struct.pack('>4I', total_rows, 1 + len(normals), 1, 0))
        pool += struct.pack('>4f', origin[0], origin[1], origin[2], 0.0)
        for n in normals:
            pool += struct.pack('>4f', n[0], n[1], n[2], 0.0)
        for v in verts:
            pool += struct.pack('>4f', v[0] - origin[0], v[1] - origin[1],
                                v[2] - origin[2], 0.0)
        return bytes(out) + bytes(pool), pool_rel

    # -- the world ----------------------------------------------------------
    def emit(self, base_off, trailer):
        """Return (payload_bytes, nblocks, ncells) to be placed at base_off."""
        buckets = self._assign()
        # DESCRIPTOR ORDER: cells in (x, y, z) lexicographic order, NOT the
        # grid's linear order. Measured: under the linear order the shipped
        # heads are monotone in only 5 of 88 sub-worlds.
        cells = sorted(buckets)

        blocks = []
        for cell in cells:
            built = self._build_block(cell, buckets[cell])
            if built is None:
                raise ValueError('cell %s exceeds the 1024-vertex / 256-normal '
                                 'block limit; use a smaller cell_size' % (cell,))
            blocks.append((cell, built[0], built[1]))

        gx, gy, gz = self.dims
        ncells = gx * gy * gz
        grid_off = base_off + 0x40
        # The parser REQUIRES the descriptor table to end exactly where the
        # first block begins -- there is no room to pad between them. Blocks
        # must also start 16-aligned so their vertex-pool rows stay aligned.
        # Aligning desc_off to 16 satisfies both at once, because a descriptor
        # is 32 bytes and 32 is a multiple of 16. (Shipped files agree: n007a
        # has grid end 0xc78 -> desc_off 0xc80 -> first block 0x2aa0.)
        desc_off = _align(grid_off + ncells * GRID_STRIDE, 16)
        first_block = desc_off + len(blocks) * 32

        descs = []
        head_of = {}
        cur = first_block
        for i, (cell, data, pool_rel) in enumerate(blocks):
            head_of[cell] = i
            descs.append(dict(size=len(data), pool=cur + pool_rel, block=cur))
            cur += _align(len(data), 16)
        trailer_off = _align(cur, 16)

        desc_bytes = bytearray()
        for d in descs:
            desc_bytes += struct.pack('>BBHHHIIIIII',
                                      DESC_GEOM_LAST, 1, d['size'], 0, DESC_F6,
                                      0, d['pool'], d['block'], 0, 1,
                                      self.surface_id)
        if desc_off + len(desc_bytes) != first_block:
            raise AssertionError('descriptor table must end exactly at the '
                                 'first block (0x%x vs 0x%x)'
                                 % (desc_off + len(desc_bytes), first_block))

        grid = bytearray()
        for lin in range(ncells):
            x = lin % gx
            y = (lin // gx) // gz
            z = (lin // gx) % gz
            head = head_of.get((x, y, z))
            if head is None:
                grid += struct.pack('>H', EMPTY_CELL) + FF * 8
            else:
                slots = bytearray(FF * 8)
                slots[LAYER_GEOMETRY] = 0x00
                grid += struct.pack('>H', head) + bytes(slots)

        desc_end = (desc_off + len(desc_bytes)) - grid_off
        sub = (struct.pack('>3fI', self.world_min[0], self.world_min[1],
                           self.world_min[2], trailer_off)
               + struct.pack('>3fI', self.cell[0], self.cell[1], self.cell[2],
                             FLOAT_ONE_BITS)
               + struct.pack('>3II', gx, gy, gz, desc_end)
               + struct.pack('>IHHII', 1, 8, GRID_STRIDE, grid_off, desc_off))
        if len(sub) != 0x40:
            raise AssertionError('sub-header must be 0x40 bytes')

        buf = bytearray()
        buf += sub
        buf += grid
        buf += ZERO * (desc_off - grid_off - len(grid))
        buf += desc_bytes
        for i, (cell, data, pool_rel) in enumerate(blocks):
            buf += ZERO * ((descs[i]['block'] - base_off) - len(buf))
            buf += data
        buf += ZERO * ((trailer_off - base_off) - len(buf))
        buf += trailer
        return bytes(buf), len(blocks), ncells


def box_faces(cx, cy, cz, sx, sy, sz, flags=0):
    """Six outward-facing quads forming an axis-aligned box."""
    x0, x1 = cx - sx / 2.0, cx + sx / 2.0
    y0, y1 = cy - sy / 2.0, cy + sy / 2.0
    z0, z1 = cz - sz / 2.0, cz + sz / 2.0
    quads = [
        ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0.0, 1.0, 0.0)),
        ([(x0, y0, z1), (x1, y0, z1), (x1, y0, z0), (x0, y0, z0)], (0.0, -1.0, 0.0)),
        ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0.0, 0.0, -1.0)),
        ([(x1, y0, z1), (x0, y0, z1), (x0, y1, z1), (x1, y1, z1)], (0.0, 0.0, 1.0)),
        ([(x0, y0, z1), (x0, y0, z0), (x0, y1, z0), (x0, y1, z1)], (-1.0, 0.0, 0.0)),
        ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1.0, 0.0, 0.0)),
    ]
    return [Face(v, n, flags) for v, n in quads]
