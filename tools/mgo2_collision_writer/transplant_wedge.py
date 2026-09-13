# -*- coding: utf-8 -*-
"""transplant_wedge -- move ONE Blood Bath concrete wedge (visual + collision)
to Ambush Alley.  The full custom-map unit test: one object, both halves.

    python transplant_wedge.py            dry run: writes out/n002a_cache.dar.wedge
                                          and out/wedge_collision.json
    python transplant_wedge.py --apply    install the cache.dar (backup .bak-marker
                                          already holds stock); then run
                                          collision_canary_build.py --apply, which
                                          picks up wedge_collision.json automatically

Source: n007a_obj.mdn sub-mesh 14, the connected component centred near
(21018, 1522, -2898): 16 verts / 17 tris, bbox 1.8 x 1.1 x 3.4 m, a wedge
(1104 tall at one end, 348 at the other).  Same 32-byte vertex layout as
Ambush Alley's placed-props mesh (0x10 pos, 0xa2 nrm, 0x83 colour, 0x78/0x79
uv, 0xae tangent), so the vertex RECORDS are copied verbatim -- only the
position floats are rewritten.

Target visual: n002a_haitikomono.mdn sub-mesh 15 (144 verts, 216 indices,
materials 56/57).  The wedge takes vertex slots 0-15 and the first 51 indices
of the first section; the other 128 verts collapse to the wedge centre and
every remaining index becomes (0,0,0).  All tables byte-identical except the
recomputed bbox row; file and .dar sizes unchanged.  The wedge wears material
56's texture -- whatever that is on Ambush Alley.

Collision: every face of the Blood Bath geom within the wedge's bbox (+150)
that is not flat ground (rises > 30 units or sits > 40 above the wedge base),
attributes and winding carried verbatim, translated by the same vector.

Placement: bbox centre -> X -30000, Z -69000 (inside grid cell (11,0,15)),
bottom -> Y 0.
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

U = r'C:\Users\soxfr\Downloads\SaveMGO\MGO2\dev_hdd0\game\NPMG00020\USRDIR\o\stage'
SRC_DAR = os.path.join(U, 'n007a', 'cache.dar.orig')          # stock Blood Bath
SRC_GEOM = r'D:\mgo2work\mapbuild\heliport\dlp_n007a_stock.geom'
SRC_MESH = 'n007a_obj.mdn'
SRC_SUB = int(os.environ.get('WEDGE_SRC_SUB', '2'))
SRC_CENTRE = tuple(float(x) for x in os.environ.get('WEDGE_SRC_CENTRE', '900,1520,-22436').split(','))
SRC_NAMES = [int(x, 16) for x in os.environ.get('WEDGE_SRC_NAMES', '2f9799,c0163e,56833a').split(',') if x]
CLOSE_END = os.environ.get('WEDGE_CLOSE_END', 'maxx')   # add a wall at this end of the character shell
DST_LIVE = os.path.join(U, 'n002a', 'cache.dar')
DST_STOCK_MD5 = '234aff58'
DST_MESH, DST_SUB = 'n002a_haitikomono.mdn', int(os.environ.get('WEDGE_DST_SUB', '0'))
DST_SEC = int(os.environ.get('WEDGE_DST_SEC', '1'))     # section index WITHIN the sub-mesh
TARGET_CX, TARGET_CZ = -30000.0, -69000.0
TARGET_BOTTOM_Y = float(os.environ.get('WEDGE_BOTTOM_Y', '125'))   # AA gravel there peaks at +125
VERT_COLOUR = bytes.fromhex(os.environ.get('WEDGE_COLOUR', '8c8c8cff'))   # BB verts carry alpha 00/80 -> invisible
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
CACHE = r'C:\Users\soxfr\Downloads\rpcs3-source\bin\dev_hdd1\caches\_NPMG00020\o\stage\n002a'


def md5(b):
    import hashlib
    return hashlib.md5(b).hexdigest()[:8]


def submesh(m, i):
    mv = mdn_io.MeshView(m, i)
    fb = bytes(m.segment('FaceBuffer').data)
    vi = dict(m.table('VertIndex'))[i]
    sc, ss = struct.unpack_from('>2I', vi, 8)
    fi = m.table('FaceIndex')
    tris = []
    secs = []
    for k in range(ss, ss + sc):
        w0, w1, w2, w3 = struct.unpack('>4I', fi[k][1])
        cnt = w0 & 0xffff
        assert w0 >> 31, 'strip section'
        idx = struct.unpack('>%dH' % cnt, fb[w1:w1 + 2 * cnt])
        tris += [tuple(idx[j:j + 3]) for j in range(0, cnt - cnt % 3, 3)]
        secs.append((k, w1, cnt, w2))
    return mv, mv.positions(), tris, secs


def component(pos, tris, centre):
    parent = list(range(len(pos)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for a, b, c in tris:
        parent[find(a)] = find(b)
        parent[find(b)] = find(c)
    comps = collections.defaultdict(list)
    for k in range(len(pos)):
        comps[find(k)].append(k)
    best = min(comps.values(), key=lambda c: sum((sum(pos[k][j] for k in c) / len(c) - centre[j]) ** 2 for j in range(3)))
    return sorted(best), [t for t in tris if t[0] in best]


def build(log=print):
    # ---- source visual
    src_plain = C.decrypt(open(SRC_DAR, 'rb').read(), 'stage/n007a')
    src_ents = dar_io.parse(src_plain)
    ms = mdn_io.Mdn.parse([x for x in src_ents if x['name'] == SRC_MESH][0]['data'])
    mvs, pos_s, tris_s, _ = submesh(ms, SRC_SUB)
    comp, ctris = component(pos_s, tris_s, SRC_CENTRE)
    remap = {v: i for i, v in enumerate(comp)}
    ctris = [(remap[a], remap[b], remap[c]) for a, b, c in ctris]
    p = [pos_s[k] for k in comp]
    lo = [min(q[k] for q in p) for k in range(3)]
    hi = [max(q[k] for q in p) for k in range(3)]
    dx = TARGET_CX - (lo[0] + hi[0]) / 2.0
    dz = TARGET_CZ - (lo[2] + hi[2]) / 2.0
    dy = TARGET_BOTTOM_Y - lo[1]
    log('source wedge: %d verts %d tris bbox %s..%s -> shift (%.0f, %.0f, %.0f)'
        % (len(comp), len(ctris), [round(x) for x in lo], [round(x) for x in hi], dx, dy, dz))
    vb_s = bytes(ms.segment('VertBuffer').data)
    src_off = {a: mvs.attr_offs[k] for k, a in enumerate(mvs.attrs)}
    src_recs = [bytes(vb_s[mvs.start + v * mvs.stride: mvs.start + (v + 1) * mvs.stride]) for v in comp]

    # ---- destination visual
    enc = open(DST_LIVE, 'rb').read()
    if md5(enc) != DST_STOCK_MD5:
        bak = DST_LIVE + '.bak-marker'
        assert md5(open(bak, 'rb').read()) == DST_STOCK_MD5, 'no stock cache.dar available'
        log('live cache.dar is %s; sourcing stock from .bak-marker' % md5(enc))
        enc = open(bak, 'rb').read()
    plain = C.decrypt(enc, 'stage/n002a')
    trailer = enc[len(C.encrypt(plain, 'stage/n002a')):]
    ents = dar_io.parse(plain)
    assert dar_io.build(ents) == plain
    e = [x for x in ents if x['name'] == DST_MESH][0]
    md = mdn_io.Mdn.parse(e['data'])
    assert md.build() == e['data']
    mvd, pos_d, tris_d, secs_d = submesh(md, DST_SUB)
    # build destination-layout records attribute by attribute (position 12 B, all
    # other attrs 4 B); source attrs the destination lacks (extra UV sets) are dropped
    dst_off = {a: mvd.attr_offs[k] for k, a in enumerate(mvd.attrs)}
    assert all(a in src_off for a in mvd.attrs), ('source lacks an attr the slot needs', mvd.attrs, mvs.attrs)
    records = []
    for rec in src_recs:
        out_rec = bytearray(mvd.stride)
        for a, o in dst_off.items():
            n = 12 if a == 0x10 else 4
            out_rec[o:o + n] = rec[src_off[a]:src_off[a] + n]
        x, y, z = struct.unpack_from('>3f', out_rec, dst_off[0x10])
        struct.pack_into('>3f', out_rec, dst_off[0x10], x + dx, y + dy, z + dz)
        if 0x83 in dst_off and VERT_COLOUR:
            out_rec[dst_off[0x83]:dst_off[0x83] + 4] = VERT_COLOUR
        records.append(bytes(out_rec))
    log('records converted %s/%d -> %s/%d' % ([hex(a) for a in mvs.attrs], mvs.stride, [hex(a) for a in mvd.attrs], mvd.stride))
    row, w1, cnt, mat = secs_d[DST_SEC]
    w3 = struct.unpack('>4I', md.table('FaceIndex')[row][1])[3]
    vmin, vcnt = w3 >> 16, w3 & 0xffff
    assert len(records) <= vcnt and 3 * len(ctris) <= cnt, ('section too small', vcnt, cnt)
    vb = md.segment('VertBuffer').data
    fb = md.segment('FaceBuffer').data
    cur = struct.unpack('>%dH' % cnt, bytes(fb[w1:w1 + 2 * cnt]))
    assert vmin <= min(cur) and max(cur) < vmin + vcnt, ('indices not sub-mesh-relative', min(cur), max(cur), vmin, vcnt)
    po_d = mvd.attr_offs[mvd.attrs.index(0x10)]
    centre = (TARGET_CX, TARGET_BOTTOM_Y + (hi[1] - lo[1]) / 2.0, TARGET_CZ)
    for v in range(vmin, vmin + vcnt):
        base = mvd.start + v * mvd.stride
        if v - vmin < len(records):
            vb[base:base + mvd.stride] = records[v - vmin]
        else:
            struct.pack_into('>3f', vb, base + po_d, *centre)      # park the section's other verts
    flat = [vmin + i for t in ctris for i in t]
    for j in range(cnt):
        struct.pack_into('>H', fb, w1 + 2 * j, flat[j] if j < len(flat) else vmin)
    log('slot: sub %d section %d (row %d) mat %d, verts %d..+%d, %d indices; wedge uses 16 verts / %d indices' % (DST_SUB, DST_SEC, row, mat, vmin, vcnt, cnt, len(flat)))
    changed = mdn_io.recompute_bboxes(md)
    data = md.build()
    assert len(data) == len(e['data'])
    log('target %s sub %d: %d bbox rows rewritten, %d bytes changed' % (DST_MESH, DST_SUB, changed, sum(1 for a, b in zip(data, e['data']) if a != b)))
    e['data'] = data
    new_plain = dar_io.build(ents)
    assert len(new_plain) == len(plain)
    out = C.encrypt(new_plain, 'stage/n002a') + trailer
    assert len(out) == len(enc) and C.decrypt(out, 'stage/n002a') == new_plain
    os.makedirs(OUT, exist_ok=True)
    op = os.path.join(OUT, 'n002a_cache.dar.wedge')
    open(op, 'wb').write(out)
    log('visual: %s md5 %s' % (op, md5(out)))

    # ---- collision faces
    g = G.GeomFile.load(SRC_GEOM)
    faces = []
    for w in g.payload.worlds:
        for b in w.blocks:
            if (b.desc.type >> 4) != 1:
                continue
            nl = b.nodes() or []
            d = b._buf
            verts = b.vertices()
            for cls, o, n in nl:
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
                        continue                                    # flat ground under it
                    if SRC_NAMES and name not in SRC_NAMES:
                        continue
                    flags = struct.unpack_from('>H', d, f + 6)[0]
                    quad = idx[3] != idx[0]
                    pts = fv if quad else fv[:3]
                    faces.append({'verts': [[q[0] + dx, q[1] + dy, q[2] + dz] for q in pts],
                                  'attr': attr, 'name': name, 'flags': flags})
    if CLOSE_END:
        # seal the end that BB seals with neighbouring geometry: one wall quad
        # across the full bbox at that end, floor to top, wearing the wedge's
        # own wall word/name (Rail) plus a scenery copy
        wall_attr = 0x00208402034; scen = 0x00100840dc2
        wnames = [f['name'] for f in faces if f['attr'] == wall_attr]
        wname = wnames[0] if wnames else SRC_NAMES[0]
        X0, X1, Z0, Z1 = lo[0] + dx, hi[0] + dx, lo[2] + dz, hi[2] + dz
        Y0, Y1 = TARGET_BOTTOM_Y - 100.0, hi[1] + dy
        if CLOSE_END == 'maxx':
            q = [[X1, Y0, Z0], [X1, Y0, Z1], [X1, Y1, Z1], [X1, Y1, Z0]]
        elif CLOSE_END == 'minx':
            q = [[X0, Y0, Z1], [X0, Y0, Z0], [X0, Y1, Z0], [X0, Y1, Z1]]
        elif CLOSE_END == 'maxz':
            q = [[X1, Y0, Z1], [X0, Y0, Z1], [X0, Y1, Z1], [X1, Y1, Z1]]
        else:
            q = [[X0, Y0, Z0], [X1, Y0, Z0], [X1, Y1, Z0], [X0, Y1, Z0]]
        faces.append({'verts': q, 'attr': wall_attr, 'name': wname, 'flags': 0x0040, 'closure': True})
        faces.append({'verts': q, 'attr': scen, 'name': wname, 'flags': 0x2040, 'closure': True})
        log('closure wall added at %s (name %06x)' % (CLOSE_END, wname))
    attrs = collections.Counter('%x' % f['attr'] for f in faces)
    names = collections.Counter('%06x' % f['name'] for f in faces)
    log('collision: %d faces, attrs %s, names %s' % (len(faces), dict(attrs), dict(names)))
    jp = os.path.join(OUT, 'wedge_collision.json')
    json.dump({'faces': faces, 'centre': [TARGET_CX, TARGET_CZ], 'bbox_lo': [lo[0] + dx, lo[1] + dy, lo[2] + dz],
               'bbox_hi': [hi[0] + dx, hi[1] + dy, hi[2] + dz]}, open(jp, 'w'), indent=0)
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
    print('NEXT: python collision_canary_build.py --apply   (reads out/wedge_collision.json)')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
