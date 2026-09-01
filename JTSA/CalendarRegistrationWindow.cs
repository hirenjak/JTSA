using JTSA.Panels;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTSA;

public sealed class CalendarRegistrationWindow : Window
{
    public CalendarRegistrationWindow(DateTime selectedDate, string titlePlaceholder, long? entryId = null)
    {
        Title = entryId.HasValue ? "カレンダー予定の編集" : "カレンダー予定の登録";
        Width = 680;
        Height = 660;
        MinWidth = 620;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x40, 0x40, 0x40));
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Resources/jtsa.ico"));

        var panel = new CalendarRegistrationPanel();
        panel.SetInitialPlaceholder(titlePlaceholder);
        panel.CloseRequested += (_, _) => Close();
        Content = panel;

        if (entryId.HasValue)
            panel.SelectEntryForEditing(entryId.Value);
        else
            panel.SetScheduleDateFromCalendar(selectedDate);
    }
}
