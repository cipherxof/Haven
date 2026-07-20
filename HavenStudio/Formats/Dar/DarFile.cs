using System;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dar;

public class DarFile
{
    public static Dar Read(Stream stream, Endianness endianness = Endianness.Big)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        using var r = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        var dar = new Dar();
        dar.ReadFrom(r);
        return dar;
    }

    public static void Write(Stream stream, Dar dar, Endianness endianness = Endianness.Big)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (dar is null) throw new ArgumentNullException(nameof(dar));

        using var w = new EndianBinaryWriter(stream, endianness, leaveOpen: true);
        dar.WriteTo(w);
        w.Flush();
    }
}
