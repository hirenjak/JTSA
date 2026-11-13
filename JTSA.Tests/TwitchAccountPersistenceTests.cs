using JTSA.Dao;
using JTSA.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JTSA.Tests;

public sealed class TwitchAccountPersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "JTSA.Tests", Guid.NewGuid().ToString("N"));

    public TwitchAccountPersistenceTests()
    {
        AppDbContext.DatabasePathOverride = Path.Combine(directory, "JTSA.db");
        using var db = new AppDbContext();
        db.Database.Migrate();
    }

    [Fact]
    public void AddingAndReauthenticatingSubAccountPreservesMain()
    {
        var main = DAO_TwitchAccount.InsertUpdate("main", "1", "main-refresh");
        var sub = DAO_TwitchAccount.InsertUpdate("sub", "2", "sub-refresh");
        DAO_TwitchAccount.InsertUpdate("sub", "2", "reauthenticated");
        Assert.Equal(main.Id, DAO_TwitchAccount.SelectPrimary()!.Id);
        Assert.False(DAO_TwitchAccount.SelectById(sub.Id)!.IsPrimary);
        Assert.Equal("main-refresh", DAO_TwitchAccount.SelectById(main.Id)!.RefreshToken);
        Assert.Equal("reauthenticated", DAO_TwitchAccount.SelectById(sub.Id)!.RefreshToken);
    }

    [Fact]
    public void ChangingMainLeavesExactlyOnePrimary()
    {
        DAO_TwitchAccount.InsertUpdate("main", "1", "main-refresh");
        var sub = DAO_TwitchAccount.InsertUpdate("sub", "2", "sub-refresh", isPrimary: true);
        Assert.Equal(sub.Id, Assert.Single(DAO_TwitchAccount.SelectAll(), a => a.IsPrimary).Id);
    }

    [Fact]
    public void LegacyDuplicatePrimariesKeepOriginalAccountAndSeparateTokens()
    {
        var main = DAO_TwitchAccount.InsertUpdate("main", "1", "main-refresh");
        var sub = DAO_TwitchAccount.InsertUpdate("sub", "2", "sub-refresh");
        using (var db = new AppDbContext())
        {
            db.M_TwitchAccount.Single(a => a.Id == sub.Id).IsPrimary = true;
            db.SaveChanges();
        }
        Assert.Equal(main.Id, DAO_TwitchAccount.SelectPrimary()!.Id);
        Assert.Single(DAO_TwitchAccount.SelectAll(), a => a.IsPrimary);
        Assert.False(DAO_TwitchAccount.SelectById(sub.Id)!.IsPrimary);
        Assert.Equal("sub-refresh", DAO_TwitchAccount.SelectById(sub.Id)!.RefreshToken);
    }

    public void Dispose()
    {
        AppDbContext.DatabasePathOverride = null;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
