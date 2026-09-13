"""MGO2 stage-file crypto: decrypt AND encrypt.

Derived from mgo2-map-tools/mgo2_crypto (decrypt-only). The cipher is a
per-qword XOR keystream combined with ciphertext chaining:

    dec[i] = enc[i] ^ ks[i % 56] ^ enc[i-1]        (enc[-1] = iv)

which inverts exactly:

    enc[i] = dec[i] ^ ks[i % 56] ^ enc[i-1]

The keystream is derived from the file's stage-relative path (e.g.
"stage/n002a"), so a file must be encrypted under the path it will live at.
"""
import hashlib
import os
import struct

from Crypto.Cipher import Blowfish

_PTSYS = {
    0: "74F66DC28598F5D172AC2DCACE5544D665F11D05BEA20568E76C529DEB35890E"
       "C332FF24FE5D9C3FB34189CF47055B26F9E4CC639A46B5465404DF41E65B8E4E",
    3: "53085788720CC955D1A75FCA0A988CED84CFBA8BFDDA9A046AF0FB4DE027DC24"
       "B2B636110D27CA284E0AB15912212593B52D945C633A0B5397D41B64F70ED1EE",
    6: "24B0DB82FD2366D428BEF3BE915BFDFF998EBFC4B549C1ECBB8A633CF5704600"
       "1DE1AD7CBE8381DBEF1342768E3D629F2059AE61B4DFFB1DA2FF1A0264487E7A",
}

BLOCK = 8
TRAILER = 0x10


def stage_key_path(path):
    """Return the key-derivation path, e.g. 'stage/n002a', from a full path.

    MEASURED 2026-09-11: the `o/dl/p/` prefix is IGNORED by the key derivation --
    `o/dl/p/stage/n001a/cache.dar` decrypts under `stage/n001a`, byte-identically
    to how the base tree works. This function used to require "/o/stage/" and
    RAISE on every dl/p path, which left 77 of the game's 101 .dar archives
    unopened and produced a family of wrong "that asset does not exist"
    conclusions (the MGL-140 HUD icon among them). Match the LAST "/stage/"
    instead, so both trees work.
    """
    p = str(path).replace("\\", "/")
    i = p.rfind("/stage/")
    if i < 0:
        raise ValueError("path must contain /stage/: " + p)
    p = p[i + 1:]
    return p[:p.rfind("/")]


def keystream(key_path):
    """56-byte XOR keystream + 8-byte initial IV, derived from key_path."""
    digest = hashlib.md5(key_path.encode("utf-8")).digest()
    key = bytes.fromhex(_PTSYS[0])
    ciphertext = bytes.fromhex(_PTSYS[3])
    sbox = Blowfish.new(key[8:], Blowfish.MODE_CBC, key[:8]).decrypt(ciphertext)
    stream = bytes(sbox[i] ^ digest[i & 0xF] for i in range(0x40))
    iv = struct.unpack(">Q", stream[:8])[0]
    return stream[8:], iv


def decrypt(data, key_path):
    ks, iv = keystream(key_path)
    body = data[:-TRAILER]
    out = bytearray()
    pos = 0
    dec = 0
    n = len(body)
    off = 0
    while off + BLOCK < n:
        enc = struct.unpack_from(">Q", body, off)[0]
        xk = struct.unpack_from(">Q", ks, pos)[0]
        dec = enc ^ xk ^ iv
        iv = enc
        out += struct.pack(">Q", dec)
        pos = (pos + BLOCK) % len(ks)
        off += BLOCK
        if off + BLOCK > n:
            pad = dec & 0xFF
            if pad <= BLOCK:
                del out[len(out) - pad:]
    return bytes(out)


def encrypt(plain, key_path):
    """Inverse of decrypt(). Reproduces the original ciphertext byte-exactly
    when given the original plaintext (verified by round trip)."""
    ks, iv = keystream(key_path)
    pad = BLOCK - (len(plain) % BLOCK)   # PKCS7: always 1..8, never 0
    body = plain + bytes([pad]) * pad
    out = bytearray()
    pos = 0
    for off in range(0, len(body), BLOCK):
        dec = struct.unpack_from(">Q", body, off)[0]
        enc = dec ^ struct.unpack_from(">Q", ks, pos)[0] ^ iv
        iv = enc
        out += struct.pack(">Q", enc)
        pos = (pos + BLOCK) % len(ks)
    return bytes(out)


def decrypt_file(path):
    return decrypt(open(path, "rb").read(), stage_key_path(path))


def repack(plain, key_path, trailer):
    """Full container: encrypted PKCS7 body + the 16-byte trailer.

    The trailer's meaning is UNIDENTIFIED (not md5 of plaintext, ciphertext,
    or path, plain or encrypted). Pass the ORIGINAL file's trailer through
    unchanged until it is identified.
    """
    if len(trailer) != TRAILER:
        raise ValueError("trailer must be %d bytes" % TRAILER)
    return encrypt(plain, key_path) + trailer
