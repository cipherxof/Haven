# -*- coding: utf-8 -*-
"""geom_build_chain -- CollisionWorld that CHAINS several blocks per grid cell.

WHY
---
`geom_build.CollisionWorld` emits exactly one block per occupied cell, and a
block is hard-capped at 1024 vertices (indices are 10-bit) and 256 normals (the
plane index is one byte).  On a real map that overflows immediately:

    heliport, 82,194 faces, cell 7000x10000x7000 -> cell (1,0,1) overflows
    ...and it still overflows at 1/2 and 1/4 cell size.

Shrinking cells is not the answer -- every shipped MGO2 map uses
7000 x 10000 x 7000.  Stock's answer is to CHAIN: n002a has 1,204 geometry
blocks for 985 cells with geometry, i.e. 219 continuation blocks.

THE CHAIN FORMAT (decoded 2026-09-04, verified 1534/1534 descriptors in n002a,
416/416 and 1110/1110 on our own builds, zero runaways)

    descriptor.type  =  (grid layer << 4) | end_of_chain_bit
        high nibble 1 = solid geometry, 2 = trigger volumes
        bit 0 set     = last descriptor of this cell's chain

    grid cell record =  u16 head + 8 layer slot bytes (stride 10)
        slot[k] = unsigned delta from `head` to layer k's first descriptor,
                  0xff = layer absent.  (This is why a delta caps at 254.)

    A cell's chain is therefore a CONSECUTIVE run of descriptors starting at
    head+slot[k], walked forward until one has bit 0 set.

    descriptor.b1  =  the block's NODE COUNT (1,533 / 1,533 stock blocks,
        100.00%).  geom_build.py hardcodes 1, which is correct only for a
        single-leaf block; a chained build is full of multi-leaf blocks, so
        this module emits the real count.

So a cell with N blocks emits N consecutive descriptors: 0x10 for the first
N-1, 0x11 for the last.  Everything else is inherited from geom_build.

INVARIANT THIS PRESERVES (measured 11,223/11,223 shipped blocks, 100.00%)
    a block's vertex-pool row 0 (its origin) is EXACTLY its cell's min corner.
That is what geom_build already does and what Haven's ImportAsNew does not; it
is the reason our Haven builds score 58-67% on `mapbuild/bp_reach.py` while
shipped maps score 96.9-100%.

This module deliberately does NOT modify geom_build.py, which is validated and
should stay that way.  Score the output with:

    python D:/mgo2work/mapbuild/bp_reach.py <file.geom>
"""
import math
import struct

from geom_build import (CollisionWorld, _align, GRID_STRIDE, EMPTY_CELL,
                        LAYER_GEOMETRY, DESC_F6, MAX_VERTS_PER_BLOCK,
                        MAX_NORMALS_PER_BLOCK, MAX_FACES_PER_LEAF, ZERO, FF)

DESC_GEOM_MORE = 0x10        # geometry layer, chain continues
DESC_GEOM_LAST = 0x11        # geometry layer, last of this cell's chain

# A block with no GEO_FIELD tree is legal -- 2,176 of 9,598 shipped geometry
# blocks (22.7%) have zero FIELD nodes, and 370 of n002a's blocks begin with a
# bare 0x2012 POLY.  But shipped FIELD-less blocks never exceed NINE leaves
# (counts 0-9, and 5,663 of 6,070 are a single leaf); every denser stock block
# carries a FIELD tree.  We emit no FIELD tree, so we stay inside that envelope.
MAX_LEAVES_PER_FIELDLESS_BLOCK = 9


def count_nodes(data, limit):
    """Number of nodes in a block, walked with the shipped size rule.

    desc.b1 is the NODE count (1,203/1,203 on stock n002a). Counting leaves is
    wrong as soon as blocks carry a FIELD tree, because every POLY gains a
    FIELD parent and the real count doubles -- writing the leaf count made the
    engine walk half the block and trip an SPU assert (write to 0xffdead00).
    """
    FIXED = {0x2004: 0x40, 0x2005: 0x90, 0x2023: 0x90, 0x2024: 0x50}
    n, o = 0, 0
    while o + 32 <= limit:
        cls = struct.unpack_from('>H', data, o + 2)[0]
        if cls == 0x2012:
            sz = 32 + ((data[o] * 8 + 15) // 16) * 16
        elif cls in FIXED:
            sz = FIXED[cls]
        else:
            break
        n += 1
        o += sz
    return n


def last_root_offset16(data):
    """desc.f4 = byte offset of the LAST ROOT-LEVEL node, in 0x10 units.

    Root nodes are those reachable from node 0 by following Next (node +0x04,
    a SIGNED offset in 0x10-byte units). MEASURED on shipped n002a:
    1,533 / 1,533 descriptors match exactly. Emitting 0 (as this writer did
    originally) declares a single root, so the engine only ever walks the
    first node of the block and ignores every sibling after it.
    """
    cur = 0
    last = 0
    for _ in range(4096):
        nxt = struct.unpack_from('>i', data, cur + 4)[0]
        if nxt == 0:
            break
        cur = cur + nxt * 16
        if cur < 0 or cur + 32 > len(data):
            break
        last = cur
    return last // 16


class ChainedCollisionWorld(CollisionWorld):
    """CollisionWorld that splits an over-full cell into a chain of blocks.

    `single_owner=True` stores each face ONCE, in the cell containing its
    centroid, instead of replicating it into every cell its AABB touches.

    That is what stock does.  Measured on shipped n002a: every face is
    reachable from at least one of its cells (132,043/132,043) but only 65% of
    faces are present in ALL the cells they overlap -- so the engine cannot be
    querying a single cell, and replication buys nothing it needs.  The
    centroid always lies inside the face's own AABB, so its cell is always one
    the face genuinely overlaps, which is the invariant that matters.

    Replication (the default, inherited behaviour) costs +10.8% file size on a
    dense map: rebuilding stock AA gives 6,771,968 bytes against stock's
    6,113,472, 1,436 blocks against 1,204, 199,517 face instances against
    132,043.  dl/p geoms must match a plaintext size recorded in o/dl/.p
    (DLT2), so on a dense map that overhead is not affordable.
    """

    def __init__(self, *a, **kw):
        self.single_owner = kw.pop('single_owner', False)
        CollisionWorld.__init__(self, *a, **kw)

    def _assign(self):
        if self.single_owner == 'ground':
            # Ground-aware assignment. MEASURED on shipped n002a: the engine
            # finds the floor in the cell you are STANDING IN -- stock scores
            # 204/250 with an exact-cell lookup and only 205/250 searching the
            # whole 3x3 neighbourhood, so it does not rely on neighbour search.
            # Registering a face once (its centroid cell) leaves 12% of the map
            # with no ground in your own cell, which is where you fall through.
            #
            # But full replication costs 6.59 MB against a 5.91 MB ceiling.
            # The resolution: only faces that can BE ground need to be in every
            # cell they cover. A near-vertical wall never answers "what is under
            # me", so it keeps a single owner. Y replication is dropped for the
            # same reason -- stock gains 1/250 from searching the Y column.
            buckets = {}
            for f in self.faces:
                horiz = abs(f.normal[1]) > 0.5
                cells = self._cells_for(f)
                if horiz:
                    seen = set()
                    for (x, y, z) in cells:
                        if (x, z) in seen:
                            continue
                        seen.add((x, z))
                        buckets.setdefault((x, y, z), []).append(f)
                else:
                    lo, hi = f.aabb()
                    c = tuple(
                        max(0, min(self.dims[k] - 1,
                                   int(math.floor(((lo[k] + hi[k]) * 0.5
                                                   - self.world_min[k]) / self.cell[k]))))
                        for k in range(3))
                    buckets.setdefault(c, []).append(f)
            return buckets
        if not self.single_owner:
            return CollisionWorld._assign(self)
        buckets = {}
        for f in self.faces:
            lo, hi = f.aabb()
            cell = tuple(
                max(0, min(self.dims[k] - 1,
                           int(math.floor(((lo[k] + hi[k]) * 0.5
                                           - self.world_min[k]) / self.cell[k]))))
                for k in range(3))
            buckets.setdefault(cell, []).append(f)
        return buckets

    def _split(self, faces):
        """Greedily cut `faces` into groups that each fit one block.

        Three caps apply: the 1024-vertex and 256-normal block limits, and a
        288-face (9-leaf) cap so a FIELD-less block stays inside the shipped
        envelope.  Counting is done exactly the way _build_block counts:
        distinct vertex tuples and distinct rounded normals.  A single face that cannot fit
        alone is impossible (4 verts, 1 normal), so this always terminates.
        """
        # ARENA LIMIT. Stock's largest block is exactly 0x3B70 bytes and a
        # build sitting on 0x3C00 crashed, so the block's TOTAL emitted size is
        # the real constraint -- not the leaf count. Note 1024 verts is 16,384
        # bytes of pool on its own, already past the limit, so the 10-bit vertex
        # cap can never be the binding one. Budget conservatively: each face
        # costs 8 bytes of row plus up to 4 new 16-byte pool rows, and each
        # leaf adds a 32-byte header (plus 0x40 when it carries a FIELD).
        ARENA = 0x3B70
        cap = MAX_LEAVES_PER_FIELDLESS_BLOCK * MAX_FACES_PER_LEAF
        groups, cur = [], []
        vseen, nseen = set(), set()
        for f in faces:
            nkey = tuple(round(c, 6) for c in f.normal)
            new_v = sum(1 for v in f.verts if v not in vseen)
            new_n = 0 if nkey in nseen else 1
            leaves = -(-(len(cur) + 1) // MAX_FACES_PER_LEAF)
            est = (leaves * (32 + 0x40)                       # node headers
                   + (len(cur) + 1) * 8                       # face rows
                   + 16 * (1 + len(nseen) + new_n
                           + len(vseen) + new_v))             # vertex pool
            if cur and (len(cur) >= cap or est > ARENA or
                        len(vseen) + new_v > MAX_VERTS_PER_BLOCK or
                        len(nseen) + new_n > MAX_NORMALS_PER_BLOCK):
                groups.append(cur)
                cur, vseen, nseen = [], set(), set()
                new_v = len(set(f.verts))
                new_n = 1
            cur.append(f)
            vseen.update(f.verts)
            nseen.add(nkey)
        if cur:
            groups.append(cur)
        return groups

    def emit(self, base_off, trailer):
        """(payload_bytes, nblocks, ncells).  Same contract as the base class."""
        buckets = self._assign()
        # Cells in (x, y, z) LEXICOGRAPHIC order -- not the grid's linear
        # order.  Under the linear order the shipped descriptor heads are
        # monotone in only 5 of 88 sub-worlds.
        cells = sorted(buckets)

        blocks = []                       # [(cell, data, pool_rel, ends_chain)]
        for cell in cells:
            groups = self._split(buckets[cell])
            for i, grp in enumerate(groups):
                built = self._build_block(cell, grp)
                if built is None:
                    raise ValueError(
                        'cell %s still overflows after splitting into %d '
                        'blocks (group %d has %d faces)'
                        % (cell, len(groups), i, len(grp)))
                nleaves = max(1, -(-len(grp) // MAX_FACES_PER_LEAF))
                blocks.append((cell, built[0], built[1],
                               i == len(groups) - 1, nleaves))

        gx, gy, gz = self.dims
        ncells = gx * gy * gz
        grid_off = base_off + 0x40
        desc_off = _align(grid_off + ncells * GRID_STRIDE, 16)
        first_block = desc_off + len(blocks) * 32

        descs, head_of = [], {}
        cur = first_block
        for i, (cell, data, pool_rel, last, nleaves) in enumerate(blocks):
            head_of.setdefault(cell, i)
            descs.append(dict(size=len(data), pool=cur + pool_rel, block=cur,
                              type=DESC_GEOM_LAST if last else DESC_GEOM_MORE,
                              nodes=count_nodes(data, pool_rel),
                              f4=last_root_offset16(data[:pool_rel])))
            cur += _align(len(data), 16)
        trailer_off = _align(cur, 16)

        desc_bytes = bytearray()
        for d in descs:
            desc_bytes += struct.pack('>BBHHHIIIIII',
                                      d['type'], min(d['nodes'], 255),
                                      d['size'], d['f4'], DESC_F6,
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
                slots[LAYER_GEOMETRY] = 0x00      # chain starts at head itself
                grid += struct.pack('>H', head) + bytes(slots)

        desc_end = (desc_off + len(desc_bytes)) - grid_off
        sub = (struct.pack('>3fI', self.world_min[0], self.world_min[1],
                           self.world_min[2], trailer_off)
               + struct.pack('>3fI', self.cell[0], self.cell[1], self.cell[2],
                             0x3F800000)
               + struct.pack('>3II', gx, gy, gz, desc_end)
               + struct.pack('>IHHII', 1, 8, GRID_STRIDE, grid_off, desc_off))
        if len(sub) != 0x40:
            raise AssertionError('sub-header must be 0x40 bytes')

        buf = bytearray()
        buf += sub
        buf += grid
        buf += ZERO * (desc_off - grid_off - len(grid))
        buf += desc_bytes
        for i, (cell, data, pool_rel, last, nleaves) in enumerate(blocks):
            buf += ZERO * ((descs[i]['block'] - base_off) - len(buf))
            buf += data
        buf += ZERO * ((trailer_off - base_off) - len(buf))
        buf += trailer
        return bytes(buf), len(blocks), ncells
