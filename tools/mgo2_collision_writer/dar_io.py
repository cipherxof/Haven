# -*- coding: utf-8 -*-
"""MGO2 .dar (name-keyed asset archive) reader/writer.

Layout, tiled to the byte on o/stage/r_sna01_n/resident.dar (568 entries,
2026-09-05):

    u32 BE count
    per entry: name, 0x00, zero-pad to 4, u32 BE size, zero-pad to 16,
               payload[size], 0x00   (one separator after EVERY payload, last included)

No index or hash table -- the game finds entries by name, so adding an asset is
appending one record and bumping the count. The on-disk file is stage-encrypted
(mgo2_stage_crypto, key path stage/<folder>) with a carried trailer; this module
works on the DECRYPTED bytes only.

    python dar_io.py <resident.dar.dec>            # list + round-trip check
    python dar_io.py <in.dec> <out.dec> add <name>=<file> ...   # append/replace entries
"""
import struct, sys, os


def parse(data):
    count = struct.unpack_from('>I', data, 0)[0]
    pos = 4
    ents = []
    for i in range(count):
        # every payload (and the count) is followed by one 0x00 before the next name
        if i > 0:
            if data[pos:pos + 1] != b'\x00':
                raise ValueError('entry %d: missing separator at 0x%x' % (i, pos))
            pos += 1
        e = data.index(b'\x00', pos)
        name = data[pos:e].decode('ascii')
        sp = (e + 1 + 3) & ~3
        size = struct.unpack_from('>I', data, sp)[0]
        pay = (sp + 4 + 15) & ~15
        ents.append({'name': name, 'data': data[pay:pay + size]})
        pos = pay + size
    if pos + 1 != len(data):
        raise ValueError('archive did not tile: walked to 0x%x, file is 0x%x' % (pos, len(data)))
    return ents


def build(ents):
    out = bytearray(struct.pack('>I', len(ents)))
    for i, e in enumerate(ents):
        if i > 0:
            out += b'\x00'
        out += e['name'].encode('ascii') + b'\x00'
        while len(out) % 4:
            out += b'\x00'
        out += struct.pack('>I', len(e['data']))
        while len(out) % 16:
            out += b'\x00'
        out += e['data']
    out += b'\x00'
    return bytes(out)


def main():
    src = sys.argv[1]
    data = open(src, 'rb').read()
    ents = parse(data)
    rebuilt = build(ents)
    print('%s: %d entries, round-trip byte-identical: %s' % (os.path.basename(src), len(ents), rebuilt == data))
    if len(sys.argv) >= 4 and sys.argv[3] == 'add':
        names = {e['name']: e for e in ents}
        for spec in sys.argv[4:]:
            name, _, path = spec.partition('=')
            blob = open(path, 'rb').read()
            if name in names:
                names[name]['data'] = blob
                print('  replaced %s (%d B)' % (name, len(blob)))
            else:
                ents.append({'name': name, 'data': blob})
                print('  appended %s (%d B)' % (name, len(blob)))
        out = build(ents)
        assert parse(out) and len(parse(out)) == len(ents)
        open(sys.argv[2], 'wb').write(out)
        print('-> %s (%d B, %d entries)' % (sys.argv[2], len(out), len(ents)))


if __name__ == '__main__':
    main()
