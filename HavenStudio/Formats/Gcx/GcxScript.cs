using System;

namespace HavenStudio.Formats.Gcx;

public class GcxScript
{
    public byte[] Bytes { get; set; }

    public GcxScript(byte[] bytes) => Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
}