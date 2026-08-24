using JTSA.Models;

namespace JTSA.Dao;

internal static class DAO_StreamExpansion
{
    public static List<T_StreamExpansionHeader> SelectAllHeaders()
    {
        using var db = new AppDbContext();
        return db.T_StreamExpansionHeader.OrderBy(x => x.SortNumber).ThenBy(x => x.Id).ToList();
    }

    public static List<T_StreamExpansionItem> SelectItems(long headerId)
    {
        using var db = new AppDbContext();
        return db.T_StreamExpansionItem.Where(x => x.HeaderId == headerId)
            .OrderBy(x => x.SortNumber).ThenBy(x => x.Id).ToList();
    }

    public static long Save(T_StreamExpansionHeader header, IEnumerable<T_StreamExpansionItem> items)
    {
        using var db = new AppDbContext();
        header.DelaySeconds = Math.Clamp(header.DelaySeconds, 0, 3600);
        var entity = header.Id == 0 ? null : db.T_StreamExpansionHeader.SingleOrDefault(x => x.Id == header.Id);
        if (entity is null)
        {
            entity = header;
            entity.CreatedDateTime = DateTime.Now;
            db.T_StreamExpansionHeader.Add(entity);
            db.SaveChanges();
        }
        else
        {
            entity.Name = header.Name;
            entity.IsActive = header.IsActive;
            entity.IsRaid = header.IsRaid;
            entity.IsSubscribe = header.IsSubscribe;
            entity.IsBits = header.IsBits;
            entity.IsFirstChat = header.IsFirstChat;
            entity.IsFollow = header.IsFollow;
            entity.IsObsStreamStart = header.IsObsStreamStart;
            entity.IsObsStreamStartSub = header.IsObsStreamStartSub;
            entity.DoShoutout = header.DoShoutout;
            entity.DelaySeconds = Math.Clamp(header.DelaySeconds, 0, 3600);
            entity.TriggerComment = header.TriggerComment;
            entity.TriggerChannelPointId = header.TriggerChannelPointId;
            entity.UpdatedDateTime = DateTime.Now;
        }

        var oldItems = db.T_StreamExpansionItem.Where(x => x.HeaderId == entity.Id).ToList();
        db.T_StreamExpansionItem.RemoveRange(oldItems);
        var now = DateTime.Now;
        db.T_StreamExpansionItem.AddRange(items.Select(item => new T_StreamExpansionItem
        {
            HeaderId = entity.Id,
            ActionType = item.ActionType,
            Content = item.Content,
            Weight = Math.Max(1, item.Weight),
            Volume = Math.Clamp(item.Volume, 0, 100),
            IsSubObs = item.IsSubObs,
            ObsSceneName = item.ObsSceneName,
            ObsSourceName = item.ObsSourceName,
            SortNumber = item.SortNumber,
            CreatedDateTime = now,
            UpdatedDateTime = now,
            LastUsedDateTime = now
        }));
        db.SaveChanges();
        return entity.Id;
    }

    public static void UpdateIsActive(long headerId, bool isActive)
    {
        using var db = new AppDbContext();
        var header = db.T_StreamExpansionHeader.SingleOrDefault(x => x.Id == headerId);
        if (header is null)
        {
            return;
        }

        header.IsActive = isActive;
        header.UpdatedDateTime = DateTime.Now;
        db.SaveChanges();
    }

    public static void Delete(long headerId)
    {
        using var db = new AppDbContext();
        db.T_StreamExpansionItem.RemoveRange(db.T_StreamExpansionItem.Where(x => x.HeaderId == headerId));
        var header = db.T_StreamExpansionHeader.SingleOrDefault(x => x.Id == headerId);
        if (header is not null) db.T_StreamExpansionHeader.Remove(header);
        db.SaveChanges();
    }
}
