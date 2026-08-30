using System.Windows;

namespace JTSA.Panels;

public partial class FriendSelectionWindow : Window
{
    private readonly string[] initialBroadcastIds;

    public IReadOnlyList<string> SelectedBroadcastIds { get; private set; } = [];

    public FriendSelectionWindow(IEnumerable<string> selectedBroadcastIds)
    {
        initialBroadcastIds = selectedBroadcastIds.ToArray();
        InitializeComponent();
        ContentRendered += FriendSelectionWindow_ContentRendered;
    }


    private void FriendSelectionWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= FriendSelectionWindow_ContentRendered;
        SelectionPanel.SelectFriends(initialBroadcastIds);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedBroadcastIds = SelectionPanel.SelectedFriendFormList
            .Select(friend => friend.BroadcastId)
            .ToArray();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
