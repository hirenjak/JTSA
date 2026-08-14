using JTSA.Models;

namespace JTSA.Dao;

internal static class DAO_StreamWindow
{
    public static List<T_StreamWindow> SelectAll()
    {
        using var db = new AppDbContext();
        return db.T_StreamWindow.OrderBy(x => x.ProcessName).ToList();
    }

    public static void Save(T_StreamWindow window)
    {
        using var db = new AppDbContext();
        var entity = db.T_StreamWindow.SingleOrDefault(x => x.ProcessName == window.ProcessName);
        var now = DateTime.Now;

        if (entity is null)
        {
            window.CreatedDateTime = now;
            window.UpdatedDateTime = now;
            window.LastUsedDateTime = now;
            db.T_StreamWindow.Add(window);
        }
        else
        {
            entity.WindowTitle = window.WindowTitle;
            entity.AppExePath = window.AppExePath;
            entity.X = window.X;
            entity.Y = window.Y;
            entity.Width = window.Width;
            entity.Height = window.Height;
            entity.UpdatedDateTime = now;
            entity.LastUsedDateTime = now;
        }

        db.SaveChanges();
    }

    public static void Delete(string processName)
    {
        using var db = new AppDbContext();
        var entity = db.T_StreamWindow.SingleOrDefault(x => x.ProcessName == processName);
        if (entity is null) return;
        db.T_StreamWindow.Remove(entity);
        db.SaveChanges();
    }
}
