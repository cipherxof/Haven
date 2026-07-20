using System;

namespace HavenStudio.Formats.Dar;

public class DarEntry
{
    public string Filename { get; set; }
    public byte[]? Bytes { get; set; }

    public DarEntry(string filename, byte[]? bytes)
    {
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
        Bytes = bytes;
    }
}