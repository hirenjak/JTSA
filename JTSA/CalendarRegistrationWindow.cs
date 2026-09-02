using JTSA.Panels;
using System.Windows;

namespace JTSA;

public sealed class CalendarRegistrationWindow : ToolPanelWindow
{
    public CalendarRegistrationWindow(DateTime selectedDate, string titlePlaceholder, long? entryId = null)
        : this(
            entryId.HasValue ? "カレンダー予定の編集" : "カレンダー予定の登録",
            new CalendarRegistrationPanel(),
            selectedDate,
            titlePlaceholder,
            entryId)
    {
    }

    private CalendarRegistrationWindow(
        string title,
        CalendarRegistrationPanel panel,
        DateTime selectedDate,
        string titlePlaceholder,
        long? entryId)
        : base(title, panel)
    {
        Width = 680;
        Height = 660;
        MinWidth = 620;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        panel.SetInitialPlaceholder(titlePlaceholder);
        panel.CloseRequested += (_, _) => Close();

        if (entryId.HasValue)
            panel.SelectEntryForEditing(entryId.Value);
        else
            panel.SetScheduleDateFromCalendar(selectedDate);
    }
}
