# -*- coding: utf-8 -*-
"""cellfirst_build -- glTF (Haven convention) -> cell-first MGO2 collision.

    python cellfirst_build.py <in.gltf> <donor.geom> <out.geom> [options]

    --single-owner      store each face once (centroid cell) instead of in
                        every cell its AABB touches (smaller, stock-like)
    --ground            ground-aware ownership (see geom_build_chain)
    --ignore-names      group leaves by attribute only, not (attribute, name)
    --pad-to-donor      zero-pad the output to the donor's byte length
                        (dl/p geoms must keep their recorded plaintext size)
    --no-reach          skip the bp_reach gate (it is slow on big maps)
    --keep-duplicates   do not drop faces that repeat (same vertices + attribute)

Replaces the donor's section 0 (the collision database) with a world built by
`mdn/geom_build_chain.ChainedCollisionWorld` from the glTF's triangles, keeps
every other chunk (REFS 0x010000, paths 0x050000, PROPS 0x060000, ROUTES
0x070000) and relocates the absolute pointers inside REFS and ROUTES by the
amount those chunks moved.  The output is only written when geom_io parses it
and round-trips it byte-identically.

Per-leaf attributes: the writer's global `surface_id` / `obj_hash` are
replaced by the glTF material's 64-bit attribute (leaf +0x18/+0x1c) and
Haven's per-primitive nameHash (leaf +0x10) -- faces are sorted by
(attribute, name) inside a cell so every leaf is uniform, and each block's
descriptor surface id is the low 32 bits of its first leaf's attribute.
"""
import io
import os
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # all modules live side by side

import geom_io                                                    # noqa: E402
import gltf_faces                                                 # noqa: E402
from geom_build import (_align, GRID_STRIDE, EMPTY_CELL, LAYER_GEOMETRY,  # noqa: E402
                        DESC_F6, MAX_VERTS_PER_BLOCK, MAX_NORMALS_PER_BLOCK,
                        MAX_FACES_PER_LEAF, NODE_LEAF, ZERO, FF, _f32)
from geom_build_chain import (ChainedCollisionWorld, DESC_GEOM_MORE,        # noqa: E402
                              DESC_GEOM_LAST, MAX_LEAVES_PER_FIELDLESS_BLOCK,
                              count_nodes, last_root_offset16)

TAG_PAYLOAD, TAG_REFS, TAG_ROUTES = 0x000000, 0x010000, 0x070000
DEFAULT_ATTR = 0x0000000100840DC2      # plain floor on stock n002a (10,360 leaves)
DEFAULT_NAME = 0x00E832C9              # geom_build's default obj hash


def _key(f):
    return (getattr(f, 'attr', DEFAULT_ATTR),
            DEFAULT_NAME if getattr(f, 'name', None) is None else f.name)


class AttrChainedWorld(ChainedCollisionWorld):
    """ChainedCollisionWorld with per-leaf attribute / name from the faces."""

    def __init__(self, faces, ignore_names=False, **kw):
        self.ignore_names = ignore_names
        ChainedCollisionWorld.__init__(self, faces, **kw)

    def _leaf_key(self, f):
        a, n = _key(f)
        return (a, DEFAULT_NAME) if self.ignore_names else (a, n)

    def _leaf_runs(self, faces):
        """Cut a key-sorted face list into leaves: uniform key, <= 32 faces."""
        runs, cur, ck = [], [], None
        for f in faces:
            k = self._leaf_key(f)
            if cur and (k != ck or len(cur) >= MAX_FACES_PER_LEAF):
                runs.append(cur)
                cur = []
            cur.append(f)
            ck = k
        if cur:
            runs.append(cur)
        return runs

    def _split(self, faces):
        """Same caps as the parent, but leaves are counted by key runs."""
        ARENA = 0x3B70
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
            est = (nleaves * (32 + 0x40)
                   + (len(cur) + 1) * 8
                   + 16 * (1 + len(nseen) + new_n + len(vseen) + new_v))
            if cur and (nleaves > MAX_LEAVES_PER_FIELDLESS_BLOCK or est > ARENA or
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
        """(bytes, pool_rel, chash, nleaves) -- leaves uniform in (attr, name)."""
        origin = tuple(_f32(self.world_min[k] + cell[k] * self.cell[k])
                       for k in range(3))
        verts, vmap, normals, nmap = [], {}, [], {}
        leaves = []
        for run in self._leaf_runs(faces):
            rows = []
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
                if len(idx) == 3:
                    idx.append(idx[0])
                rows.append((idx, nmap[key], f.flags))
            leaves.append((self._leaf_key(run[0]), rows))
        if len(verts) > MAX_VERTS_PER_BLOCK or len(normals) > MAX_NORMALS_PER_BLOCK:
            return None

        node_sizes = [32 + _align(len(r) * 8, 16) for _, r in leaves]
        pool_rel = sum(node_sizes)
        out = bytearray()
        node_off = 0
        for li, ((attr, name), rows) in enumerate(leaves):
            body = bytearray()
            for idx, pidx, flags in rows:
                high = (((idx[0] >> 8) & 3) | (((idx[1] >> 8) & 3) << 2)
                        | (((idx[2] >> 8) & 3) << 4) | (((idx[3] >> 8) & 3) << 6))
                body += struct.pack('>BBBBBBH', idx[0] & 0xFF, idx[1] & 0xFF,
                                    idx[2] & 0xFF, idx[3] & 0xFF, high, pidx,
                                    flags & 0xFFFF)
            body += ZERO * (_align(len(body), 16) - len(body))
            hdr = bytearray(32)
            hdr[0] = len(rows)
            struct.pack_into('>H', hdr, 2, NODE_LEAF)
            struct.pack_into('>I', hdr, 0x10, name & 0xFFFFFFFF)
            struct.pack_into('>I', hdr, 0x14, (pool_rel - node_off) // 16)
            struct.pack_into('>I', hdr, 0x18, (attr >> 32) & 0xFFFFFFFF)
            struct.pack_into('>I', hdr, 0x1C, attr & 0xFFFFFFFF)
            out += bytes(hdr) + bytes(body)
            node_off += node_sizes[li]

        total_rows = 1 + len(normals) + len(verts)
        pool = bytearray(struct.pack('>4I', total_rows, 1 + len(normals), 1, 0))
        pool += struct.pack('>4f', origin[0], origin[1], origin[2], 0.0)
        for n in normals:
            pool += struct.pack('>4f', n[0], n[1], n[2], 0.0)
        for v in verts:
            pool += struct.pack('>4f', v[0] - origin[0], v[1] - origin[1],
                                v[2] - origin[2], 0.0)
        chash = leaves[0][0][0] & 0xFFFFFFFF
        return bytes(out) + bytes(pool), pool_rel, chash, len(leaves)

    def emit(self, base_off, trailer):
        """(payload, nblocks, ncells, stats) -- chain emit + per-block chash."""
        buckets = self._assign()
        cells = sorted(buckets)
        blocks = []
        chain_len = {}
        for cell in cells:
            groups = self._split(buckets[cell])
            chain_len[cell] = len(groups)
            for i, grp in enumerate(groups):
                built = self._build_block(cell, grp)
                if built is None:
                    raise ValueError('cell %s still overflows after splitting into %d '
                                     'blocks (group %d has %d faces)'
                                     % (cell, len(groups), i, len(grp)))
                data, pool_rel, chash, nleaves = built
                blocks.append((cell, data, pool_rel, i == len(groups) - 1, chash, nleaves))

        gx, gy, gz = self.dims
        ncells = gx * gy * gz
        grid_off = base_off + 0x40
        desc_off = _align(grid_off + ncells * GRID_STRIDE, 16)
        first_block = desc_off + len(blocks) * 32

        descs, head_of = [], {}
        cur = first_block
        for i, (cell, data, pool_rel, last, chash, nleaves) in enumerate(blocks):
            head_of.setdefault(cell, i)
            descs.append(dict(size=len(data), pool=cur + pool_rel, block=cur,
                              type=DESC_GEOM_LAST if last else DESC_GEOM_MORE,
                              nodes=count_nodes(data, pool_rel),
                              f4=last_root_offset16(data[:pool_rel]), chash=chash))
            cur += _align(len(data), 16)
        trailer_off = _align(cur, 16)

        desc_bytes = bytearray()
        for d in descs:
            desc_bytes += struct.pack('>BBHHHIIIIII', d['type'], min(d['nodes'], 255),
                                      d['size'], d['f4'], DESC_F6, 0, d['pool'],
                                      d['block'], 0, 1, d['chash'])
        if desc_off + len(desc_bytes) != first_block:
            raise AssertionError('descriptor table must end exactly at the first block')

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
               + struct.pack('>3fI', self.cell[0], self.cell[1], self.cell[2], 0x3F800000)
               + struct.pack('>3II', gx, gy, gz, desc_end)
               + struct.pack('>IHHII', 1, 8, GRID_STRIDE, grid_off, desc_off))
        buf = bytearray()
        buf += sub
        buf += grid
        buf += ZERO * (desc_off - grid_off - len(grid))
        buf += desc_bytes
        for i, blk in enumerate(blocks):
            buf += ZERO * ((descs[i]['block'] - base_off) - len(buf))
            buf += blk[1]
        buf += ZERO * ((trailer_off - base_off) - len(buf))
        buf += trailer
        stats = dict(blocks=len(blocks), cells_used=len(cells), ncells=ncells,
                     leaves=sum(b[5] for b in blocks),
                     max_chain=max(chain_len.values()) if chain_len else 0,
                     chains_over_10=sum(1 for v in chain_len.values() if v > 10),
                     max_block=max(len(b[1]) for b in blocks) if blocks else 0,
                     faces_stored=sum(len(g) for g in buckets.values()))
        return bytes(buf), len(blocks), ncells, stats


# ---------------------------------------------------------------- splicing --
def _refs_relocate(buf, delta):
    out = bytearray(buf)
    o = n = 0
    while o + 0x70 <= len(out):
        if struct.unpack_from('>H', out, o + 0x0E)[0] == 0:
            break
        bo = struct.unpack_from('>i', out, o + 0x68)[0]
        struct.pack_into('>i', out, o + 0x68, bo + delta)
        n += 1
        o += 0x70
    return bytes(out), n


def _routes_relocate(buf, delta):
    out = bytearray(buf)
    o = n = 0
    while o + 16 <= len(out):
        pid, z, cw, ptr = struct.unpack_from('>4I', out, o)
        cnt = cw >> 16
        if pid == 0 or cnt == 0 or cnt > 4096:
            break
        struct.pack_into('>I', out, o + 12, ptr + delta)
        n += 1
        o += 16
    return bytes(out), n


def splice_payload(donor_bytes, entries, payload, pad_to=None):
    """Replace section 0 with `payload`; shift + relocate everything after it."""
    d = donor_bytes
    order = sorted(entries, key=lambda e: e[2])
    tag0, size0, off0 = order[0]
    if tag0 != TAG_PAYLOAD:
        raise ValueError('first section is not the payload')
    delta = len(payload) - size0
    out = bytearray(d[:off0]) + bytearray(payload)
    new_entries = {TAG_PAYLOAD: (len(payload), off0)}
    reloc = {}
    for tag, size, off in order[1:]:
        blob = d[off:off + size]
        if tag == TAG_REFS:
            blob, reloc['refs'] = _refs_relocate(blob, delta)
        elif tag == TAG_ROUTES and size > 16:
            blob, reloc['routes'] = _routes_relocate(blob, delta)
        if len(out) != off + delta:
            raise AssertionError('section 0x%06x expected at 0x%x, cursor 0x%x'
                                 % (tag, off + delta, len(out)))
        out += blob
        new_entries[tag] = (size, off + delta)
    if pad_to is not None:
        if len(out) > pad_to:
            raise ValueError('output is %d bytes, larger than the %d-byte target'
                             % (len(out), pad_to))
        out += ZERO * (pad_to - len(out))
    for i, (tag, size, off) in enumerate(entries):
        ns, no = new_entries[tag]
        struct.pack_into('>3I', out, 0x20 + i * 12, tag, ns, no)
    struct.pack_into('>I', out, 4, len(out))
    struct.pack_into('>I', out, 0x24, new_entries[TAG_PAYLOAD][0])
    return bytes(out), reloc


# -------------------------------------------------------------- checking ---
def _pointer_check(data, entries):
    ent = {t: (s, o) for t, s, o in entries}
    res = {}
    if TAG_REFS in ent:
        s, o = ent[TAG_REFS]
        n = ins = q = 0
        while q + 0x70 <= s:
            if struct.unpack_from('>H', data, o + q + 0x0E)[0] == 0:
                break
            bo = struct.unpack_from('>i', data, o + q + 0x68)[0]
            n += 1
            ins += (o <= bo < o + s)
            q += 0x70
        res['refs'] = (n, ins)
    if TAG_ROUTES in ent:
        s, o = ent[TAG_ROUTES]
        n = ins = q = 0
        while q + 16 <= s:
            pid, z, cw, ptr = struct.unpack_from('>4I', data, o + q)
            cnt = cw >> 16
            if pid == 0 or cnt == 0 or cnt > 4096:
                break
            n += 1
            ins += (o <= ptr < o + s)
            q += 16
        res['routes'] = (n, ins)
    return res


def _count_faces(g):
    tot = 0
    for w in g.payload.worlds:
        for b in w.blocks:
            if (b.desc.type >> 4) != 1:
                continue
            nl = b.nodes()
            if nl is None:
                return None
            for cls, o, n in nl:
                if cls == NODE_LEAF:
                    tot += b._buf[o]
    return tot


def build(gltf_path, donor_path, out_path, single_owner=False, ignore_names=False,
          pad_to_donor=False, run_reach=True, dedupe=True, log=print):
    t0 = time.time()
    faces, rep = gltf_faces.load(gltf_path, default_attr=DEFAULT_ATTR,
                                 warn=lambda m: log('WARN ' + m))
    log('glTF: %d primitives, %d triangles, %d degenerate dropped, %d faces, %d attributes'
        % (rep['primitives'], rep['triangles'], rep['degenerate'], rep['faces'],
           len(rep['attrs'])))
    if not faces:
        raise SystemExit('no faces')
    if dedupe:
        # Haven's export carries a face once per block that stores it (stock
        # replicates ~28% of faces across cells); storing those again just
        # inflates the file and the chains.  Identity = vertex set + attribute.
        seen, kept = set(), []
        for f in faces:
            k = (tuple(sorted(f.verts)), _key(f)[0])
            if k in seen:
                continue
            seen.add(k)
            kept.append(f)
        log('dedupe: %d -> %d faces (%d duplicates dropped)'
            % (len(faces), len(kept), len(faces) - len(kept)))
        faces = kept

    donor = geom_io.GeomFile.load(donor_path)
    entries = [tuple(e) for e in donor.header.entries]
    nworlds = len(donor.payload.worlds)
    if nworlds != 1:
        log('WARN donor has %d worlds; the output will have ONE' % nworlds)
    trailer = bytes(donor.payload.worlds[-1].trailer._buf)
    base = sorted(entries, key=lambda e: e[2])[0][2]
    dref = _pointer_check(donor.source, entries)
    log('donor: %d bytes, %d worlds, %d blocks, refs %s, routes %s'
        % (len(donor.source), nworlds, len(donor.payload.blocks),
           dref.get('refs'), dref.get('routes')))

    world = AttrChainedWorld(faces, ignore_names=ignore_names, single_owner=single_owner)
    log('world: min %s cell %s dims %s' % (tuple(round(c, 1) for c in world.world_min),
                                            world.cell, world.dims))
    payload, nblocks, ncells, st = world.emit(base, trailer)
    log('section 0: %d bytes, %d blocks, %d/%d cells, %d leaves, max chain %d '
        '(%d over 10), max block 0x%X, %d face instances'
        % (len(payload), nblocks, st['cells_used'], ncells, st['leaves'],
           st['max_chain'], st['chains_over_10'], st['max_block'], st['faces_stored']))

    out, reloc = splice_payload(donor.source, entries, payload,
                                pad_to=len(donor.source) if pad_to_donor else None)
    log('spliced: %d bytes (donor %d, %+d), relocated %s'
        % (len(out), len(donor.source), len(out) - len(donor.source), reloc))

    g = geom_io.GeomFile(out, out_path)
    rt = g.to_bytes() == out
    nf = _count_faces(g)
    walked = sum(1 for b in g.payload.blocks if b.nodes() is not None)
    pc = _pointer_check(out, [tuple(e) for e in g.header.entries])
    log('geom_io: roundtrip_ok=%s, blocks %d (node walk exact %d), faces %s, refs %s, routes %s'
        % (rt, len(g.payload.blocks), walked, nf, pc.get('refs'), pc.get('routes')))
    ok = rt and walked == len(g.payload.blocks) and nf == st['faces_stored']
    if 'refs' in dref and pc.get('refs') != dref['refs']:
        ok = False
        log('FAIL refs changed %s -> %s' % (dref['refs'], pc.get('refs')))
    if 'routes' in dref and pc.get('routes') != dref['routes']:
        ok = False
        log('FAIL routes changed %s -> %s' % (dref['routes'], pc.get('routes')))
    if not ok:
        log('REFUSED: output not written')
        return 2
    with open(out_path, 'wb') as fp:
        fp.write(out)
    log('wrote %s (%.1fs)' % (out_path, time.time() - t0))

    if run_reach:
        import bp_reach
        tot, okf, _ = bp_reach.score(out_path)
        log('bp_reach: faces=%d reachable=%d %.2f%%' % (tot, okf, 100.0 * okf / max(1, tot)))
        return 0 if tot and okf / float(tot) >= 0.96 else 3
    return 0


def main(argv):
    args = [a for a in argv[1:] if not a.startswith('--')]
    if len(args) != 3:
        print(__doc__)
        return 1
    return build(args[0], args[1], args[2],
                 single_owner='ground' if '--ground' in argv else ('--single-owner' in argv),
                 ignore_names='--ignore-names' in argv,
                 pad_to_donor='--pad-to-donor' in argv,
                 run_reach='--no-reach' not in argv,
                 dedupe='--keep-duplicates' not in argv)


if __name__ == '__main__':
    sys.exit(main(sys.argv))
