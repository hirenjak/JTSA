using JTSA.Models;
using JTSA.Panels;
using Xunit;

namespace JTSA.Tests;

public class CalendarEntryOrderingTests
{
    [Fact]
    public void OrderEntries_ListsUpcomingFirstAndPastInDescendingOrder()
    {
        var today = new DateTime(2026, 9, 18);
        var entries = new[]
        {
            CreateEntry(1, today.AddDays(-3), 20),
            CreateEntry(2, today.AddDays(2), 21),
            CreateEntry(3, today.AddDays(-1), 22),
            CreateEntry(4, today, 19),
            CreateEntry(5, today.AddDays(2), 18)
        };

        var orderedIds = CalendarPanel.OrderEntries(entries, today)
            .Select(entry => entry.Id)
            .ToArray();

        Assert.Equal([4, 5, 2, 3, 1], orderedIds);
    }

    private static T_CalendarEntry CreateEntry(long id, DateTime date, int hour) => new()
    {
        Id = id,
        CalendarDate = date,
        StartTime = TimeSpan.FromHours(hour),
        UpdatedDateTime = date
    };
}
