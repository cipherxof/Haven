# -*- coding: utf-8 -*-
"""collision_canary_build -- in-game proof for the cell-first collision writer.

    python collision_canary_build.py            dry run: build + gates, nothing installed
    python collision_canary_build.py --apply    install into dl/p n002a (backup .bak-colcanary),
                                                bless the launcher manifest, purge the stage cache

Rebuilds Ambush Alley's (n002a) collision from Haven's export of the STOCK dl/p
geom through our own writer (mdn/geom_build_chain via cellfirst_build), and adds
ONE invisible collision block on open floor between the two Team-A Team
Deathmatch spawn rows.  Nothing visual changes (cache.dar untouched).

Readout in game (host Team Deathmatch on Ambush Alley, spawn as Team A, walk
between the two spawn rows toward the other cluster):
  * you bump into / climb onto an invisible block at about X -32000, Z -69000
    AND the rest of the map is walkable            -> writer output LOADS and HOLDS
  * no block, map normal                            -> our file was not what loaded
  * block present but you fall through elsewhere    -> loads, binning still wrong
  * no floor at all                                 -> loads, writer output rejected/broken
"""
import os
import sys
import shutil
import struct
import subprocess
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # all modules live side by side

import geom_io                                                     # noqa: E402
import gltf_faces                                                  # noqa: E402
import bp_reach                                                    # noqa: E402
import mgo2_stage_crypto as C                                      # noqa: E402
from gltf_faces import AttrFace                                    # noqa: E402
from cellfirst_build import (splice_payload,                       # noqa: E402
                             _pointer_check, _count_faces, DEFAULT_ATTR)
from cellfirst_fields import FieldChainedWorld, link_reach_file    # noqa: E402

STAGE = 'n002a'
KEYPATH = 'stage/' + STAGE
GLTF = r'D:\mgo2work\mapbuild\heliport\n002a_dlp.gltf'      # Haven export of STOCK dl/p n002a
LIVE = (r'C:\Users\soxfr\Downloads\SaveMGO\MGO2\dev_hdd0\game\NPMG00020\USRDIR'
        r'\o\dl\p\stage\n002a\n002a.geom')
STOCK_ENC_MD5 = '316e36e4'
OUT = os.path.join(HERE, 'out')
CACHE = r'C:\Users\soxfr\Downloads\rpcs3-source\bin\dev_hdd1\caches\_NPMG00020\o\stage\n002a'
MANIFEST_SYNC = r'D:\mgo2work\tools\manifest_sync.py'

# The block: centre XZ, half-extent, Y bottom/top.  Probed 2026-09-12 from the
# stock faces: 48 floor faces at y -125..125 and 0 wall faces within r1200 of
# this XZ -> open floor between the Team-A TDM spawn rows.
BOX_CX, BOX_CZ, BOX_HALF, BOX_Y0, BOX_Y1 = -30000.0, -69000.0, 750.0, -150.0, 1050.0   # v5: fully inside cell (11, 0, 15)
BOX_NAME = 0x00C0FFEE   # leaf +0x10 object hash: a marker you can find in the output


def box_faces(cx, cz, half, y0, y1, attr, name):
    x0, x1, z0, z1 = cx - half, cx + half, cz - half, cz + half

    def P(x, y, z):
        return (float(x), float(y), float(z))
    a, b, c, d = P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1)   # bottom ring
    e, f, g, h = P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1), P(x0, y1, z1)   # top ring
    quads = [            # outward-facing, counter-clockwise seen from outside
        (e, f, g, h),    # top    (+Y)
        (a, d, c, b),    # bottom (-Y)
        (a, b, f, e),    # -Z side
        (c, d, h, g),    # +Z side
        (b, c, g, f),    # +X side
        (d, a, e, h),    # -X side
    ]
    # Two copies, like stock: a CHARACTER copy (Floor|Player|Enemy... on the
    # top, the character-only blocking-wall word on the sides) and a
    # PROJECTILE/camera copy (the shootable-scenery word).  With only the
    # scenery word (2026-09-12 19:11 screenshot) dropped boxes rested on the
    # block and the camera hit it, but the player walked straight through.
    ATTR_FLOOR = 0x0020a000834      # walkable floor: Floor|Player|Enemy|Ik|NoBehind|F27|F33
    ATTR_WALL = 0x00208202830       # blocking wall, character-only
    ATTR_SCENERY = 0x00100840dc2    # bullets/camera/objects
    faces = []
    for qi, q in enumerate(quads):
        q = q[::-1]   # engine normal = (b-a)x(c-a); the lists above come out INWARD (walked in, could not walk out, 2026-09-12 19:25)
        char_attr = ATTR_FLOOR if qi == 0 else ATTR_WALL
        for a in (char_attr, ATTR_SCENERY):
            faces.append(AttrFace([q[0], q[1], q[2]], None, 0, a, name))
            faces.append(AttrFace([q[0], q[2], q[3]], None, 0, a, name))
    return faces


def md5(b):
    import hashlib
    return hashlib.md5(b).hexdigest()[:8]


def build(log=print):
    t0 = time.time()
    enc = open(LIVE, 'rb').read()
    if md5(enc) != STOCK_ENC_MD5:
        bak = LIVE + '.bak-colcanary'
        if os.path.exists(bak) and md5(open(bak, 'rb').read()) == STOCK_ENC_MD5:
            log('live dl/p geom is %s (a previous canary); sourcing stock from %s'
                % (md5(enc), os.path.basename(bak)))
            enc = open(bak, 'rb').read()
        else:
            raise SystemExit('live dl/p %s.geom is %s, expected stock %s -- restore first'
                             % (STAGE, md5(enc), STOCK_ENC_MD5))
    plain = C.decrypt(enc, KEYPATH)
    trailer = enc[len(C.encrypt(plain, KEYPATH)):]
    log('live: enc %d plain %d trailer %d B (%s)' % (len(enc), len(plain), len(trailer), trailer.hex()))
    donor = geom_io.GeomFile(plain, 'live_' + STAGE)
    entries = [tuple(e) for e in donor.header.entries]
    assert len(donor.payload.worlds) == 1
    wtrailer = bytes(donor.payload.worlds[-1].trailer._buf)
    base = sorted(entries, key=lambda e: e[2])[0][2]
    dref = _pointer_check(plain, entries)
    log('donor: %d blocks, refs %s, routes %s'
        % (len(donor.payload.blocks), dref.get('refs'), dref.get('routes')))

    faces, rep = gltf_faces.load(GLTF, default_attr=DEFAULT_ATTR, warn=lambda m: log('WARN ' + m))
    log('glTF: %d faces (%d tris, %d degenerate dropped, %d attrs)'
        % (len(faces), rep['triangles'], rep['degenerate'], len(rep['attrs'])))
    seen, kept = set(), []
    for f in faces:
        k = (tuple(sorted(f.verts)), getattr(f, 'attr', DEFAULT_ATTR))
        if k in seen:
            continue
        seen.add(k)
        kept.append(f)
    log('dedupe: %d -> %d' % (len(faces), len(kept)))
    faces = kept

    # openness check of the footprint, from the stock faces themselves
    hits = [f for f in faces
            if max(v[0] for v in f.verts) >= BOX_CX - 1200 and min(v[0] for v in f.verts) <= BOX_CX + 1200
            and max(v[2] for v in f.verts) >= BOX_CZ - 1200 and min(v[2] for v in f.verts) <= BOX_CZ + 1200]
    walls = sum(1 for f in hits if abs(f.normal[1]) < 0.5)
    ys = [v[1] for f in hits for v in f.verts]
    log('footprint: %d stock faces, %d wall-ish, y %.0f..%.0f' % (len(hits), walls, min(ys), max(ys)))
    if walls or max(ys) > 300:
        raise SystemExit('footprint is not open floor -- move the box')

    box = box_faces(BOX_CX, BOX_CZ, BOX_HALF, BOX_Y0, BOX_Y1, DEFAULT_ATTR, BOX_NAME)
    # TRANSPLANT MODE: if transplant_wedge.py left out/wedge_collision.json, the
    # Blood Bath wedge's own collision faces (attrs + winding verbatim, already
    # translated) replace the authored box.
    import json as _json
    wj = os.path.join(OUT, 'wedge_collision.json')
    if os.path.exists(wj) and os.environ.get('COLCANARY_SHAPE', 'wedge') == 'wedge':
        wd = _json.load(open(wj))
        box = []
        if os.environ.get('COLCANARY_WEDGE', 'faces') == 'prism':
            # CLOSED PRISM authored from the wedge's bbox. The carried Blood Bath
            # character copy is open at the tall end (7 boundary edges above
            # ground, 2026-09-12) and hung the game on every kind of contact.
            lo_, hi_ = wd['bbox_lo'], wd['bbox_hi']
            x0, x1, z0, z1 = lo_[0], hi_[0], lo_[2], hi_[2]
            h_z0, h_z1, yb = hi_[1], 348.0, -100.0        # tall end at min Z (measured), short end 348
            if os.environ.get('COLCANARY_FLAT', '1') == '1':
                h_z1 = h_z0          # FLAT-TOP control: prism footprint + height, horizontal top edges everywhere
            P = lambda x, y, z: (float(x), float(y), float(z))
            a, b, c, d = P(x0, yb, z0), P(x1, yb, z0), P(x1, yb, z1), P(x0, yb, z1)
            e, f, g, h = P(x0, h_z0, z0), P(x1, h_z0, z0), P(x1, h_z1, z1), P(x0, h_z1, z1)
            quads = [(e, f, g, h), (a, b, c, d), (a, b, f, e), (d, c, g, h), (b, c, g, f), (a, d, h, e)]
            if os.environ.get('COLCANARY_RAMP', 'stepped') == 'stepped' and os.environ.get('COLCANARY_FLAT', '1') != '1' and os.environ.get('COLCANARY_TROUGH', '0') != '1':
                # STEPPED RAMP: walls with sloped top edges hung the game (prism);
                # a sloped top inside vertical rectangular walls did not (trough).
                # So the side walls become N vertical rectangles with HORIZONTAL
                # tops that follow the slope (top = slope height at the step's
                # uphill edge; the <95-unit lip per step is below capsule size).
                N = int(os.environ.get('COLCANARY_STEPS', '8'))
                def hz(z):
                    return h_z0 + (h_z1 - h_z0) * (z - z0) / (z1 - z0)
                quads = [(e, f, g, h), (a, b, c, d), (a, b, f, e), (d, c, g, h)]   # top, bottom, tall end, short end
                for i in range(N):
                    za, zb = z0 + (z1 - z0) * i / N, z0 + (z1 - z0) * (i + 1) / N
                    top = max(hz(za), hz(zb))
                    quads.append((P(x0, yb, za), P(x0, yb, zb), P(x0, top, zb), P(x0, top, za)))   # -X side step
                    quads.append((P(x1, yb, za), P(x1, yb, zb), P(x1, top, zb), P(x1, top, za)))   # +X side step
            if os.environ.get('COLCANARY_TROUGH', '0') == '1':
                # TROUGH control: sloped top, but every wall is a full-height rectangle
                # (horizontal top edges), so the slope sits inside vertical rails.
                E, F, G, H = P(x0, h_z0, z0), P(x1, h_z0, z0), P(x1, h_z0, z1), P(x0, h_z0, z1)
                quads = [(e, f, g, h), (a, b, c, d), (a, b, F, E), (d, c, G, H), (b, c, G, F), (a, d, H, E)]
            ctr = ((x0 + x1) / 2, (yb + h_z0) / 2, (z0 + z1) / 2)
            ATTR_FLOOR, ATTR_WALL, ATTR_SCEN = 0x0020a000834, 0x00208202830, 0x00100840dc2
            for qi, q in enumerate(quads):
                for tri in ((q[0], q[1], q[2]), (q[0], q[2], q[3])):
                    t = AttrFace(list(tri), None, 0, ATTR_SCEN, BOX_NAME)
                    cen = [sum(v[k] for v in tri) / 3 for k in range(3)]
                    if sum((cen[k] - ctr[k]) * t.normal[k] for k in range(3)) < 0:
                        tri = (tri[0], tri[2], tri[1])           # flip to outward
                    # per-face material word (row +0x06): stock NEVER leaves it 0 on a
                    # sloped character face; BB's wedge uses material 1 (0x1040 slope,
                    # 0x0040 wall, 0x2040 scenery). Env COLCANARY_WORDS=0 to drop.
                    words = os.environ.get('COLCANARY_WORDS', '1') == '1'
                    for at in ((ATTR_FLOOR if qi == 0 else ATTR_WALL), ATTR_SCEN):
                        wword = 0
                        if words:
                            wword = 0x2040 if at == ATTR_SCEN else (0x1040 if at == ATTR_FLOOR else 0x0040)
                        box.append(AttrFace(list(tri), None, wword, at, BOX_NAME))
            log('TRANSPLANT: closed prism %d faces, x %.0f..%.0f z %.0f..%.0f, top %.0f -> %.0f' % (len(box), x0, x1, z0, z1, h_z0, h_z1))
            wd['faces'] = []
        for f in wd['faces']:
            vs = [tuple(v) for v in f['verts']]
            tris = [(vs[0], vs[1], vs[2])] + ([(vs[0], vs[2], vs[3])] if len(vs) == 4 else [])
            a = int(f['attr'])
            # Rail (bit 22) = climbable ledge; it hung the game on contact when
            # transplanted (2026-09-12 19:36) -- ledge data is per-map and keyed
            # elsewhere. Strip Rail|AttackGuard|Ik|Floor from wall words -> plain
            # blocking wall 0x208202830 (the value the working box used).
            if a == 0x208402034 and os.environ.get('COLCANARY_KEEP_RAIL', '1') != '1':
                a = 0x208202830
            nm = int(f['name'])
            if os.environ.get('COLCANARY_KEEP_NAMES', '1') != '1':
                nm = BOX_NAME        # foreign leaf names suspected in the 19:40 hangs; the box's name worked
            for t in tris:
                box.append(AttrFace(list(t), None, int(f.get('flags', 0)), a, nm))
        log('TRANSPLANT: %d wedge collision faces from %s replace the box (bbox %s..%s)'
            % (len(box), os.path.basename(wj), [round(x) for x in wd['bbox_lo']], [round(x) for x in wd['bbox_hi']]))
    # the block must not straddle a grid cell: walls are stored once (centroid
    # cell) and a wall 94 units across the boundary was unreachable from the
    # neighbouring cell in game (2026-09-12, 'i didnt run into anything')
    import math as _m
    wmin = donor.payload.worlds[0].sub.world_min; csz = donor.payload.worlds[0].sub.cell_size
    cells = set()
    for f in box:
        for v in f.verts:
            cells.add(tuple(int(_m.floor((v[k] - wmin[k]) / csz[k])) for k in range(3)))
    log('box cells: %s' % sorted(cells))
    if len(cells) != 1 and not os.path.exists(wj):
        raise SystemExit('box straddles cells %s -- move it' % sorted(cells))
    log('box: %d faces, normals %s'
        % (len(box), sorted(set(tuple(int(round(c)) for c in f.normal) for f in box))))
    faces = faces + box

    st_t, st_r = link_reach_file(donor)
    log('link gate, stock control: %d/%d POLY leaves reachable by links' % (st_r, st_t))
    # OWNERSHIP MODE. 'ground' (default): floor-ish faces (|ny|>0.5) are stored
    # in EVERY XZ cell they cover, walls once -- the engine looks for ground in
    # the cell you stand in (geom_build_chain measured 204/250 exact-cell on
    # stock), so a single-owner floor face straddling a cell edge is a hole.
    # In game 2026-09-12: single-owner = "good but random holes in floors".
    mode = os.environ.get('COLCANARY_MODE', 'ground')
    owner = {'ground': 'ground', 'single': True, 'full': False}[mode]
    log('ownership mode: %s' % mode)
    world = FieldChainedWorld(faces, ignore_names=False, single_owner=owner)
    log('world: min %s cell %s dims %s'
        % (tuple(round(c, 1) for c in world.world_min), world.cell, world.dims))
    payload, nblocks, ncells, st = world.emit(base, wtrailer)
    log('section 0: %d B, %d blocks, %d/%d cells, %d leaves, max chain %d (%d over 10), %d faces'
        % (len(payload), nblocks, st['cells_used'], ncells, st['leaves'],
           st['max_chain'], st['chains_over_10'], st['faces_stored']))
    out, reloc = splice_payload(plain, entries, payload, pad_to=len(plain))
    # VISIBLE MARKER (user's idea 2026-09-12): relocate every PRP_CBOX cardboard
    # box spawn (31 records, one shared id 0x15275e) onto/around the block.
    # Spawn Y is ignored by the engine (objects drop to the surface beneath),
    # so boxes over the block footprint should come to rest ON the invisible
    # block, ~1 m up -- a visible collision proof -- and the ring marks the spot.
    CBOX_ID = 0x15275e
    g0 = geom_io.GeomFile(out, 'cbox')
    cb = [r for r in g0.instances if getattr(r, 'pos', None) is not None and r.id == CBOX_ID]
    import math as _m2
    spots = [(BOX_CX + dx, BOX_Y1 + 50.0, BOX_CZ + dz) for dx in (-500.0, 0.0, 500.0) for dz in (-500.0, 0.0, 500.0)]
    if os.path.exists(wj) and os.environ.get('COLCANARY_BOXES_ON_TOP', '0') != '1':
        spots = []           # wedge rounds: nothing resting on the slope, everything in the ring
    ring = max(0, len(cb) - len(spots))
    for i in range(ring):
        a = 2 * _m2.pi * i / max(1, ring)
        rr = float(os.environ.get('COLCANARY_RING', '3200' if os.path.exists(wj) else '1900'))
        # items must not straddle a collision edge: the prism is 3420 long, the
        # 1900 ring put two boxes across its end walls (hangs 2026-09-12 evening)
        spots.append((BOX_CX + rr * _m2.cos(a), 0.0, BOX_CZ + rr * _m2.sin(a)))
    for r, sp in zip(cb, spots):
        r.position = sp
    out2 = g0.to_bytes()
    assert len(out2) == len(out), 'instance edit changed size'
    chk = [r for r in geom_io.GeomFile(out2, 'chk').instances if getattr(r, 'pos', None) is not None and r.id == CBOX_ID]
    log('PRP_CBOX: %d records relocated (%d on the block top, %d in a ring r1900); verify first: %s'
        % (len(cb), min(9, len(cb)), ring, [round(x) for x in chk[0].pos[:3]]))
    out = out2
    log('spliced: %d B (== donor %s), relocated %s' % (len(out), len(out) == len(plain), reloc))

    g = geom_io.GeomFile(out, 'canary')
    rt = g.to_bytes() == out
    walked = sum(1 for b in g.payload.blocks if b.nodes() is not None)
    nf = _count_faces(g)
    pc = _pointer_check(out, [tuple(e) for e in g.header.entries])
    log('geom_io: roundtrip %s, blocks %d walked %d, faces %s, refs %s, routes %s'
        % (rt, len(g.payload.blocks), walked, nf, pc.get('refs'), pc.get('routes')))
    ok = (rt and walked == len(g.payload.blocks) and nf == st['faces_stored']
          and pc.get('refs') == dref.get('refs') and pc.get('routes') == dref.get('routes'))
    if not ok:
        raise SystemExit('GATE FAILED (geom_io / pointers)')
    lt, lr = link_reach_file(g)
    log('link gate: %d/%d POLY leaves reachable by links (%d blocks)' % (lr, lt, len(g.payload.blocks)))
    if lr != lt or lt != st['leaves']:
        raise SystemExit('GATE FAILED (link reach %d/%d, expected %d leaves)' % (lr, lt, st['leaves']))
    marker = struct.pack('>I', BOX_NAME)
    log('box marker leaves in output: %d' % out.count(marker))

    os.makedirs(OUT, exist_ok=True)
    plain_path = os.path.join(OUT, '%s_colcanary.plain.geom' % STAGE)
    open(plain_path, 'wb').write(out)
    tot, okf, _ = bp_reach.score(plain_path)
    log('bp_reach: %d/%d = %.2f%%' % (okf, tot, 100.0 * okf / max(1, tot)))
    if okf / float(tot) < 0.99:
        raise SystemExit('GATE FAILED (reachability)')

    enc_out = C.encrypt(out, KEYPATH) + trailer
    assert len(enc_out) == len(enc), (len(enc_out), len(enc))
    assert C.decrypt(enc_out, KEYPATH) == out
    enc_path = os.path.join(OUT, '%s_colcanary.geom' % STAGE)
    open(enc_path, 'wb').write(enc_out)
    log('encrypted: %s md5 %s (%d B, trailer carried, decrypt round-trip ok)  %.0fs'
        % (enc_path, md5(enc_out), len(enc_out), time.time() - t0))
    return enc_path, enc_out


def main(argv):
    enc_path, enc_out = build()
    if '--apply' not in argv:
        print('DRY RUN: game tree untouched. Re-run with --apply to install.')
        return 0
    bak = LIVE + '.bak-colcanary'
    if not os.path.exists(bak):
        shutil.copyfile(LIVE, bak)
    open(LIVE, 'wb').write(enc_out)
    print('installed -> %s' % LIVE)
    print('backup    -> %s' % os.path.basename(bak))
    subprocess.check_call([sys.executable, MANIFEST_SYNC, 'sync', STAGE], cwd=r'D:\mgo2work')
    if os.path.isdir(CACHE):
        shutil.rmtree(CACHE)
        print('purged stage cache %s' % CACHE)
    else:
        print('no stage cache to purge')
    print('RESTORE: copy %s back over the live file, then manifest_sync sync %s'
          % (os.path.basename(bak), STAGE))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
