using System;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public static class MdnFile
{
    public const int Magic = 0x4D444E20; // "MDN "

    public static Mdn Read(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        var mdn = new Mdn();
        mdn.ReadFrom(stream);
        return mdn;
    }

    public static void Write(Stream stream, Mdn mdn)
    {
        if (mdn is null) throw new ArgumentNullException(nameof(mdn));
        var endianness = mdn.BigEndian ? Endianness.Big : Endianness.Little;
        Write(stream, mdn, endianness);
    }

    public static void Write(Stream stream, Mdn mdn, Endianness endianness)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        if (mdn is null) throw new ArgumentNullException(nameof(mdn));

        mdn.WriteTo(stream, endianness);
    }
}
