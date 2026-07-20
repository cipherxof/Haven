using HavenStudio.Formats.Dds;
using HavenStudio.Tests.TestSupport;
using HavenStudio.Utils;

namespace HavenStudio.Tests.Utilities;

public sealed class UtilityTests
{
    [Theory]
    [InlineData("", 0u)]
    [InlineData("a", 97u)]
    [InlineData("abc", 102563u)]
    public void String_hash_matches_known_vectors(string value, uint expected)
    {
        Assert.Equal(expected, HavenStudio.Utils.String.HashString(value));
    }

    [Fact]
    public void Deflate_round_trip_preserves_payload()
    {
        var input = Enumerable.Range(0, 4097).Select(i => (byte)(i % 239)).ToArray();

        var compressed = Compression.DeflateBuffer(input);
        var restored = Compression.InflateBuffer2(compressed, input.Length);

        Assert.NotEmpty(compressed);
        Assert.Equal(input, restored);
    }

    [Fact]
    public void Crypto_service_encrypt_decrypt_round_trip_preserves_payload()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.GetPath("payload.bin");
        var input = Enumerable.Range(0, 73).Select(i => (byte)(i * 3)).ToArray();
        File.WriteAllBytes(sourcePath, input);
        var service = new CryptoService();

        service.Encrypt(sourcePath, "fixture-key");
        service.Decrypt(sourcePath + ".enc", "fixture-key");

        var encrypted = File.ReadAllBytes(sourcePath + ".enc");
        Assert.Equal(97, encrypted.Length);
        Assert.Equal(
            Convert.FromHexString("773179FB0DE1CB1F21E975C52EEEFDD5802B21D7"),
            encrypted.AsSpan(0, 20).ToArray());
        Assert.Equal(encrypted, service.Encrypt(input, "fixture-key"));
        Assert.Equal(input, service.Decrypt(encrypted, "fixture-key"));
        Assert.Equal(input, File.ReadAllBytes(sourcePath + ".enc.dec"));
    }

    [Fact]
    public void Dxt1_known_red_block_decodes_to_opaque_red_pixels()
    {
        var block = new byte[]
        {
            0x00, 0xF8,
            0xE0, 0x07,
            0x00, 0x00, 0x00, 0x00
        };

        var rgba = DxtDecoder.DecodeToRgba(4, 4, "DXT1", block);

        Assert.Equal(4 * 4 * 4, rgba.Length);
        for (var i = 0; i < rgba.Length; i += 4)
        {
            Assert.Equal(255, rgba[i]);
            Assert.Equal(0, rgba[i + 1]);
            Assert.Equal(0, rgba[i + 2]);
            Assert.Equal(255, rgba[i + 3]);
        }
    }

    [Fact]
    public void Dxt_decoder_rejects_truncated_block_data()
    {
        Assert.Throws<InvalidDataException>(() =>
            DxtDecoder.DecodeToRgba(4, 4, "DXT5", new byte[15]));
    }

    [Fact]
    public void Gcx_command_builder_matches_known_new_camera_opcode()
    {
        var command = GcxCommandBuilder.BuildCommand(
            GcxCommandType.NewCamera,
            new Dictionary<string, object>());

        Assert.Equal(
            Convert.FromHexString("6D0DA792650806DE1A06068C563D00"),
            command);
    }
}
