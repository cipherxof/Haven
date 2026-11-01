using System.Buffers.Binary;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Pdl;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public sealed class VegetationEditingTests
{
    [Fact]
    public void PdlWritePositionsPreservesHeaderScaleAndUneditedGroup()
    {
        var bytes = BuildPdl([2, 1],
            [(new Vector3(1, 2, 3), 1f), (new Vector3(4, 5, 6), 0.5f), (new Vector3(7, 8, 9), 2f)]);
        var document = PdlDocument.Read(bytes);
        var beforeGroupOne = bytes.AsSpan(0x40, 0x20).ToArray();
        document.Groups[1].Instances[0].Position += new Vector3(10, 20, 30);

        var output = document.WritePositions();

        Assert.Equal(bytes.Length, output.Length);
        Assert.Equal(bytes.AsSpan(0, 0x40).ToArray(), output.AsSpan(0, 0x40).ToArray());
        Assert.Equal(beforeGroupOne, output.AsSpan(0x40, 0x20).ToArray());
        Assert.Equal(bytes.AsSpan(0x40 + 0x20 + 12, 4).ToArray(), output.AsSpan(0x40 + 0x20 + 12, 4).ToArray());
        Assert.Equal(new Vector3(17, 28, 39), document.Groups[1].Instances[0].Position);
    }

    [Fact]
    public void ScannerLinksNewGrassModelToPdl()
    {
        const string script = """
            NewGrassMng_03 [1F13C5][1]
              -m[01C0EC] [2A4274]
              -z GRAS_B
              -s[6311EC] 30 90
              -p[01CCEC] [E0C049]
            NewGrassMng_03 [1BFFC7][1]
              -m[01C0EC] [334AAE]
              -p[01CCEC] [E0C048]
            """;

        var references = GcxVegetationReferenceScanner.Scan([script]);
        Assert.Equal(2, references.Count);
        var reference = references.Single(candidate => candidate.PdlHash == 0xE0C049u);

        Assert.Equal(0x2A4274u, reference.ModelHash);
        Assert.Equal(0xE0C049u, reference.PdlHash);
        Assert.Equal(0.3f, reference.MinimumScale);
        Assert.Equal(0.9f, reference.MaximumScale);
    }

    [Fact]
    public void ScannerLinksVegetationByWireHashWithoutResolvedCommandName()
    {
        const string script = """
            [6592A7] [1F13C5][1]
              -m[01C0EC] [2A4274]
              -s[6311EC] 90 110
              -p[01CCEC] [E0C049]
            """;

        var reference = Assert.Single(GcxVegetationReferenceScanner.Scan([script]));

        Assert.Equal(0x2A4274u, reference.ModelHash);
        Assert.Equal(0xE0C049u, reference.PdlHash);
        Assert.Equal(0.9f, reference.MinimumScale);
        Assert.Equal(1.1f, reference.MaximumScale);
    }

    [Fact]
    public void StructuralScannerDoesNotDependOnDecompiledCommandNames()
    {
        var site = new GcxPlacementSite
        {
            CommandHash = 0x1E20BF,
            CommandName = "[1E20BF]",
            CommandOffset = 12,
            CommandLength = 30,
            VegetationModelHash = 0x2A4274,
            VegetationPdlHash = 0xE0C049,
            VegetationScaleValues = [90, 110]
        };

        var reference = Assert.Single(GcxVegetationReferenceScanner.Scan([site]));

        Assert.Equal(0x2A4274u, reference.ModelHash);
        Assert.Equal(0xE0C049u, reference.PdlHash);
        Assert.Equal(0.9f, reference.MinimumScale);
        Assert.Equal(1.1f, reference.MaximumScale);
    }

    [Fact]
    public async Task SameLengthDarReplacementChangesOnlyEntryPayload()
    {
        using var temp = new TempDirectory();
        var darPath = Path.Combine(temp.Path, "cache.dar");
        using (var stream = File.Create(darPath))
        {
            var dar = new Dar();
            dar.Entries.Add(new DarEntry("before.bin", [1, 2, 3, 4]));
            dar.Entries.Add(new DarEntry("grass_test.pdl", [11, 22, 33, 44, 55, 66]));
            dar.Entries.Add(new DarEntry("after.bin", [7, 8, 9]));
            DarFile.Write(stream, dar, Endianness.Big);
        }
        var original = File.ReadAllBytes(darPath);
        var catalog = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await catalog.ScanAsync();
        var path = snapshot.WithExtension(".pdl").Single().Path;
        byte[] replacement = [60, 50, 40, 30, 20, 10];

        catalog.Replace(path, replacement);

        var edited = File.ReadAllBytes(darPath);
        Assert.Equal(original.Length, edited.Length);
        var payloadOffset = FindSequence(original, [11, 22, 33, 44, 55, 66]);
        var expected = (byte[])original.Clone();
        replacement.CopyTo(expected, payloadOffset);
        Assert.Equal(expected, edited);
        Assert.Equal(replacement, catalog.ReadAllBytes(path));
    }

    private static byte[] BuildPdl(int[] counts, (Vector3 Position, float Scale)[] records)
    {
        var bytes = new byte[PdlDocument.HeaderSize + records.Length * PdlDocument.RecordSize];
        for (var index = 0; index < counts.Length; index++)
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(index * 4, 4), counts[index]);
        for (var index = 0; index < records.Length; index++)
        {
            var offset = PdlDocument.HeaderSize + index * PdlDocument.RecordSize;
            WriteSingle(bytes, offset, records[index].Position.X);
            WriteSingle(bytes, offset + 4, records[index].Position.Y);
            WriteSingle(bytes, offset + 8, records[index].Position.Z);
            WriteSingle(bytes, offset + 12, records[index].Scale);
        }
        return bytes;
    }

    private static void WriteSingle(byte[] bytes, int offset, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));

    private static int FindSequence(byte[] haystack, byte[] needle)
    {
        for (var offset = 0; offset <= haystack.Length - needle.Length; offset++)
        {
            if (haystack.AsSpan(offset, needle.Length).SequenceEqual(needle))
                return offset;
        }
        throw new InvalidOperationException("Test payload was not found.");
    }
}
