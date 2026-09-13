# -*- coding: utf-8 -*-
"""cellfirst_fields -- AttrChainedWorld that emits a FIELD chain per block.

WHY (measured 2026-09-12 on stock dl/p n002a, and in game the same evening):
the engine walks a block's nodes by LINKS, not by size.  A block with N POLY
leaves and no links exposes only leaf 0 -- the player spawned on a face that
happened to be in leaf 0 and fell through as soon as they stepped onto a face
in leaf 1..8.  Stock's FIELD-less blocks are single-leaf (360/362); every
multi-leaf stock block is a chain of FIELD nodes, each FIELD parenting one POLY:

    FIELD k : Length 1, type 0x04, cls 0x2004, next -> FIELD k+1 (+units of
              0x10, 0 on the last), prev -> FIELD k-1 (negative, 0 on the
              first), child +4 (the POLY right after it), name 0, f14 0,
              attr = the POLY's u64 attribute.
              body: size = half-extent + 100.0 (3 f32), i16 0, u16 0,
                    world = AABB centre (3 f32), u32 0.
    POLY k  : as before (next/prev/child 0, f14 = (pool - node) / 16).

desc.b1 = node count (FIELDs + POLYs), desc.f4 = offset/16 of the last FIELD
(last root reached by following Next from node 0) -- both already computed
generically by geom_build_chain.count_nodes / last_root_offset16.
"""
import struct
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # all modules live side by side

from geom_build import (_align, MAX_VERTS_PER_BLOCK, MAX_NORMALS_PER_BLOCK,   # noqa: E402
                        MAX_FACES_PER_LEAF, NODE_LEAF, ZERO, _f32)
from cellfirst_build import AttrChainedWorld                                  # noqa: E402

NODE_FIELD = 0x2004
FIELD_SIZE = 0x40
FIELD_PAD = 100.0
MAX_LEAVES_PER_FIELD_BLOCK = 32       # stock max observed: 32 leaves / 60 nodes
ARENA = 0x3B70


class FieldChainedWorld(AttrChainedWorld):
    """Cell-first world whose blocks carry one FIELD per POLY leaf, chained."""

    def _assign(self):
        """'ground' mode, corrected 2026-09-12: floor-ish faces (|ny| > 0.5)
        go into EVERY cell their AABB overlaps -- including every Y cell, not
        just the lowest.  geom_build_chain's ground mode deduped by (x, z) and
        kept only the lowest Y cell; in game that gave floors just under a Y
        boundary that bullets hit (ray walks the cells) but the player fell
        through (ground probe asks the cell the player's origin is in, which
        is the cell ABOVE).  Stock stores every floor face in each Y cell it
        overlaps (62,565 / 62,565 own-cell on dl/p n002a).  Walls keep a
        single owner (centroid cell) for size."""
        if self.single_owner != 'ground':
            return AttrChainedWorld._assign(self)
        import math
        buckets = {}
        for f in self.faces:
            if abs(f.normal[1]) > 0.5:
                for c in self._cells_for(f):
                    buckets.setdefault(tuple(c), []).append(f)
            else:
                lo, hi = f.aabb()
                c = tuple(max(0, min(self.dims[k] - 1,
                                     int(math.floor(((lo[k] + hi[k]) * 0.5
                                                     - self.world_min[k]) / self.cell[k]))))
                          for k in range(3))
                buckets.setdefault(c, []).append(f)
        return buckets

    def _split(self, faces):
        faces = sorted(faces, key=self._leaf_key)
        groups, cur = [], []
        vseen, nseen = set(), set()
        leaves, leaf_fill, leaf_key = 0, 0, None
        for f in faces:
            k = self._leaf_key(f)
            nkey = tuple(round(c, 6) for c in f.normal)
            new_v = sum(1 for v in set(f.verts) if v not in vseen)
            new_n = 0 if nkey in nseen else 1
            new_leaf = (not cur) or k != leaf_key or leaf_fill >= MAX_FACES_PER_LEAF
            nleaves = leaves + (1 if new_leaf else 0)
            est = (nleaves * (32 + FIELD_SIZE)
                   + (len(cur) + 1) * 8 + 16 * nleaves        # leaf row padding
                   + 16 * (1 + len(nseen) + new_n + len(vseen) + new_v))
            if cur and (nleaves > MAX_LEAVES_PER_FIELD_BLOCK or est > ARENA or
                        len(vseen) + new_v > MAX_VERTS_PER_BLOCK or
                        len(nseen) + new_n > MAX_NORMALS_PER_BLOCK):
                groups.append(cur)
                cur, vseen, nseen = [], set(), set()
                leaves, leaf_fill, leaf_key = 0, 0, None
                new_leaf = True
                nleaves = 1
            if new_leaf:
                leaves = nleaves
                leaf_fill = 0
                leaf_key = k
            cur.append(f)
            leaf_fill += 1
            vseen.update(f.verts)
            nseen.add(nkey)
        if cur:
            groups.append(cur)
        return groups

    def _build_block(self, cell, faces):
        origin = tuple(_f32(self.world_min[k] + cell[k] * self.cell[k]) for k in range(3))
        verts, vmap, normals, nmap = [], {}, [], {}
        leaves = []
        for run in self._leaf_runs(faces):
            rows, pts = [], []
            for f in run:
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
                    pts.append(v)
                if len(idx) == 3:
                    idx.append(idx[0])
                rows.append((idx, nmap[key], f.flags))
            lo = [min(p[k] for p in pts) for k in range(3)]
            hi = [max(p[k] for p in pts) for k in range(3)]
            leaves.append((self._leaf_key(run[0]), rows, lo, hi))
        if len(verts) > MAX_VERTS_PER_BLOCK or len(normals) > MAX_NORMALS_PER_BLOCK:
            return None

        poly_sizes = [32 + _align(len(r) * 8, 16) for _, r, _, _ in leaves]
        node_offs = []                       # (field_off, poly_off) per leaf
        o = 0
        for ps in poly_sizes:
            node_offs.append((o, o + FIELD_SIZE))
            o += FIELD_SIZE + ps
        pool_rel = o

        out = bytearray()
        n = len(leaves)
        for li, ((attr, name), rows, lo, hi) in enumerate(leaves):
            fo, po = node_offs[li]
            nxt = (node_offs[li + 1][0] - fo) // 16 if li + 1 < n else 0
            prv = (node_offs[li - 1][0] - fo) // 16 if li > 0 else 0
            fh = bytearray(32)
            fh[0] = 1
            fh[1] = 0x04
            struct.pack_into('>H', fh, 2, NODE_FIELD)
            struct.pack_into('>iii', fh, 4, nxt, prv, FIELD_SIZE // 16)
            struct.pack_into('>I', fh, 0x18, (attr >> 32) & 0xFFFFFFFF)
            struct.pack_into('>I', fh, 0x1C, attr & 0xFFFFFFFF)
            ctr = [(lo[k] + hi[k]) / 2.0 for k in range(3)]
            half = [(hi[k] - lo[k]) / 2.0 + FIELD_PAD for k in range(3)]
            fb = struct.pack('>3f', *half) + struct.pack('>hH', 0, 0) + struct.pack('>3f', *ctr) + struct.pack('>I', 0)
            out += bytes(fh) + fb

            body = bytearray()
            for idx, pidx, flags in rows:
                high = (((idx[0] >> 8) & 3) | (((idx[1] >> 8) & 3) << 2)
                        | (((idx[2] >> 8) & 3) << 4) | (((idx[3] >> 8) & 3) << 6))
                body += struct.pack('>BBBBBBH', idx[0] & 0xFF, idx[1] & 0xFF,
                                    idx[2] & 0xFF, idx[3] & 0xFF, high, pidx, flags & 0xFFFF)
            body += ZERO * (_align(len(body), 16) - len(body))
            hdr = bytearray(32)
            hdr[0] = len(rows)
            struct.pack_into('>H', hdr, 2, NODE_LEAF)
            struct.pack_into('>I', hdr, 0x10, name & 0xFFFFFFFF)
            struct.pack_into('>I', hdr, 0x14, (pool_rel - po) // 16)
            struct.pack_into('>I', hdr, 0x18, (attr >> 32) & 0xFFFFFFFF)
            struct.pack_into('>I', hdr, 0x1C, attr & 0xFFFFFFFF)
            out += bytes(hdr) + bytes(body)
        assert len(out) == pool_rel

        total_rows = 1 + len(normals) + len(verts)
        pool = bytearray(struct.pack('>4I', total_rows, 1 + len(normals), 1, 0))
        pool += struct.pack('>4f', origin[0], origin[1], origin[2], 0.0)
        for nn in normals:
            pool += struct.pack('>4f', nn[0], nn[1], nn[2], 0.0)
        for v in verts:
            pool += struct.pack('>4f', v[0] - origin[0], v[1] - origin[1], v[2] - origin[2], 0.0)
        chash = leaves[0][0][0] & 0xFFFFFFFF
        return bytes(out) + bytes(pool), pool_rel, chash, len(leaves)


# ------------------------------------------------------------- link gate ---
FIXED = {0x2004: 0x40, 0x2005: 0x90, 0x2023: 0x90, 0x2024: 0x50}


def _node_size(d, o):
    cls = struct.unpack_from('>H', d, o + 2)[0]
    if cls == NODE_LEAF:
        return 32 + _align(d[o] * 8, 16)
    return FIXED.get(cls)


def link_reach(block_buf, pool_rel):
    """(polys_total, polys_reached): POLY leaves reachable from node 0 by links.

    Follows Next and Child (signed 0x10-unit offsets, relative to the node)
    the way the engine does, starting at node 0.  A writer that leaves links
    at zero passes every size-walk check and still hides every leaf but the
    first -- this is the gate that catches it.
    """
    d = block_buf
    polys, o = set(), 0
    while o + 32 <= pool_rel:
        sz = _node_size(d, o)
        if sz is None:
            break
        if struct.unpack_from('>H', d, o + 2)[0] == NODE_LEAF:
            polys.add(o)
        o += sz
    seen, reached, stack = set(), set(), [0]
    while stack:
        o = stack.pop()
        if o in seen or o < 0 or o + 32 > pool_rel:
            continue
        seen.add(o)
        cls = struct.unpack_from('>H', d, o + 2)[0]
        nxt, prv, ch = struct.unpack_from('>iii', d, o + 4)
        if cls == NODE_LEAF:
            reached.add(o)
        if nxt:
            stack.append(o + nxt * 16)
        if ch:
            stack.append(o + ch * 16)
    return len(polys), len(reached & polys)


def link_reach_file(g):
    """Sum link_reach over every geometry block of a parsed geom_io file."""
    tot = ok = 0
    for w in g.payload.worlds:
        for b in w.blocks:
            if (b.desc.type >> 4) != 1 or b._pool_rel is None:
                continue
            t, r = link_reach(b._buf, b._pool_rel)
            tot += t
            ok += r
    return tot, ok
