# -*- coding: utf-8 -*-
"""transplant_block -- move a WHOLE Blood Bath concrete block (an assembly of
co-located props components) to Ambush Alley: visual + collision.

    python transplant_block.py            dry run
    python transplant_block.py --apply    install cache.dar; then collision_canary_build.py --apply

The 'wedge' is only the sloped top skin (n007a_obj sub 2 comp 294, 9 verts).
The object as drawn is that plus two side slabs, a cap strip and two end
faces, all co-located (catalogue 2026-09-12): sub 2 comps 291, 293, 294, 295
(material 25) and 115, 116 (material 16).  Ground at Y 84, slope top from 968
to 2072: 0.9 m tall at the low end, 2.0 m at the tall end, 3.7 x 2.0 m.

Visual: components picked from the catalogue's bbox centres (nearest
connected component in that sub-mesh), records converted per attribute into
the slot's layout, colour forced opaque, merged into ONE slot section:
n002a_haitikomono sub 0 section 0 (336 verts / 504 indices, material 1).
Collision: every geom face inside the assembly's bbox (+150) that is not
flat ground, verbatim (attr, name, material word), translated; plus a sealing
wall at the tall end if the character shell is open there.
"""
import os
import sys
import json
import struct
import shutil
import collections

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # all modules live side by side
import dar_io                                   # noqa: E402
import mdn_io                                   # noqa: E402
import mgo2_stage_crypto as C                   # noqa: E402
import geom_io as G                             # noqa: E402
from transplant_wedge import submesh, component, md5   # noqa: E402

U = r'C:\Users\soxfr\Downloads\SaveMGO\MGO2\dev_hdd0\game\NPMG00020\USRDIR\o\stage'
SRC_DAR = os.path.join(U, 'n007a', 'cache.dar.orig')
SRC_GEOM = r'D:\mgo2work\mapbuild\heliport\dlp_n007a_stock.geom'
SRC_MESH = 'n007a_obj.mdn'
CATALOG = r'D:\mgo2work\catalog\props\n007a.json'
PARTS = [(2, 291), (2, 293), (2, 294), (2, 295), (2, 115), (2, 116)]
DST_LIVE = os.path.join(U, 'n002a', 'cache.dar')
DST_STOCK_MD5 = '234aff58'
DST_MESH, DST_SUB, DST_SEC = 'n002a_haitikomono.mdn', 0, 0
TARGET_CX, TARGET_CZ = -30000.0, -69000.0
TARGET_BOTTOM_Y = float(os.environ.get('BLOCK_BOTTOM_Y', '125'))
VERT_COLOUR = bytes.fromhex(os.environ.get('BLOCK_COLOUR', '8c8c8cff'))
CLOSE_END = os.environ.get('BLOCK_CLOSE_END', 'auto')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
CACHE = r'C:\Users\soxfr\Downloads\rpcs3-source\bin\dev_hdd1\caches\_NPMG00020\o\stage\n002a'


def build(log=print):
    cat = {(o['sub'], o['comp']): o for o in json.load(open(CATALOG))['objects'] if o['mesh'] == SRC_MESH}
    src_plain = C.decrypt(open(SRC_DAR, 'rb').read(), 'stage/n007a')
    ms = mdn_io.Mdn.parse([x for x in dar_io.parse(src_plain) if x['name'] == SRC_MESH][0]['data'])
    vb_s = bytes(ms.segment('VertBuffer').data)

    # ---- gather the assembly: records (source layout) + triangles (merged index space)
    all_recs, all_tris, layout = [], [], None
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for sub, comp in PARTS:
        o = cat[(sub, comp)]
        mv, pos, tris, _ = submesh(ms, sub)
        cverts, ctris = component(pos, tris, tuple(o['centre']))
        remap = {v: len(all_recs) + i for i, v in enumerate(cverts)}
        all_tris += [(remap[a], remap[b], remap[c]) for a, b, c in ctris]
        src_off = {a: mv.attr_offs[k] for k, a in enumerate(mv.attrs)}
        if layout is None:
            layout = (mv.attrs, mv.stride, src_off)
        assert (mv.attrs, mv.stride) == layout[:2], 'parts differ in layout'
        for v in cverts:
            all_recs.append(bytes(vb_s[mv.start + v * mv.stride: mv.start + (v + 1) * mv.stride]))
        p = [pos[k] for k in cverts]
        for k in range(3):
            lo[k] = min(lo[k], min(q[k] for q in p))
            hi[k] = max(hi[k], max(q[k] for q in p))
        log('part sub %d comp %d: %d verts %d tris (catalogue size %s)' % (sub, comp, len(cverts), len(ctris), o['size']))
    dx = TARGET_CX - (lo[0] + hi[0]) / 2.0
    dz = TARGET_CZ - (lo[2] + hi[2]) / 2.0
    dy = TARGET_BOTTOM_Y - lo[1]
    log('assembly: %d verts %d tris bbox %s..%s -> shift (%.0f, %.0f, %.0f)'
        % (len(all_recs), len(all_tris), [round(x) for x in lo], [round(x) for x in hi], dx, dy, dz))

    # ---- destination slot
    enc = open(DST_LIVE, 'rb').read()
    if md5(enc) != DST_STOCK_MD5:
        bak = DST_LIVE + '.bak-marker'
        assert md5(open(bak, 'rb').read()) == DST_STOCK_MD5
        enc = open(bak, 'rb').read()
        log('live cache.dar is modded; sourcing stock from .bak-marker')
    plain = C.decrypt(enc, 'stage/n002a')
    trailer = enc[len(C.encrypt(plain, 'stage/n002a')):]
    ents = dar_io.parse(plain)
    assert dar_io.build(ents) == plain
    e = [x for x in ents if x['name'] == DST_MESH][0]
    md = mdn_io.Mdn.parse(e['data'])
    assert md.build() == e['data']
    mvd, _, _, secs_d = submesh(md, DST_SUB)
    row, w1, cnt, mat = secs_d[DST_SEC]
    w3 = struct.unpack('>4I', md.table('FaceIndex')[row][1])[3]
    vmin, vcnt = w3 >> 16, w3 & 0xffff
    assert len(all_recs) <= vcnt and 3 * len(all_tris) <= cnt, ('slot too small', vcnt, cnt, len(all_recs), 3 * len(all_tris))
    dst_off = {a: mvd.attr_offs[k] for k, a in enumerate(mvd.attrs)}
    src_off = layout[2]
    assert all(a in src_off for a in mvd.attrs), (mvd.attrs, layout[0])
    vb = md.segment('VertBuffer').data
    fb = md.segment('FaceBuffer').data
    centre = (TARGET_CX, TARGET_BOTTOM_Y + (hi[1] - lo[1]) / 2.0, TARGET_CZ)
    for i in range(vcnt):
        base = mvd.start + (vmin + i) * mvd.stride
        if i < len(all_recs):
            rec = all_recs[i]
            out_rec = bytearray(mvd.stride)
            for a, o in dst_off.items():
                n = 12 if a == 0x10 else 4
                out_rec[o:o + n] = rec[src_off[a]:src_off[a] + n]
            x, y, z = struct.unpack_from('>3f', out_rec, dst_off[0x10])
            struct.pack_into('>3f', out_rec, dst_off[0x10], x + dx, y + dy, z + dz)
            if 0x83 in dst_off:
                out_rec[dst_off[0x83]:dst_off[0x83] + 4] = VERT_COLOUR
            vb[base:base + mvd.stride] = bytes(out_rec)
        else:
            struct.pack_into('>3f', vb, base + dst_off[0x10], *centre)
    flat = [vmin + i for t in all_tris for i in t]
    for j in range(cnt):
        struct.pack_into('>H', fb, w1 + 2 * j, flat[j] if j < len(flat) else vmin)
    changed = mdn_io.recompute_bboxes(md)
    data = md.build()
    assert len(data) == len(e['data'])
    log('slot sub %d sec %d (mat %d): %d/%d verts, %d/%d indices used; %d bbox rows rewritten' % (DST_SUB, DST_SEC, mat, len(all_recs), vcnt, len(flat), cnt, changed))
    e['data'] = data
    new_plain = dar_io.build(ents)
    assert len(new_plain) == len(plain)
    out = C.encrypt(new_plain, 'stage/n002a') + trailer
    assert len(out) == len(enc) and C.decrypt(out, 'stage/n002a') == new_plain
    os.makedirs(OUT, exist_ok=True)
    op = os.path.join(OUT, 'n002a_cache.dar.block')
    open(op, 'wb').write(out)
    log('visual: %s md5 %s' % (op, md5(out)))

    # ---- collision: everything inside the assembly bbox that is not flat ground
    g = G.GeomFile.load(SRC_GEOM)
    faces = []
    for w in g.payload.worlds:
        for b in w.blocks:
            if (b.desc.type >> 4) != 1:
                continue
            d = b._buf
            verts = b.vertices()
            for cls, o, n in (b.nodes() or []):
                if cls != 0x2012:
                    continue
                attr = struct.unpack_from('>Q', d, o + 0x18)[0]
                name = struct.unpack_from('>I', d, o + 0x10)[0]
                for j in range(d[o]):
                    f = o + 32 + j * 8
                    hb = d[f + 4]
                    idx = (d[f] + 256 * (hb & 3), d[f + 1] + 256 * ((hb >> 2) & 3),
                           d[f + 2] + 256 * ((hb >> 4) & 3), d[f + 3] + 256 * ((hb >> 6) & 3))
                    if max(idx) >= len(verts):
                        continue
                    fv = [verts[i] for i in idx]
                    if not all(lo[k] - 150 <= min(q[k] for q in fv) and max(q[k] for q in fv) <= hi[k] + 150 for k in range(3)):
                        continue
                    ys = [q[1] for q in fv]
                    if max(ys) - min(ys) <= 30 and min(ys) <= lo[1] + 40:
                        continue
                    quad = idx[3] != idx[0]
                    pts = fv if quad else fv[:3]
                    faces.append({'verts': [[q[0] + dx, q[1] + dy, q[2] + dz] for q in pts],
                                  'attr': attr, 'name': name, 'flags': struct.unpack_from('>H', d, f + 6)[0]})
    # open edges of the character shell -> where to seal
    ch = [f for f in faces if f['attr'] & 0x10]
    edges = collections.Counter()
    for f in ch:
        vs = [tuple(round(x / 10) * 10 for x in v) for v in f['verts']]
        for i in range(len(vs)):
            edges[tuple(sorted((vs[i], vs[(i + 1) % len(vs)])))] += 1
    open_e = [e_ for e_, c in edges.items() if c == 1 and max(e_[0][1], e_[1][1]) > TARGET_BOTTOM_Y + 60]
    X0, X1, Z0, Z1 = lo[0] + dx, hi[0] + dx, lo[2] + dz, hi[2] + dz
    side = collections.Counter()
    for e_ in open_e:
        mx = (e_[0][0] + e_[1][0]) / 2
        mz = (e_[0][2] + e_[1][2]) / 2
        side[min((('minx', abs(mx - X0)), ('maxx', abs(mx - X1)), ('minz', abs(mz - Z0)), ('maxz', abs(mz - Z1))), key=lambda t: t[1])[0]] += 1
    log('collision: %d faces %s; character shell open edges above ground: %d by side %s'
        % (len(faces), dict(collections.Counter('%x' % f['attr'] for f in faces)), len(open_e), dict(side)))
    ends = [CLOSE_END] if CLOSE_END not in ('auto', '') else [s_ for s_, c in side.items() if c >= 2]
    wall_attr, scen = 0x00208402034, 0x00100840dc2
    wnames = [f['name'] for f in faces if f['attr'] == wall_attr]
    wname = wnames[0] if wnames else (ch[0]['name'] if ch else 0xC0FFEE)
    Y0, Y1 = TARGET_BOTTOM_Y - 100.0, hi[1] + dy
    for end in ends:
        q = {'maxx': [[X1, Y0, Z0], [X1, Y0, Z1], [X1, Y1, Z1], [X1, Y1, Z0]],
             'minx': [[X0, Y0, Z1], [X0, Y0, Z0], [X0, Y1, Z0], [X0, Y1, Z1]],
             'maxz': [[X1, Y0, Z1], [X0, Y0, Z1], [X0, Y1, Z1], [X1, Y1, Z1]],
             'minz': [[X0, Y0, Z0], [X1, Y0, Z0], [X1, Y1, Z0], [X0, Y1, Z0]]}[end]
        faces.append({'verts': q, 'attr': wall_attr, 'name': wname, 'flags': 0x0040, 'closure': True})
        faces.append({'verts': q, 'attr': scen, 'name': wname, 'flags': 0x2040, 'closure': True})
        log('sealing wall added at %s' % end)
    jp = os.path.join(OUT, 'wedge_collision.json')
    json.dump({'faces': faces, 'centre': [TARGET_CX, TARGET_CZ], 'bbox_lo': [X0, lo[1] + dy, Z0], 'bbox_hi': [X1, hi[1] + dy, Z1]}, open(jp, 'w'), indent=0)
    log('wrote %s' % jp)
    return out


def main(argv):
    out = build()
    if '--apply' not in argv:
        print('DRY RUN: game tree untouched.')
        return 0
    bak = DST_LIVE + '.bak-marker'
    if not os.path.exists(bak):
        shutil.copyfile(DST_LIVE, bak)
    open(DST_LIVE, 'wb').write(out)
    print('installed -> %s' % DST_LIVE)
    if os.path.isdir(CACHE):
        shutil.rmtree(CACHE)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
