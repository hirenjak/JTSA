using System.Collections.ObjectModel;
using JTSA.Panels;
using Xunit;

namespace JTSA.Tests;

public class VtsNamedOptionCatalogTests
{
    [Fact]
    public void ReplaceKeepingReplacesPlaceholderWithNamedOption()
    {
        var list = new ObservableCollection<VtsNamedOption>
        {
            new() { Id = "hotkey-1", Name = "hotkey-1" }
        };

        VtsNamedOptionCatalog.ReplaceKeeping(
            list,
            [new VtsNamedOption { Id = "hotkey-1", Name = "手を振る" }],
            ["hotkey-1"]);

        Assert.Single(list);
        Assert.Equal("hotkey-1", list[0].Id);
        Assert.Equal("手を振る", list[0].Name);
    }

    [Fact]
    public void ReplaceKeepingMergesCaseInsensitiveIds()
    {
        var list = new ObservableCollection<VtsNamedOption>
        {
            new() { Id = "ABC-1", Name = "ABC-1" }
        };

        VtsNamedOptionCatalog.ReplaceKeeping(
            list,
            [new VtsNamedOption { Id = "abc-1", Name = "報酬 (100)" }],
            ["ABC-1"]);

        Assert.Single(list);
        Assert.Equal("abc-1", list[0].Id);
        Assert.Equal("報酬 (100)", list[0].Name);
    }

    [Fact]
    public void EnsureOptionDoesNotDuplicateExistingId()
    {
        var list = new ObservableCollection<VtsNamedOption>
        {
            new() { Id = "abc-1", Name = "報酬 (100)" }
        };

        VtsNamedOptionCatalog.EnsureOption(list, "ABC-1");

        Assert.Single(list);
        Assert.Equal("報酬 (100)", list[0].Name);
    }
}
