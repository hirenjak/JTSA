using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JTSA.Models;

public class T_CalendarEntry : DBBaseTransaction
{
    [Key]
    public long Id { get; set; }

    public DateTime CalendarDate { get; set; }

    public TimeSpan StartTime { get; set; }

    [NotMapped]
    public string StartTimeDisplay => $"{StartTime:hh\\:mm}～";

    [NotMapped]
    public bool IsPast => CalendarDate.Date < DateTime.Today;

    public string Content { get; set; } = string.Empty;

    public string TitlePlaceholder { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string CategoryBoxArtUrl { get; set; } = string.Empty;

    /// <summary>予定に紐づけたフレンドのBroadcastId（カンマ区切り）。</summary>
    public string SelectedFriendIds { get; set; } = string.Empty;

    [NotMapped]
    public string SelectedFriendNames { get; set; } = string.Empty;
}
