using JTSA.Panels;
using System.Windows;

namespace JTSA;

public partial class StreamExpansionPermissionSettingsWindow : Window
{
    private readonly StreamExpansionHeaderForm target;

    public bool ChatPermissionEveryone { get; set; }
    public bool ChatPermissionModerator { get; set; }
    public bool ChatPermissionVip { get; set; }
    public bool ChatPermissionSubscriber { get; set; }

    public StreamExpansionPermissionSettingsWindow(StreamExpansionHeaderForm target)
    {
        this.target = target;
        ChatPermissionEveryone = target.ChatPermissionEveryone;
        ChatPermissionModerator = target.ChatPermissionModerator;
        ChatPermissionVip = target.ChatPermissionVip;
        ChatPermissionSubscriber = target.ChatPermissionSubscriber;

        InitializeComponent();
        DataContext = this;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        target.ChatPermissionEveryone = ChatPermissionEveryone;
        target.ChatPermissionModerator = ChatPermissionModerator;
        target.ChatPermissionVip = ChatPermissionVip;
        target.ChatPermissionSubscriber = ChatPermissionSubscriber;
        target.NotifyExecutionPermissionItems();
        DialogResult = true;
    }
}
