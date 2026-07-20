using System;

namespace HavenStudio.Utils;

public static class GeomUtils
{
    /// <summary>
    /// Encodes four zero-based GEOM vertex indices into four low bytes and the
    /// packed two-bit high parts stored in the fifth polygon byte.
    /// </summary>
    public static void EncodeFaceIndices(
        int a,
        int b,
        int c,
        int d,
        Span<byte> destination)
    {
        if (destination.Length < 5)
        {
            throw new ArgumentException("Face index encoding needs five bytes.", nameof(destination));
        }

        Validate(a, nameof(a));
        Validate(b, nameof(b));
        Validate(c, nameof(c));
        Validate(d, nameof(d));
        destination[0] = (byte)a;
        destination[1] = (byte)b;
        destination[2] = (byte)c;
        destination[3] = (byte)d;
        destination[4] = (byte)(
            (a >> 8) |
            (b >> 8) << 2 |
            (c >> 8) << 4 |
            (d >> 8) << 6);

        static void Validate(int value, string parameter)
        {
            if ((uint)value > 1023)
            {
                throw new ArgumentOutOfRangeException(parameter, value, "GEOM polygon indices must be in 0..1023.");
            }
        }
    }

    /// <summary>
    /// Applies the four packed two-bit high parts to one-based low-byte indices.
    /// The former implementation listed observed flag-byte combinations manually;
    /// the debug format and corpus prove this is a regular four-by-two-bit field.
    /// </summary>
    public static void FaceBitCalculation(
        int extraBit,
        ref int fa,
        ref int fb,
        ref int fc,
        ref int fd)
    {
        fa += (extraBit & 0x03) << 8;
        fb += ((extraBit >> 2) & 0x03) << 8;
        fc += ((extraBit >> 4) & 0x03) << 8;
        fd += ((extraBit >> 6) & 0x03) << 8;
    }
}
