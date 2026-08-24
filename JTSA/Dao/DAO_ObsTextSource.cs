using JTSA.Models;
using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao;

internal static class DAO_ObsTextSource
{
    public static List<M_ObsTextSource> SelectAll()
    {
        using var db = new AppDbContext();
        return db.M_ObsTextSource.OrderBy(x => x.SortNumber).ThenBy(x => x.Id).ToList();
    }

    public static void ReplaceAll(IEnumerable<M_ObsTextSource> settings)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.M_ObsTextSource.RemoveRange(db.M_ObsTextSource);
        db.SaveChanges();

        var now = DateTime.Now;
        db.M_ObsTextSource.AddRange(settings.Select((setting, index) => new M_ObsTextSource
        {
            IsSubObs = setting.IsSubObs,
            DisplayName = setting.DisplayName,
            SceneName = setting.SceneName,
            SourceName = setting.SourceName,
            SortNumber = index,
            CreatedDateTime = now,
            UpdatedDateTime = now,
            LastUsedDateTime = now
        }));
        db.SaveChanges();
        transaction.Commit();
    }
}
