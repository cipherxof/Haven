using System;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Qar;

public class QarFile
{
    public static Qar Read(Stream stream, Endianness endianness = Endianness.Big)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("QAR reading requires a seekable stream.", nameof(stream));

        using var r = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        var qar = new Qar();
        qar.ReadFrom(r);
        return qar;
    }

    public static void Write(Stream stream, Qar qar, Endianness endianness = Endianness.Big)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (qar is null) throw new ArgumentNullException(nameof(qar));
        if (!stream.CanSeek) throw new ArgumentException("QAR writing requires a seekable stream.", nameof(stream));

        using var w = new EndianBinaryWriter(stream, endianness, leaveOpen: true);
        qar.WriteTo(w);
        w.Flush();
    }
}
