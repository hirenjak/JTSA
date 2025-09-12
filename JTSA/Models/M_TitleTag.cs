using JTSA;
using JTSA.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class M_TitleTag
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public required String DisplayName { get; set; }

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
    public static List<M_TitleTag> SelectAllOrderbyLastUser(AppDbContext db)
    {
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
    public static bool Insert(AppDbContext db, M_TitleTag insertData)
    {
        db.M_TitleTagList.Add(insertData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }
}