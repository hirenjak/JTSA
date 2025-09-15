using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class M_Category
{
    [Key]
    public required string CategoryId { get; set; }

    public required string DisplayName { get; set; }

    public required string BoxArtUrl { get; set; }

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
    public static List<M_Category> SelectAllOrderbyLastUser()
    {
        using var db = new AppDbContext();

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
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static M_Category SelectOneByCategoryId(string categoryId)
    {
        using var db = new AppDbContext();

        return db.M_CategoryList.Single(x => x.CategoryId == categoryId);
    }


    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns>true：登録成功 false：既にデータがある</returns>
    public static bool Insert(M_Category insertData)
    {
        using var db = new AppDbContext();

        db.M_CategoryList.Add(insertData);

        if(db.M_CategoryList.SingleOrDefault(x => x.CategoryId == insertData.CategoryId) == null)
        {
            db.SaveChanges();

            return true;
        }

        return false;
    }


    /// <summary>
    /// Update
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Update(M_Category updateData)
    {
        using var db = new AppDbContext();

        var targetRecord = SelectOneByCategoryId(updateData.CategoryId);

        updateData.CreatedDateTime = targetRecord.CreatedDateTime;

        db.M_CategoryList.Update(updateData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update：最終使用
    /// </summary>
    /// <param name="categoryId"></param>
    /// <returns></returns>
    public static bool UpdateLastUse(string categoryId)
    {
        var targetRecord = SelectOneByCategoryId(categoryId);

        targetRecord.CountSelected += 1;
        targetRecord.LastUseDateTime = DateTime.Now;

        return Update(targetRecord);
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(string categoryId)
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