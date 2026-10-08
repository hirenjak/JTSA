using System.ComponentModel.DataAnnotations;

namespace JTSA.Models;

/// <summary>アプリから案内したワンショット通知の確認状態。</summary>
public sealed class T_AppNotificationReceipt
{
    [Key]
    public required string NotificationKey { get; set; }

    public DateTime AcknowledgedAt { get; set; }
}
