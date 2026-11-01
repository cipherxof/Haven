using System.Buffers.Binary;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Tests.Editors;

public sealed class GcxRaceExclusionWriterTests
{
    // Native JJ table procedure, including its non-RACE command and physical padding.
    private const string Native =
        "8E6A016D15C92B0804061CA0E15541C711A9C55542C811A9C400" +
        "6D1AC92B0804067F2CA55562C59232C15A6C2BB237C3C4C5C6C8C900" +
        "6D1BC92B0804067F2CA55562C59232C25B6C2BB237C3C5C7C9CACBC800" +
        "6D1BC92B0804067F2CA55562C59232C35B6C2BB237C2C3C4C5C6C7CC00" +
        "6D1AC92B0804067F2CA55562C59232C45A6C2BB237C1C3C4C6C7CC00" +
        "6D18C92B0804067F2CA55562C59232C5586C2BB237C2C3C6CB00" +
        "6D19C92B0804067F2CA55562C59232C6596C2BB237C1C3C4C5C800" +
        "6D1AC92B0804067F2CA55562C59232C75A6C2BB237C2C3C4CACBC800" +
        "6D1AC92B0804067F2CA55562C59232C85A6C2BB237C6C1C9CACBCC00" +
        "6D1BC92B0804067F2CA55562C59232C95B6C2BB237C2C5C7CACBC8CC00" +
        "6D1AC92B0804067F2CA55562C59232CA5A6C2BB237C2C4C7CBC8CC00" +
        "6D1BC92B0804067F2CA55562C59232CB5B6C2BB237C2C3C5C7C9CACC00" +
        "6D1BC92B0804067F2CA55562C59232CC5B6C2BB237C2C3C4C7CBC5CA0000000000000000";

    [Fact]
    public void Replaces_all_rows_and_preserves_other_command_and_padding()
    {
        var before = Convert.FromHexString(Native);
        IReadOnlyList<int>[] rows = Enumerable.Range(0,12).Select(i => (IReadOnlyList<int>)new[] {(i+1)%12}).ToArray();
        var after = GcxRaceExclusionWriter.Replace(before, rows);
        var text = GcxDecompiler.Decompile(after,"race");
        uint[] goals = [0x31E379,0x31E37A,0x8FB28D,0x8FB28E,0x8FB28F,0x8FB290,0x8FB291,0x8FB292,0x8FB293,0x8FB294,0x8FB295,0x8FB2AC];
        var decoded = GcxRaceGoalScanner.FindGoalLinks([text],goals);
        Assert.Equal(12,decoded.Count);
        for(int i=0;i<12;i++) Assert.Equal(goals[(i+1)%12],Assert.Single(decoded[goals[i]]));
        Assert.Equal(before.AsSpan(3,23).ToArray(),after.AsSpan(3,23).ToArray());
        int oldEnd=3+BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(1,2));
        int newEnd=3+BinaryPrimitives.ReadUInt16LittleEndian(after.AsSpan(1,2));
        Assert.Equal(before[oldEnd..],after[newEnd..]);
        Assert.Equal(Convert.FromHexString(Native),before); // caller's input is immutable
        Assert.Equal(after,GcxRaceExclusionWriter.Replace(after,rows));
    }

    [Fact]
    public void Longer_lists_promote_parameter_prefix_and_still_decode()
    {
        IReadOnlyList<int>[] rows = Enumerable.Range(0,12).Select(_ => (IReadOnlyList<int>)Enumerable.Range(0,12).ToArray()).ToArray();
        var after=GcxRaceExclusionWriter.Replace(Convert.FromHexString(Native),rows);
        Assert.Contains("-l[37B22B] 0 1 2 3 4 5 6 7 8 9 10 11",GcxDecompiler.Decompile(after,"race"));
        Assert.Equal(after,GcxRaceExclusionWriter.Replace(after,rows));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(12)]
    public void Rejects_out_of_range_exclusions(int invalid)
    {
        IReadOnlyList<int>[] rows=Enumerable.Range(0,12).Select(_=>(IReadOnlyList<int>)new[]{invalid}).ToArray();
        Assert.Throws<ArgumentException>(()=>GcxRaceExclusionWriter.Replace(Convert.FromHexString(Native),rows));
    }

    [Fact]
    public void Rejects_duplicate_exclusions_and_missing_rows()
    {
        IReadOnlyList<int>[] rows=Enumerable.Range(0,12).Select(_=>(IReadOnlyList<int>)new[]{0,0}).ToArray();
        Assert.Throws<ArgumentException>(()=>GcxRaceExclusionWriter.Replace(Convert.FromHexString(Native),rows));
        Assert.Throws<ArgumentException>(()=>GcxRaceExclusionWriter.Replace(Convert.FromHexString(Native),rows[..11]));
    }

    [Fact]
    public void Rejects_truncated_script_and_duplicate_base_row()
    {
        var before=Convert.FromHexString(Native);
        IReadOnlyList<int>[] rows=Enumerable.Range(0,12).Select(_=>(IReadOnlyList<int>)new[]{0}).ToArray();
        Assert.Throws<InvalidDataException>(()=>GcxRaceExclusionWriter.Replace(before[..80],rows));
        byte[] secondBase=Convert.FromHexString("5562C59232C2");
        int offset=before.AsSpan().IndexOf(secondBase);
        Assert.True(offset>=0);
        before[offset+5]=0xC1;
        Assert.Throws<InvalidDataException>(()=>GcxRaceExclusionWriter.Replace(before,rows));
    }
}
