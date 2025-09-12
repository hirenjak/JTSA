using JTSA.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Packaging;

public class M_Friend
{
    [Key]
    public required String BroadcastId { get; set; }

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
    public static List<M_Friend> SelectAllOrderbyLastUser(AppDbContext db)
    {
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
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Insert(AppDbContext db, M_Friend insertData)
    {
        if (!db.M_FriendList.Any(x => x.BroadcastId == insertData.BroadcastId))
        {
            db.M_FriendList.Add(insertData);
        }

        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(String broadcastId)
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