namespace HavenStudio.Formats.Gcx;

public class GcxCipher
{
    private readonly int _seed;
    private int _key;

    public GcxCipher(int seed)
    {
        _seed = seed;
        _key = seed;
    }

    private byte NextByte()
    {
        // key = key * 0x7d2b89dd + 0xcf9; return (byte)(key >> 15); :contentReference[oaicite:25]{index=25}
        unchecked
        {
            _key = _key * (int)0x7D2B89DD + 0xCF9;
            return (byte)(_key >> 15);
        }
    }

    public void Reset() => _key = _seed;

    public byte Decrypt(byte b) => (byte)(b ^ NextByte());
    public byte Encrypt(byte b) => (byte)(b ^ NextByte());
}