namespace HavenStudio.Formats.Qar;

public class QarEntry
{
    public int Info { get; set; }
    public string? Filename { get; set; }
    public byte[]? Data { get; set; }
}