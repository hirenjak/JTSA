using JTSA.Models;

namespace JTSA.Dao;

internal static class DAO_AppNotificationReceipt
{
    public static bool IsAcknowledged(string notificationKey)
    {
        using var db = new AppDbContext();
        return db.T_AppNotificationReceipt.Any(x => x.NotificationKey == notificationKey);
    }

    public static void Acknowledge(string notificationKey)
    {
        using var db = new AppDbContext();
        if (db.T_AppNotificationReceipt.Any(x => x.NotificationKey == notificationKey)) return;

        db.T_AppNotificationReceipt.Add(new T_AppNotificationReceipt
        {
            NotificationKey = notificationKey,
            AcknowledgedAt = DateTime.Now
        });
        db.SaveChanges();
    }
}
