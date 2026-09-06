using JTSA.Panels;
using System.Windows;

namespace JTSA;

public sealed class CalendarRegistrationWindow : ToolPanelWindow
{
    public CalendarRegistrationWindow(
        DateTime selectedDate,
        string titlePlaceholder,
        long? entryId = null,
        bool duplicateEntry = false)
        : this(
            entryId.HasValue && duplicateEntry
                ? "カレンダー予定の複製"
                : entryId.HasValue ? "カレンダー予定の編集" : "カレンダー予定の登録",
            new CalendarRegistrationPanel(),
            selectedDate,
            titlePlaceholder,
            entryId,
            duplicateEntry)
    {
    }

    private CalendarRegistrationWindow(
        string title,
        CalendarRegistrationPanel panel,
        DateTime selectedDate,
        string titlePlaceholder,
        long? entryId,
        bool duplicateEntry)
        : base(title, panel)
    {
        Width = 680;
        Height = 660;
        MinWidth = 620;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        panel.SetInitialPlaceholder(titlePlaceholder);
        panel.CloseRequested += (_, _) => Close();

        if (entryId.HasValue && duplicateEntry)
            panel.SelectEntryForDuplication(entryId.Value);
        else if (entryId.HasValue)
            panel.SelectEntryForEditing(entryId.Value);
        else
            panel.SetScheduleDateFromCalendar(selectedDate);
    }
}
