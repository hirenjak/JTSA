using JTSA.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_TitleTag
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public required string DisplayName { get; set; }

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
    public static List<M_TitleTag> SelectAllOrderbyLastUser()
    {
        using var db = new AppDbContext();

        List<M_TitleTag> results = new();

        foreach (var record in db.M_TitleTagList.OrderByDescending(x => x.LastUseDateTime))
        {
            results.Add(new()
            {
                Id = record.Id,
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
    public static M_TitleTag SelectOneById(int id)
    {
        using var db = new AppDbContext();

        return db.M_TitleTagList.Single(x => x.Id == id);
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(int id)
    {
        using var db = new AppDbContext();

        var entity = db.M_TitleTagList.FirstOrDefault(x => x.Id == id);

        if (entity != null)
        {
            db.M_TitleTagList.Remove(entity);
            db.SaveChanges();
        }
    }


    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Insert(M_TitleTag insertData)
    {
        using var db = new AppDbContext();

        db.M_TitleTagList.Add(insertData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Update
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Update(M_TitleTag updateData)
    {
        using var db = new AppDbContext();

        var targetRecord = SelectOneById(updateData.Id);

        updateData.CreatedDateTime = targetRecord.CreatedDateTime;

        db.M_TitleTagList.Update(updateData);
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
}