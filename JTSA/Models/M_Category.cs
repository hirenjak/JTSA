using JTSA.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_Category
{
    [Key]
    public required String CategoryId { get; set; }

    public required String DisplayName { get; set; }

    public required String BoxArtUrl { get; set; }

    public int CountSelected { get; set; }

    public int SortNumber { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime LastUseDateTime { get; set; }

    public DateTime CreatedDateTime { get; set; }
    
    public DateTime UpdateDateTime { get; set; }


    /// <summary>
    /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<M_Category> SelectAllOrderbyLastUser(AppDbContext db)
    {
        List<M_Category> results = new();

        foreach (var record in db.M_CategoryList.OrderByDescending(x => x.LastUseDateTime))
        {
            results.Add(new()
            {
                CategoryId = record.CategoryId,
                DisplayName = record.DisplayName,
                BoxArtUrl = record.BoxArtUrl,
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
    /// <returns>true：登録成功 false：既にデータがある</returns>
    public static bool Insert(AppDbContext db, M_Category insertData)
    {
        db.M_CategoryList.Add(insertData);

        if(db.M_CategoryList.SingleOrDefault(x => x.CategoryId == insertData.CategoryId) == null)
        {
            db.SaveChanges();

            return true;
        }

        return false;
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(String categoryId)
    {
        using var db = new AppDbContext();
        var entity = db.M_CategoryList.FirstOrDefault(x => x.CategoryId == categoryId);

        if (entity != null)
        {
            db.M_CategoryList.Remove(entity);
            db.SaveChanges();
        }
    }
}