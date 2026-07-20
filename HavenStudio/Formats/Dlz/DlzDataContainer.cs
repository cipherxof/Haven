namespace HavenStudio.Formats.Dlz;

public class DlzDataContainer
{
    public int SizeCompressed;
    public int SizeDecompressed;
    public byte[] CompressedData;

    public DlzDataContainer(int sizeCompressed, int sizeDecompressed, byte[] compressedData)
    {
        SizeCompressed = sizeCompressed;
        SizeDecompressed = sizeDecompressed;
        CompressedData = compressedData;
    }
}