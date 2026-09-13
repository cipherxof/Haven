# -*- coding: utf-8 -*-
"""bp_reach.py -- BROADPHASE REACHABILITY GATE for MGO2 .geom collision.

Decoded/measured 2026-09-04.

WHY THIS EXISTS
---------------
A generated .geom can have correct geometry, a correct radix encoding, correct
descriptor chains and still give NO FLOOR, because none of those checks ask the
one question the engine actually asks:

    standing in grid cell C, can the engine reach the triangle under my feet?

The engine's narrow-phase is only ever handed the blocks it found by walking
grid cell -> layer chain -> descriptor -> block.  A face stored in a block that
cell C cannot reach is invisible collision.  This tool walks exactly that path
and scores a file.

MEASURED BASELINE (26 shipped MGO2 stage geoms, all worlds)
    shipped maps        96.94% .. 100.00%   (n002a Ambush Alley is 100.00%)
    our generated maps  58.35% and 66.98%
The residual few % on shipped maps is at least partly this tool's own
approximation: membership is computed from the face AABB, so a face whose AABB
clips a cell its actual polygon misses is counted as a miss.  Treat >=96% as
"shipped-like" and anything below ~90% as broken.

FORMAT NOTES DECODED HERE (all verified against the shipped corpus)
    descriptor.type byte  =  (grid layer << 4) | end_of_chain_bit
        high nibble 1 = geometry layer, 2 = volumes layer.  Verified exactly:
        stock n002a has 985 layer-1 heads for the 985 cells whose grid slot 1
        is present, and 330 layer-2 blocks for the 330 cells with slot 2.
        Low bit 1 terminates the chain: walking cell.rest[k] as a delta from
        the cell's u16 head and stopping on (type & 1) covers 1534/1534
        descriptors in n002a with zero runaways (416/416 and 1110/1110 on our
        two builds).
    grid cell record      =  u16 head + 8 layer slot bytes (stride 10)
        slot byte = unsigned delta from head to that layer's first descriptor,
        0xff = layer absent.  This is why a delta can overflow at 254.
    A block is owned by exactly ONE cell (1204/1204 in n002a).  Faces may spill
    into neighbours, but a face lying ENTIRELY outside its owner cell is lost.

USAGE
    python bp_reach.py <file.geom> [more.geom ...]
    python bp_reach.py --delta <file.geom>     # show the (di,dj,dk) misfile skew
"""
import os
import sys, os, math
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import geom_io as G


def faces_of(b):
    """World-space faces [(p0,p1,p2,p3)] of one block, [] if it has no pool."""
    d = b._buf
    limit = b._pool_rel
    if limit is None:
        return []
    verts = b.vertices()
    out, o = [], 0
    while o + 32 <= limit:
        cls = G._u16(d, o + 2)
        cnt = d[o]
        if cls == 0x2012:                       # POLY leaf
            for j in range(cnt):
                f = o + 32 + j * 8
                hi = d[f + 4]                   # 10-bit indices: 2 high bits each
                idx = (d[f] + 256 * (hi & 3),
                       d[f + 1] + 256 * ((hi >> 2) & 3),
                       d[f + 2] + 256 * ((hi >> 4) & 3),
                       d[f + 3] + 256 * ((hi >> 6) & 3))
                if max(idx) < len(verts):
                    out.append(tuple(verts[i] for i in idx))
            o += 32 + ((cnt * 8 + 15) // 16) * 16
        elif cls in G.NODE_FIXED_SIZE:
            o += G.NODE_FIXED_SIZE[cls]
        else:
            return out
    return out


def _reach(world):
    """cell index -> set(descriptor index) the engine can reach from it."""
    ds, cells = world.descriptors.records, world.grid.records
    reach = defaultdict(set)
    owner = defaultdict(set)
    for ci, c in enumerate(cells):
        if c.empty:
            continue
        for layer, delta in enumerate(c.rest):
            if delta == 0xff:
                continue
            j = c.index + delta
            while j < len(ds):
                reach[ci].add(j)
                owner[j].add(ci)
                if ds[j].type & 1:
                    break
                j += 1
    return reach, owner


def _cells_of(face, sub):
    gx, gy, gz = sub.dims
    mnx, mny, mnz = sub.world_min
    cx, cy, cz = sub.cell_size
    xs = [p[0] for p in face]; ys = [p[1] for p in face]; zs = [p[2] for p in face]

    def rg(lo, hi, mn, cs, n):
        a = int(math.floor((lo - mn) / cs)); b = int(math.floor((hi - mn) / cs))
        return range(max(a, 0), min(b, n - 1) + 1)

    out = []
    for i in rg(min(xs), max(xs), mnx, cx, gx):
        for j in rg(min(ys), max(ys), mny, cy, gy):
            for k in rg(min(zs), max(zs), mnz, cz, gz):
                out.append((i, j, k, i + gx * (k + gz * j)))
    return out


def score(path, show_delta=False):
    g = G.GeomFile.load(path)
    tot = ok = 0
    delta = Counter()
    for w in g.payload.worlds:
        sub = w.sub
        if not all(sub.cell_size):
            continue
        reach, owner = _reach(w)
        ds = w.descriptors.records
        di = {id(d): n for n, d in enumerate(ds)}
        for blk in w.blocks:
            if (blk.desc.type >> 4) != 1:
                continue
            idx = di.get(id(blk.desc))
            if idx is None:
                continue
            for f in faces_of(blk):
                cl = _cells_of(f, sub)
                if not cl:
                    continue
                tot += 1
                if any(idx in reach.get(c[3], ()) for c in cl):
                    ok += 1
                elif show_delta and owner.get(idx):
                    gx, gy, gz = sub.dims
                    best = None
                    for oc in owner[idx]:
                        oj, r = divmod(oc, gx * gz); ok_, oi = divmod(r, gx)
                        for i, j, k, _ in cl:
                            d3 = (oi - i, oj - j, ok_ - k)
                            if best is None or sum(map(abs, d3)) < sum(map(abs, best)):
                                best = d3
                    delta[best] += 1
    return tot, ok, delta


def main(argv):
    show = '--delta' in argv
    paths = [a for a in argv[1:] if not a.startswith('--')]
    for p in paths:
        try:
            tot, ok, delta = score(p, show)
        except Exception as e:
            print('%-32s PARSE FAIL: %s' % (os.path.basename(p), e))
            continue
        pct = 100.0 * ok / max(tot, 1)
        verdict = 'shipped-like' if pct >= 96 else ('SUSPECT' if pct >= 90 else 'BROKEN')
        print('%-32s faces=%-7d reachable=%-7d %6.2f%%  %s'
              % (os.path.basename(p), tot, ok, pct, verdict))
        if show and delta:
            print('   nearest owner-cell skew (di,dj,dk) for unreachable faces:')
            for k, v in delta.most_common(8):
                print('      %-14s %d' % (str(k), v))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
