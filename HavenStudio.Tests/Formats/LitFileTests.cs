using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Formats;

public sealed class LitFileTests
{
    [Fact]
    public void Mpo_stage_fixture_parses_verified_layout()
    {
        using var stream = File.OpenRead(FixturePath("mo_st01_d.lt2"));

        var file = LitFile.Read(stream);

        Assert.Equal(LitVariant.Raw, file.Variant);
        Assert.False(file.BigEndian);
        Assert.Equal(81, file.Groups.Count);
        Assert.InRange(new Vector3(file.Direction.X, file.Direction.Y, file.Direction.Z).Length, 0.9999f, 1.0001f);
        Assert.Equal(new LitColor(200, 160, 60, 0), file.Color);
        Assert.Equal(new LitColor(30, 60, 45, 0), file.Ambient);
        Assert.Equal(0xF50u, file.Groups[0].LitOffset);
        Assert.Equal(80, file.Groups.Count(group => group.Type == 32));
        var pointGroup = Assert.Single(file.Groups, group => group.Type == 1);
        Assert.Equal(2, pointGroup.Lights.Count);
        var point = Assert.IsType<LitPointLight>(pointGroup.Lights[0]);
        Assert.Equal(new LitColor(100, 96, 48, 0), point.Color);
        Assert.Equal(625f, point.Range);
        Assert.Equal(1250f, point.ExtendedRange);
    }

    [Theory]
    [InlineData("mo_st01_d.lt2")]
    [InlineData("mo_st01_sky_d.lt2")]
    public void Mpo_fixtures_round_trip_byte_identically(string fixture)
    {
        var expected = File.ReadAllBytes(FixturePath(fixture));
        using var stream = new MemoryStream(expected, writable: false);

        var file = LitFile.Read(stream);

        Assert.Equal(expected, file.ToArray());
    }

    [Fact]
    public void Header_only_sky_fixture_parses_without_groups()
    {
        using var stream = File.OpenRead(FixturePath("mo_st01_sky_d.lt2"));

        var file = LitFile.Read(stream);

        Assert.Equal(LitVariant.Raw, file.Variant);
        Assert.False(file.BigEndian);
        Assert.Empty(file.Groups);
    }

    [Fact]
    public void Mgs4_stage_fixture_parses_real_prefixed_layout_and_round_trips()
    {
        var expected = File.ReadAllBytes(FixturePath("n012a.lt3"));
        using var stream = new MemoryStream(expected, writable: false);

        var file = LitFile.Read(stream);

        Assert.Equal(LitVariant.Prefixed, file.Variant);
        Assert.True(file.BigEndian);
        Assert.Equal(209, file.Groups.Count);
        var firstPoint = Assert.IsType<LitPointLight>(file.Groups[0].Lights[0]);
        Assert.Equal(6000f, firstPoint.Range);
        Assert.Equal(6000f, firstPoint.ExtendedRange);
        Assert.Equal(0x200u, firstPoint.Flag);
        var blackPoint = Assert.IsType<LitBlackPoint>(Assert.Single(file.Groups, group => group.Type == 16).Lights[0]);
        Assert.Equal(3500f, blackPoint.Range);
        Assert.Equal(0x200u, blackPoint.Flag);
        Assert.Equal(160, Assert.IsType<LitRawLight>(file.Groups.First(group => group.Type == 64).Lights[0]).Data.Length);
        Assert.Equal(160, Assert.IsType<LitRawLight>(file.Groups.First(group => group.Type == 320).Lights[0]).Data.Length);
        Assert.Equal(expected, file.ToArray());
    }

    [Fact]
    public void Mgs4_single_field_edit_only_changes_the_typed_field_bytes()
    {
        var expected = File.ReadAllBytes(FixturePath("n012a.lt3"));
        using var stream = new MemoryStream(expected, writable: false);
        var file = LitFile.Read(stream);
        var point = Assert.IsType<LitPointLight>(file.Groups[0].Lights[0]);
        point.ExtendedRange = 6500f;

        var actual = file.ToArray();

        var changed = expected.Zip(actual).Select((pair, index) => (pair, index))
            .Where(item => item.pair.First != item.pair.Second)
            .Select(item => item.index)
            .ToArray();
        Assert.NotEmpty(changed);
        Assert.All(changed, index => Assert.InRange(index, (int)file.Groups[0].LitOffset + 28, (int)file.Groups[0].LitOffset + 31));
    }

    [Fact]
    public void Synthetic_prefixed_big_endian_file_round_trips()
    {
        var file = new LitFile
        {
            Variant = LitVariant.Prefixed,
            BigEndian = true,
            Prefix = Convert.FromHexString("4C495433000000031122334455667788"),
            Direction = new Vector4(0, -1, 0, 0.5f),
            Color = new LitColor(200, 180, 160, 128),
            Ambient = new LitColor(20, 30, 40, 0),
            HeaderPad = 0x10203040
        };
        var pointGroup = new LitGroup
        {
            BoundsMin = new Vector4(-100, -100, -100, 0),
            BoundsMax = new Vector4(100, 100, 100, 0),
            Type = 1,
            Pad = 0xAABBCCDD
        };
        pointGroup.Lights.Add(new LitPointLight
        {
            Point = new Vector4(1, 2, 3, 0.25f),
            Color = new LitColor(10, 20, 30, 40),
            Range = 25,
            ExtendedRange = 50,
            Flag = 0x200,
            VariantExtra = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray()
        });
        file.Groups.Add(pointGroup);
        var rawGroup = new LitGroup { Type = 64 };
        rawGroup.Lights.Add(new LitRawLight(Enumerable.Range(0, 352).Select(value => (byte)value).ToArray()));
        file.Groups.Add(rawGroup);

        var bytes = file.ToArray();
        using var stream = new MemoryStream(bytes, writable: false);
        var restored = LitFile.Read(stream);

        Assert.Equal(LitVariant.Prefixed, restored.Variant);
        Assert.True(restored.BigEndian);
        Assert.Equal(file.Prefix, restored.Prefix);
        Assert.Equal(2, restored.Groups.Count);
        Assert.Equal(Enumerable.Range(0, 16).Select(value => (byte)value),
            Assert.IsType<LitPointLight>(restored.Groups[0].Lights[0]).VariantExtra);
        Assert.Equal(352, Assert.IsType<LitRawLight>(restored.Groups[1].Lights[0]).Data.Length);
        Assert.Equal(bytes, restored.ToArray());
    }

    [Fact]
    public void Writer_recomputes_offsets_when_early_group_grows()
    {
        using var stream = File.OpenRead(FixturePath("mo_st01_d.lt2"));
        var file = LitFile.Read(stream);
        var first = file.Groups[0];
        first.Lights.Add(Clone(Assert.IsType<LitParallelLight>(first.Lights[0])));

        var rewritten = file.ToArray();
        using var output = new MemoryStream(rewritten, writable: false);
        var restored = LitFile.Read(output);

        Assert.Equal(2, restored.Groups[0].Lights.Count);
        Assert.Equal(file.Groups[0].LitOffset + 128u, restored.Groups[1].LitOffset);
    }

    [Theory]
    [MemberData(nameof(MalformedInputs))]
    public void Malformed_input_returns_clean_error(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);

        var success = LitFile.TryRead(stream, out var file, out var error);

        Assert.False(success);
        Assert.Null(file);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    public static TheoryData<byte[]> MalformedInputs => new()
    {
        Array.Empty<byte>(),
        new byte[31],
        Enumerable.Repeat((byte)0xFF, 96).ToArray()
    };

    private static LitParallelLight Clone(LitParallelLight source) => new()
    {
        BoundsMax = source.BoundsMax,
        BoundsMin = source.BoundsMin,
        Direction = source.Direction,
        Color = source.Color,
        Ambient = source.Ambient,
        Force = source.Force,
        Flag = source.Flag,
        VariantExtra = source.VariantExtra.ToArray()
    };

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Lit", name);
}
