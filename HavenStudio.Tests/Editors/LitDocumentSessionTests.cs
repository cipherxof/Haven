using HavenStudio.Editors.Lighting;
using HavenStudio.Extensions;
using HavenStudio.Formats.Lit;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Editors;

public sealed class LitDocumentSessionTests
{
    [Fact]
    public async Task Untouched_session_save_is_byte_identical()
    {
        using var temp = new TempDirectory();
        var expected = File.ReadAllBytes(FixturePath("mo_st01_d.lt2"));
        var path = temp.GetPath("mo_st01_d.lt2");
        await File.WriteAllBytesAsync(path, expected);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Little);
        await workspace.ScanAsync();
        var session = LitDocumentSession.Load(workspace, WorkspacePath.Physical(path));

        session.Save();

        Assert.False(session.IsDirty);
        Assert.Equal(expected, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Single_point_edit_only_changes_that_records_float()
    {
        using var temp = new TempDirectory();
        var expected = File.ReadAllBytes(FixturePath("mo_st01_d.lt2"));
        var path = temp.GetPath("mo_st01_d.lt2");
        await File.WriteAllBytesAsync(path, expected);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Little);
        await workspace.ScanAsync();
        var session = LitDocumentSession.Load(workspace, WorkspacePath.Physical(path));
        var group = Assert.Single(session.Document.Groups, candidate => candidate.Type == 1);
        var point = Assert.IsType<LitPointLight>(group.Lights[0]);
        point.Range = 700f;
        session.MarkDirty();

        session.Save();

        var actual = await File.ReadAllBytesAsync(path);
        var changed = expected.Zip(actual).Select((pair, index) => (pair, index))
            .Where(item => item.pair.First != item.pair.Second)
            .Select(item => item.index)
            .ToArray();
        Assert.NotEmpty(changed);
        Assert.All(changed, index => Assert.InRange(index, (int)group.LitOffset + 20, (int)group.LitOffset + 23));
        using var rewritten = new MemoryStream(actual, writable: false);
        Assert.Equal(700f,
            Assert.IsType<LitPointLight>(Assert.Single(LitFile.Read(rewritten).Groups, candidate => candidate.Type == 1).Lights[0]).Range);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Lit", name);
}
