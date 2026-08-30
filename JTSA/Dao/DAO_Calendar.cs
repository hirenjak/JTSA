using JTSA.Models;

namespace JTSA.Dao;

internal static class DAO_Calendar
{
    public static List<T_CalendarEntry> SelectAll()
    {
        using var db = new AppDbContext();
        var entries = db.T_CalendarEntry
            .ToList()
            .OrderBy(entry => entry.CalendarDate)
            .ThenBy(entry => entry.StartTime)
            .ToList();
        var friendNames = db.M_User
            .ToDictionary(user => user.UserId, user => user.DisplayName);

        foreach (var entry in entries)
        {
            entry.SelectedFriendNames = string.Join("、",
                entry.SelectedFriendIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => friendNames.TryGetValue(id, out var name) ? name : id));
        }

        return entries;
    }

    public static void InsertUpdate(
        DateTime date,
        string content,
        string titlePlaceholder = "",
        string categoryId = "",
        string categoryName = "",
        string categoryBoxArtUrl = "",
        string selectedFriendIds = "",
        TimeSpan? startTime = null,
        long? entryId = null)
    {
        using var db = new AppDbContext();
        var calendarDate = date.Date;
        var entry = entryId.HasValue
            ? db.T_CalendarEntry.SingleOrDefault(x => x.Id == entryId.Value)
            : null;
        var now = DateTime.Now;

        if (entry == null)
        {
            db.T_CalendarEntry.Add(new T_CalendarEntry
            {
                CalendarDate = calendarDate,
                StartTime = startTime ?? TimeSpan.Zero,
                Content = content,
                TitlePlaceholder = titlePlaceholder,
                CategoryId = categoryId,
                CategoryName = categoryName,
                CategoryBoxArtUrl = categoryBoxArtUrl,
                SelectedFriendIds = selectedFriendIds,
                SelectedCount = 0,
                SortNumber = 9999,
                CreatedDateTime = now,
                UpdatedDateTime = now,
                LastUsedDateTime = now
            });
        }
        else
        {
            entry.Content = content;
            entry.StartTime = startTime ?? TimeSpan.Zero;
            entry.TitlePlaceholder = titlePlaceholder;
            entry.CategoryId = categoryId;
            entry.CategoryName = categoryName;
            entry.CategoryBoxArtUrl = categoryBoxArtUrl;
            entry.SelectedFriendIds = selectedFriendIds;
            entry.UpdatedDateTime = now;
            entry.LastUsedDateTime = now;
        }

        db.SaveChanges();
    }

    public static void Delete(long id)
    {
        using var db = new AppDbContext();
        var entry = db.T_CalendarEntry.SingleOrDefault(x => x.Id == id);
        if (entry == null)
            return;

        db.T_CalendarEntry.Remove(entry);
        db.SaveChanges();
    }
}
