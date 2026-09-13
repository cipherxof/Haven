# -*- coding: utf-8 -*-
"""gltf_faces -- read a glTF 2.0 file into geom_build.Face objects.

The bridge between Haven (cipherxof's stage editor, which exports/imports
collision as glTF) and our own cell-first collision writer.  Standard library
only: json + struct + base64.

WHAT IS CARRIED
---------------
* Every TRIANGLES primitive (mode 4, the default) with a float3 POSITION and
  u8/u16/u32 indices (or no indices).  Node transforms (matrix or TRS) are
  applied, so a Blender scene with placed objects comes out in world space.
* The primitive's material name, Haven's convention: ``attr_0x<hex>`` where
  <hex> is the leaf node's 64-bit collision ATTRIBUTE (leaf +0x18/+0x1c as one
  big-endian u64; e.g. ``attr_0x100840DC2`` = hi32 1, lo32 0x00840DC2, a plain
  floor on Ambush Alley).  A Blender ``.001`` duplicate suffix is ignored, the
  hex is case-insensitive, both exactly as Haven's own importer parses it.
  Materials that do not match get attribute 0 and are counted in the report.
* Haven's per-primitive ``extras.nameHash`` (leaf +0x10, the object/group hash)
  when present.

WHAT IS NOT CARRIED (measured on Haven's export of stock n002a, 2026-09-12)
* The per-face u16 surface-material word (row +0x06).  glTF has no per-face
  channel and Haven does not export it, so every face gets 0.  Base-stage geoms
  carry real materials there (96,107 / 132,334 non-zero on n002a); dl/p
  overlay geoms are all-zero anyway.
* Quads.  Haven triangulates, so a stock quad arrives as two triangles.

Degenerate triangles (a repeated vertex, or zero area) are dropped and counted.
"""
import base64
import io
import json
import math
import os
import re
import struct

from geom_build import Face

_ATTR_RE = re.compile(r'^attr_0x([0-9A-Fa-f]{1,16})(?:\..*)?$', re.IGNORECASE)

_COMPONENT = {
    5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2), 5123: ('H', 2),
    5125: ('I', 4), 5126: ('f', 4),
}
_NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


class AttrFace(Face):
    """A Face that also remembers its 64-bit leaf attribute and name hash."""
    __slots__ = ('attr', 'name')

    def __init__(self, verts, normal=None, flags=0, attr=0, name=None):
        Face.__init__(self, verts, normal, flags)
        self.attr = int(attr) & 0xFFFFFFFFFFFFFFFF
        self.name = None if name is None else int(name) & 0xFFFFFFFF


def parse_attr_name(name):
    """attr_0x<hex>[.suffix] -> int, or None when the name is not one."""
    if not name:
        return None
    m = _ATTR_RE.match(name.strip())
    return int(m.group(1), 16) if m else None


def _mat_mul(a, b):
    """4x4 column-major (glTF) matrix product a*b."""
    out = [0.0] * 16
    for c in range(4):
        for r in range(4):
            out[c * 4 + r] = sum(a[k * 4 + r] * b[c * 4 + k] for k in range(4))
    return out


def _mat_apply(m, v):
    x, y, z = v
    return (m[0] * x + m[4] * y + m[8] * z + m[12],
            m[1] * x + m[5] * y + m[9] * z + m[13],
            m[2] * x + m[6] * y + m[10] * z + m[14])


def _trs_matrix(node):
    if 'matrix' in node:
        return [float(c) for c in node['matrix']]
    t = node.get('translation', [0.0, 0.0, 0.0])
    q = node.get('rotation', [0.0, 0.0, 0.0, 1.0])
    s = node.get('scale', [1.0, 1.0, 1.0])
    x, y, z, w = q
    r = [1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0.0,
         2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0.0,
         2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0.0,
         0.0, 0.0, 0.0, 1.0]
    for c in range(3):
        for k in range(3):
            r[c * 4 + k] *= s[c]
    r[12], r[13], r[14] = t
    return r


_IDENTITY = [1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0,
             0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0]


class _Gltf(object):
    def __init__(self, path):
        self.path = path
        with open(path, 'rb') as fp:
            raw = fp.read()
        self.glb_bin = None
        if raw[:4] == b'glTF':
            self.doc = self._parse_glb(raw)
        else:
            self.doc = json.loads(raw.decode('utf-8'))
        self._buffers = {}

    def _parse_glb(self, raw):
        magic, version, length = struct.unpack_from('<III', raw, 0)
        o, doc = 12, None
        while o + 8 <= min(length, len(raw)):
            clen, ctype = struct.unpack_from('<II', raw, o)
            chunk = raw[o + 8:o + 8 + clen]
            if ctype == 0x4E4F534A:        # JSON
                doc = json.loads(chunk.decode('utf-8'))
            elif ctype == 0x004E4942:      # BIN
                self.glb_bin = chunk
            o += 8 + clen
        if doc is None:
            raise ValueError('GLB has no JSON chunk')
        return doc

    def buffer(self, i):
        if i in self._buffers:
            return self._buffers[i]
        b = self.doc['buffers'][i]
        uri = b.get('uri')
        if uri is None:
            data = self.glb_bin
        elif uri.startswith('data:'):
            data = base64.b64decode(uri.split(',', 1)[1])
        else:
            p = os.path.join(os.path.dirname(os.path.abspath(self.path)), uri)
            with open(p, 'rb') as fp:
                data = fp.read()
        if data is None or len(data) < b['byteLength']:
            raise ValueError('buffer %d short: need %d have %d'
                             % (i, b['byteLength'], 0 if data is None else len(data)))
        self._buffers[i] = data
        return data

    def accessor(self, i):
        """Decode accessor i into a list of tuples (or scalars)."""
        a = self.doc['accessors'][i]
        fmt, csize = _COMPONENT[a['componentType']]
        n = _NCOMP[a['type']]
        count = a['count']
        if 'bufferView' not in a:
            return [tuple([0.0] * n) if n > 1 else 0] * count
        bv = self.doc['bufferViews'][a['bufferView']]
        data = self.buffer(bv['buffer'])
        base = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = bv.get('byteStride', n * csize)
        rec = struct.Struct('<' + fmt * n)
        out = []
        for k in range(count):
            v = rec.unpack_from(data, base + k * stride)
            out.append(v if n > 1 else v[0])
        return out


def load(path, default_attr=0, warn=None):
    """Return (faces, report).  `warn` is an optional callable for messages."""
    g = _Gltf(path)
    doc = g.doc
    materials = doc.get('materials', [])
    mat_attr = {}
    unnamed = set()
    for mi, m in enumerate(materials):
        v = parse_attr_name(m.get('name'))
        if v is None:
            unnamed.add(mi)
            v = default_attr
        mat_attr[mi] = v

    rep = dict(primitives=0, triangles=0, degenerate=0, faces=0,
               unnamed_material_tris=0, no_material_tris=0,
               unnamed_materials=sorted(materials[i].get('name', '?') for i in unnamed),
               attrs={}, nodes=0, skipped_modes={})

    nodes = doc.get('nodes', [])
    scenes = doc.get('scenes')
    if scenes:
        scene = doc.get('scene', 0)
        roots = list(scenes[scene].get('nodes', []))
    else:
        children = set(c for nd in nodes for c in nd.get('children', []))
        roots = [i for i in range(len(nodes)) if i not in children]

    faces = []
    stack = [(i, _IDENTITY) for i in roots]
    seen = set()
    while stack:
        ni, parent = stack.pop()
        if ni in seen:
            continue
        seen.add(ni)
        node = nodes[ni]
        world = _mat_mul(parent, _trs_matrix(node))
        rep['nodes'] += 1
        for c in node.get('children', []):
            stack.append((c, world))
        if 'mesh' not in node:
            continue
        mesh = doc['meshes'][node['mesh']]
        for prim in mesh.get('primitives', []):
            mode = prim.get('mode', 4)
            if mode != 4:
                rep['skipped_modes'][mode] = rep['skipped_modes'].get(mode, 0) + 1
                continue
            if 'POSITION' not in prim.get('attributes', {}):
                continue
            rep['primitives'] += 1
            pos = [_mat_apply(world, p) for p in g.accessor(prim['attributes']['POSITION'])]
            if 'indices' in prim:
                idx = g.accessor(prim['indices'])
            else:
                idx = list(range(len(pos)))
            mi = prim.get('material')
            if mi is None:
                attr = default_attr
            else:
                attr = mat_attr.get(mi, default_attr)
            name = None
            ex = prim.get('extras') or {}
            nh = ex.get('nameHash')
            if isinstance(nh, str):
                try:
                    name = int(nh, 16)
                except ValueError:
                    name = None
            elif isinstance(nh, int):
                name = nh
            ntri = len(idx) // 3
            rep['triangles'] += ntri
            if mi is None:
                rep['no_material_tris'] += ntri
            elif mi in unnamed:
                rep['unnamed_material_tris'] += ntri
            for t in range(ntri):
                a, b, c = pos[idx[3 * t]], pos[idx[3 * t + 1]], pos[idx[3 * t + 2]]
                if a == b or b == c or a == c:
                    rep['degenerate'] += 1
                    continue
                ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
                vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
                nx = uy * vz - uz * vy
                ny = uz * vx - ux * vz
                nz = ux * vy - uy * vx
                if nx * nx + ny * ny + nz * nz == 0.0:
                    rep['degenerate'] += 1
                    continue
                faces.append(AttrFace([a, b, c], None, 0, attr, name))
                rep['attrs'][attr] = rep['attrs'].get(attr, 0) + 1
    rep['faces'] = len(faces)
    if warn:
        if rep['unnamed_materials']:
            warn('%d material(s) without attr_0xNNNN names -> attribute 0x%X: %s'
                 % (len(rep['unnamed_materials']), default_attr,
                    ', '.join(rep['unnamed_materials'][:8])))
        if rep['skipped_modes']:
            warn('skipped non-TRIANGLES primitives: %r' % rep['skipped_modes'])
    return faces, rep


if __name__ == '__main__':
    import sys
    fs, r = load(sys.argv[1], warn=lambda m: sys.stderr.write('WARN ' + m + '\n'))
    for k in ('nodes', 'primitives', 'triangles', 'degenerate', 'faces',
              'no_material_tris', 'unnamed_material_tris'):
        print('%-24s %d' % (k, r[k]))
    print('distinct attributes     %d' % len(r['attrs']))
    for a, n in sorted(r['attrs'].items(), key=lambda kv: -kv[1])[:12]:
        print('   attr_0x%X  %d' % (a, n))
