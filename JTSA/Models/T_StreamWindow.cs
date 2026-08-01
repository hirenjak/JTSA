using JTSA.Models;
using System.ComponentModel.DataAnnotations;

public class T_StreamWindow :DBBaseTransaction
{
    [Key]
    public required string ProcessName { get; set; }
    
    public required string WindowTitle { get; set; }

    public required string AppExePath { get; set; }

    public int X { get; set; }
    
    public int Y { get; set; }
    
    public int Width { get; set; }

    public int Height { get; set; }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static List<T_StreamWindow> SelectAllOrderbyProcessName()
    {
        using var db = new AppDbContext();

        List<T_StreamWindow> results = [];

        foreach (var record in db.M_StreamWindowList.OrderByDescending(x => x.ProcessName))
        {
            results.Add(new()
            {
                ProcessName = record.ProcessName,
                WindowTitle = record.WindowTitle,
                AppExePath = record.AppExePath,
                X = record.X,
                Y = record.Y,
                Width = record.Width,
                Height = record.Height,
                IsDeleted = record.IsDeleted,
                LastUsedDateTime = record.LastUsedDateTime,
                CreatedDateTime = record.CreatedDateTime,
                UpdatedDateTime = record.UpdatedDateTime
            });
        }

        return results;
    }


    /// <summary>
    /// SELECT * FROM M_TitleText ORDER BY Id DESC
    /// </summary>
    /// <param name="db"></param>
    /// <returns></returns>
    public static T_StreamWindow SelectOneById(string processName)
    {
        using var db = new AppDbContext();

        return db.M_StreamWindowList.Single(x => x.ProcessName == processName);
    }


    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Insert(T_StreamWindow insertData)
    {
        using var db = new AppDbContext();

        db.M_StreamWindowList.Add(insertData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Insert
    /// </summary>
    /// <param name="db"></param>
    /// <param name="insertData"></param>
    /// <returns></returns>
    public static bool Update(T_StreamWindow updateData)
    {
        using var db = new AppDbContext();

        var targetRecord = SelectOneById(updateData.ProcessName);

        updateData.CreatedDateTime = targetRecord.CreatedDateTime;

        db.M_StreamWindowList.Update(updateData);
        int result = db.SaveChanges();

        return result > 0 ? true : false;
    }


    /// <summary>
    /// Delete
    /// </summary>
    /// <param name="id"></param>
    public static void Delete(string processName)
    {
        using var db = new AppDbContext();

        var entity = db.M_StreamWindowList.FirstOrDefault(x => x.ProcessName == processName);

        if (entity != null)
        {
            db.M_StreamWindowList.Remove(entity);
            db.SaveChanges();
        }
    }
}