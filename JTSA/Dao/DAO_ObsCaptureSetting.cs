using JTSA.Models;
using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao;

internal static class DAO_ObsCaptureSetting
{
    public static List<M_ObsCaptureSource> SelectSources()
    {
        using var db = new AppDbContext();
        return db.M_ObsCaptureSource.OrderBy(x => x.Id).ToList();
    }

    public static void ReplaceSources(IEnumerable<M_ObsCaptureSource> sources)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.M_ObsCaptureSource.RemoveRange(db.M_ObsCaptureSource);
        db.SaveChanges();

        var now = DateTime.Now;
        db.M_ObsCaptureSource.AddRange(sources.Select(source => new M_ObsCaptureSource
        {
            IsSubObs = source.IsSubObs,
            InputName = source.InputName,
            IsSelected = source.IsSelected,
            CreatedDateTime = now,
            UpdatedDateTime = now,
            LastUsedDateTime = now
        }));
        db.SaveChanges();
        transaction.Commit();
    }

    public static List<M_ObsCategoryCaptureRule> SelectRules()
    {
        using var db = new AppDbContext();
        return db.M_ObsCategoryCaptureRule.AsNoTracking().ToList();
    }

    public static void UpsertRule(M_ObsCategoryCaptureRule rule)
    {
        using var db = new AppDbContext();
        var now = DateTime.Now;
        var existing = db.M_ObsCategoryCaptureRule.SingleOrDefault(x => x.CategoryId == rule.CategoryId);
        if (existing is null)
        {
            rule.CreatedDateTime = now;
            rule.UpdatedDateTime = now;
            rule.LastUsedDateTime = now;
            db.M_ObsCategoryCaptureRule.Add(rule);
        }
        else
        {
            existing.IsSubObs = rule.IsSubObs;
            existing.InputName = rule.InputName;
            existing.DestinationValue = rule.DestinationValue;
            existing.UpdatedDateTime = now;
            existing.LastUsedDateTime = now;
        }
        db.SaveChanges();
    }

    public static void DeleteRule(string categoryId)
    {
        using var db = new AppDbContext();
        var rule = db.M_ObsCategoryCaptureRule.SingleOrDefault(x => x.CategoryId == categoryId);
        if (rule is null) return;
        db.M_ObsCategoryCaptureRule.Remove(rule);
        db.SaveChanges();
    }
}
