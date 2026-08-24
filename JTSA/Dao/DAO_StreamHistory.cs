using JTSA.Models;
using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao
{
    class DAO_StreamHistory
    {
        public static void Upsert(T_StreamHistory record)
        {
            using var db = new AppDbContext();
            var existing = db.T_StreamHistory.FirstOrDefault(x => x.StreamId == record.StreamId);
            if (existing is null)
            {
                db.T_StreamHistory.Add(record);
            }
            else
            {
                existing.Title = string.IsNullOrWhiteSpace(record.Title) ? existing.Title : record.Title;
                existing.CategoryName = string.IsNullOrWhiteSpace(record.CategoryName) ? existing.CategoryName : record.CategoryName;
                existing.StartedAt = record.StartedAt;
                existing.EndedAt = record.EndedAt ?? existing.EndedAt;
                existing.ArchiveVideoId = string.IsNullOrWhiteSpace(record.ArchiveVideoId) ? existing.ArchiveVideoId : record.ArchiveVideoId;
                existing.ArchiveUrl = string.IsNullOrWhiteSpace(record.ArchiveUrl) ? existing.ArchiveUrl : record.ArchiveUrl;
                existing.UpdatedDateTime = DateTime.Now;
            }
            db.SaveChanges();
        }

        public static List<T_StreamHistory> SelectAll()
        {
            using var db = new AppDbContext();
            return db.T_StreamHistory.AsNoTracking().OrderBy(x => x.StartedAt).ToList();
        }

        public static void EndActiveStreams(string broadcasterId, DateTime endedAt)
        {
            using var db = new AppDbContext();
            var activeStreams = db.T_StreamHistory
                .Where(x => x.BroadcasterId == broadcasterId && x.EndedAt == null)
                .ToList();
            foreach (var stream in activeStreams)
            {
                stream.EndedAt = endedAt;
                stream.UpdatedDateTime = DateTime.Now;
            }
            if (activeStreams.Count > 0)
                db.SaveChanges();
        }
    }
}
