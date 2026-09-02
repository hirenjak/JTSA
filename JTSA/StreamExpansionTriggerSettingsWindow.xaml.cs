using JTSA.Panels;
using System.Collections.ObjectModel;
using System.Windows;

namespace JTSA;

public partial class StreamExpansionTriggerSettingsWindow : Window
{
    private readonly StreamExpansionHeaderForm target;

    public bool IsRaid { get; set; }
    public bool IsSubscribe { get; set; }
    public bool IsBits { get; set; }
    public bool IsFirstChat { get; set; }
    public bool IsFollow { get; set; }
    public bool IsObsStreamStartMain { get; set; }
    public bool IsObsStreamStartSub { get; set; }
    public int DelaySeconds { get; set; }
    public string TriggerComment { get; set; } = string.Empty;
    public string TriggerChannelPointId { get; set; } = string.Empty;
    public ObservableCollection<StreamExpansionChannelPointForm> ChannelPoints { get; } = [];

    public StreamExpansionTriggerSettingsWindow(
        StreamExpansionHeaderForm target,
        IEnumerable<StreamExpansionChannelPointForm> channelPoints)
    {
        this.target = target;
        IsRaid = target.IsRaid;
        IsSubscribe = target.IsSubscribe;
        IsBits = target.IsBits;
        IsFirstChat = target.IsFirstChat;
        IsFollow = target.IsFollow;
        IsObsStreamStartMain = target.IsObsStreamStartMain;
        IsObsStreamStartSub = target.IsObsStreamStartSub;
        DelaySeconds = target.DelaySeconds;
        TriggerComment = target.TriggerComment;
        TriggerChannelPointId = target.TriggerChannelPointId;
        foreach (var item in channelPoints) ChannelPoints.Add(item);

        InitializeComponent();
        DataContext = this;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        target.IsRaid = IsRaid;
        target.IsSubscribe = IsSubscribe;
        target.IsBits = IsBits;
        target.IsFirstChat = IsFirstChat;
        target.IsFollow = IsFollow;
        target.IsObsStreamStartMain = IsObsStreamStartMain;
        target.IsObsStreamStartSub = IsObsStreamStartSub;
        target.DelaySeconds = Math.Clamp(DelaySeconds, 0, 3600);
        target.TriggerComment = TriggerComment?.Trim() ?? string.Empty;
        target.TriggerChannelPointId = TriggerChannelPointId?.Trim() ?? string.Empty;
        target.NotifyExecutionTimingSummary();
        DialogResult = true;
    }
}
