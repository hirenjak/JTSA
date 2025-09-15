using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_Friend
{
    [Key]
    public required string BroadcastId { get; set; }

    public required string UserId { get; set; }

    public required string DisplayName { get; set; }

    public int CountSelected { get; set; }

    public int SortNumber { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime LastUseDateTime { get; set; }

    public DateTime CreatedDateTime { get; set; }

    public DateTime UpdateDateTime { get; set; }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY BroadcastId DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<M_Friend> SelectAllOrderbyBroadcastId(AppDbContext db)
    {
        List<M_Friend> results = new();

        foreach (var record in db.M_FriendList.OrderByDescending(x => x.BroadcastId))
        {
            results.Add(new()
            {
                BroadcastId = record.BroadcastId,
                UserId = record.UserId,
                DisplayName = record.DisplayName,
                CountSelected = record.CountSelected,
                SortNumber = record.SortNumber,
                IsDeleted = record.IsDeleted,
                LastUseDateTime = record.LastUseDateTime,
                CreatedDateTime = record.CreatedDateTime,
                UpdateDateTime = record.UpdateDateTime
            });
        }

        return results;
    }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY LastUseDateTime DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<M_Friend> SelectAllOrderbyLastUser()
    {
        using var db = new AppDbContext();

        List<M_Friend> results = new();

        foreach (var record in db.M_FriendList.OrderByDescending(x => x.LastUseDateTime))
        {
            results.Add(new()
            {
                BroadcastId = record.BroadcastId,
                UserId = record.UserId,
                DisplayName = record.DisplayName,
                CountSelected = record.CountSelected,
                SortNumber = record.SortNumber,
                IsDeleted = record.IsDeleted,
                LastUseDateTime = record.LastUseDateTime,
                CreatedDateTime = record.CreatedDateTime,
                UpdateDateTime = record.UpdateDateTime
            });
        }

        return results;
    }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static M_Friend SelectOneByBroadcasterId(string broadcasterId)
    {
        using var db = new AppDbContext();

        return db.M_FriendList.Single(x => x.BroadcastId == broadcasterId);
    }


    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Insert(M_Friend insertData)
    {
        using var db = new AppDbContext();

        if (!db.M_FriendList.Any(x => x.BroadcastId == insertData.BroadcastId))
        {
            db.M_FriendList.Add(insertData);
        }

        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Update(M_Friend updateData)
    {
        using var db = new AppDbContext();

        var targetRecord = SelectOneByBroadcasterId(updateData.BroadcastId);
        updateData.CreatedDateTime = targetRecord.CreatedDateTime;

        db.M_FriendList.Update(updateData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update：最終使用
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool UpdateLastUse(string broadcastId)
    {
        var targetRecord = SelectOneByBroadcasterId(broadcastId);

        targetRecord.CountSelected += 1;
        targetRecord.LastUseDateTime = DateTime.Now;

        return Update(targetRecord);
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(string broadcastId)
    {
        using var db = new AppDbContext();
        var entity = db.M_FriendList.FirstOrDefault(x => x.BroadcastId == broadcastId);

        if (entity != null)
        {
            db.M_FriendList.Remove(entity);
            db.SaveChanges();
        }
    }
}