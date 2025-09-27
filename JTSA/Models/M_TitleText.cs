using JTSA.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_TitleText
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public required string Content { get; set; }

    public required string CategoryId { get; set; }

    public required string CategoryName { get; set; }

    public required string CategoryBoxArtUrl { get; set; }

    public int CountSelected { get; set; }

    public int SortNumber { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime LastUseDateTime { get; set; }

    public DateTime CreatedDateTime { get; set; }
    
    public DateTime UpdateDateTime { get; set; }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<M_TitleText> SelectAllOrderbyId(AppDbContext db)
    {
        List<M_TitleText> results = new();

        foreach(var record in db.M_TitleTextList.OrderByDescending(x => x.Id))
        {
            results.Add(new()
            {
                Id = record.Id,
                Content = record.Content,
                CategoryId = record.CategoryId,
                CategoryName = record.CategoryName,
                CategoryBoxArtUrl = record.CategoryBoxArtUrl,
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
    public static List<M_TitleText> SelectAllOrderbyLastUser(AppDbContext db)
    {
        List<M_TitleText> results = [];

        foreach (var record in db.M_TitleTextList.OrderByDescending(x => x.LastUseDateTime))
        {
            results.Add(new()
            {
                Id = record.Id,
                Content = record.Content,
                CategoryId = record.CategoryId,
                CategoryName = record.CategoryName,
                CategoryBoxArtUrl = record.CategoryBoxArtUrl,
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
    public static M_TitleText SelectOneById(int id)
    {
        using var db = new AppDbContext();

        return db.M_TitleTextList.Single(x => x.Id == id);
    }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<M_TitleText> SelectSaveDataOrderbyLastUser()
    {
        using var db = new AppDbContext();

        List<M_TitleText> results = [];

        foreach (var record in db.M_TitleTextList.Where(x => x.SortNumber == 9999).OrderByDescending(x => x.UpdateDateTime))
        {
            results.Add(new()
            {
                Id = record.Id,
                Content = record.Content,
                CategoryId = record.CategoryId,
                CategoryName = record.CategoryName,
                CategoryBoxArtUrl = record.CategoryBoxArtUrl,
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
    public static bool Insert(M_TitleText insertData)
    {
        using var db = new AppDbContext();

        db.M_TitleTextList.Add(insertData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Update(M_TitleText updateData)
    {
        using var db = new AppDbContext();

        var targetRecord = SelectOneById(updateData.Id);
        updateData.CreatedDateTime = targetRecord.CreatedDateTime;

        db.M_TitleTextList.Update(updateData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update：最終使用
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool UpdateLastUse(int id)
    {
        var targetRecord = SelectOneById(id);

        targetRecord.CountSelected += 1;
        targetRecord.LastUseDateTime = DateTime.Now;

        return Update(targetRecord);
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(int id)
    {
        using var db = new AppDbContext();

        var entity = db.M_TitleTextList.FirstOrDefault(x => x.Id == id);

        if (entity != null)
        {
            db.M_TitleTextList.Remove(entity);
            db.SaveChanges();
        }
    }
}