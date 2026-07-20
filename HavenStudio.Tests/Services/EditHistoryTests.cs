using HavenStudio.Services;

namespace HavenStudio.Tests.Services;

public sealed class EditHistoryTests
{
    [Fact]
    public void Execute_supports_undo_and_redo()
    {
        var history = new EditHistory();
        var value = 0;

        history.Execute("set value", () => value = 7, () => value = 0);

        Assert.Equal(7, value);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.True(history.Undo());
        Assert.Equal(0, value);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        Assert.True(history.Redo());
        Assert.Equal(7, value);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Coalesced_edit_keeps_one_entry_with_the_last_redo()
    {
        var history = new EditHistory();
        var value = 10;

        history.BeginCoalesced("move object", () => value = 10);
        value = 11;
        history.UpdateCoalesced(() => value = 11);
        value = 25;
        history.UpdateCoalesced(() => value = 25);
        Assert.True(history.CommitCoalesced());

        Assert.Equal(1, history.Count);
        Assert.True(history.Undo());
        Assert.Equal(10, value);
        Assert.True(history.Redo());
        Assert.Equal(25, value);
    }

    [Fact]
    public void New_edit_clears_the_redo_stack()
    {
        var history = new EditHistory();
        var value = 0;
        history.Execute("first", () => value = 1, () => value = 0);
        history.Undo();

        history.Execute("second", () => value = 2, () => value = 0);

        Assert.Equal(2, value);
        Assert.False(history.CanRedo);
    }
}
