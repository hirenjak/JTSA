using JTSA.Dao;
using JTSA.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JTSA.Panels;

public class StreamExpansionHeaderForm : INotifyPropertyChanged
{
    private string headerName = string.Empty;
    private bool isActive;
    public long HeaderId { get; set; }
    public string HeaderName { get => headerName; set { headerName = value; Changed(); } }
    public bool IsActive { get => isActive; set { isActive = value; Changed(); } }
    public bool IsRaid { get; set; }
    public bool IsSubscribe { get; set; }
    public bool IsBits { get; set; }
    public bool IsFirstChat { get; set; }
    public bool IsFollow { get; set; }
    public bool DoShoutout { get; set; }
    public int DelaySeconds { get; set; }
    public string TriggerComment { get; set; } = string.Empty;
    public string TriggerChannelPointId { get; set; } = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public class StreamExpansionItemForm : INotifyPropertyChanged
{
    private bool isImage;
    private bool isAudio;
    private bool isChat;
    private int audioVolume = 100;
    private int weight = 1;
    private string probabilityText = "（0%）";

    public bool IsImage { get => isImage; set { isImage = value; Changed(); } }
    public bool IsAudio { get => isAudio; set { isAudio = value; Changed(); } }
    public bool IsChat { get => isChat; set { isChat = value; Changed(); } }
    public string ImageContent { get; set; } = string.Empty;
    public string AudioContent { get; set; } = string.Empty;
    public string ChatContent { get; set; } = string.Empty;
    public int Weight { get => weight; set { weight = value; Changed(); } }
    public int AudioVolume { get => audioVolume; set { audioVolume = Math.Clamp(value, 0, 100); Changed(); } }
    public string ProbabilityText { get => probabilityText; set { probabilityText = value; Changed(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public class StreamExpansionChannelPointForm
{
    public string RewardId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public partial class StereamExpansionPanel : UserControl , INotifyPropertyChanged
{
    private StreamExpansionHeaderForm? selectedHeader;

    public ObservableCollection<StreamExpansionHeaderForm> HeaderFormList { get; } = [];
    public ObservableCollection<StreamExpansionItemForm> ItemFormList { get; } = [];
    public ObservableCollection<StreamExpansionChannelPointForm> ChannelPointFormList { get; } = [];

    public StreamExpansionHeaderForm? SelectedHeader { get => selectedHeader; set { selectedHeader = value; PropertyChanged?.Invoke(this, new(nameof(SelectedHeader))); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public StereamExpansionPanel() 
    {
        InitializeComponent();
        DataContext = this;


        Loaded += StereamExpansionPanel_Loaded;
        IsVisibleChanged += StereamExpansionPanel_IsVisibleChanged;
    }

    private void StereamExpansionPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadChannelPoints();
        Reload();
    }

    private void StereamExpansionPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ReloadChannelPoints();
        }
    }

    /// <summary>
    /// チャンネルポイント選択肢再読み込み
    /// </summary>
    public void ReloadChannelPoints()
    {
        ChannelPointFormList.Clear();
        ChannelPointFormList.Add(new()
        {
            RewardId = string.Empty,
            DisplayName = "（指定なし）"
        });

        foreach (var reward in DAO_ChannelPoint.SelectAll())
        {
            ChannelPointFormList.Add(new()
            {
                RewardId = reward.RewardId,
                DisplayName = $"{reward.Title}（{reward.Cost:N0}pt）"
            });
        }
    }

    /// <summary>
    /// 再読み込み
    /// </summary>
    private void Reload(long selectId = 0)
    {
        // ヘッダーリストの初期化
        HeaderFormList.Clear();

        // ヘッダー情報をDBから呼び出し
        var dbResults = DAO_StreamExpansion.SelectAllHeaders();
        foreach (var x in dbResults)
        {
            HeaderFormList.Add(new()
            {
                HeaderId = x.Id,
                HeaderName = x.Name,
                IsActive = x.IsActive,
                IsRaid = x.IsRaid,
                IsSubscribe = x.IsSubscribe,
                IsBits = x.IsBits,
                IsFirstChat = x.IsFirstChat,
                IsFollow = x.IsFollow,
                DoShoutout = x.DoShoutout,
                DelaySeconds = x.DelaySeconds,
                TriggerComment = x.TriggerComment,
                TriggerChannelPointId = x.TriggerChannelPointId
            });
        }

        // 選択しているアイテムを格納
        SelectedHeader = HeaderFormList.FirstOrDefault(x => x.HeaderId == selectId) ?? HeaderFormList.FirstOrDefault();
        StreamExpansionListBox.SelectedItem = SelectedHeader;
    }


    /// <summary>
    /// 
    /// </summary>
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var item = new StreamExpansionHeaderForm { 
            HeaderName = "新規作成", 
            IsActive = true 
        };

        HeaderFormList.Add(item); SelectedHeader = item; StreamExpansionListBox.SelectedItem = item; ClearItemForms();
    }

    /// <summary>
    /// 左側一覧のチェック操作を、登録済み機能の有効状態へ即時反映する。
    /// </summary>
    private void ActiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is not StreamExpansionHeaderForm header || header.HeaderId == 0)
        {
            return;
        }

        DAO_StreamExpansion.UpdateIsActive(header.HeaderId, header.IsActive);
    }


    /// <summary>
    /// 
    /// </summary>
    private void HeaderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearItemForms();

        if (SelectedHeader is null || SelectedHeader.HeaderId == 0) return;
        foreach (var group in DAO_StreamExpansion.SelectItems(SelectedHeader.HeaderId).GroupBy(x => x.SortNumber))
        {
            var form = new StreamExpansionItemForm
            {
                Weight = group.First().Weight
            };

            foreach (var item in group)
            {
                switch (item.ActionType)
                {
                    case "Image":
                        form.IsImage = true;
                        form.ImageContent = item.Content;
                        break;
                    case "Chat":
                        form.IsChat = true;
                        form.ChatContent = item.Content;
                        break;
                    default:
                        form.IsAudio = true;
                        form.AudioContent = item.Content;
                        form.AudioVolume = item.Volume;
                        break;
                }
            }

            AddItemForm(form);
        }

        UpdateProbabilities();
    }


    /// <summary>
    /// 
    /// </summary>
    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        AddItemForm(new());
        UpdateProbabilities();
    }


    /// <summary>
    /// 
    /// </summary>
    private void DeleteItemButton_Click(object sender, RoutedEventArgs e) 
    {
        if ((sender as Button)?.DataContext is StreamExpansionItemForm x)
        {
            x.PropertyChanged -= StreamExpansionItemForm_PropertyChanged;
            ItemFormList.Remove(x);
            UpdateProbabilities();
        }
    }

    private void AddItemForm(StreamExpansionItemForm form)
    {
        form.PropertyChanged += StreamExpansionItemForm_PropertyChanged;
        ItemFormList.Add(form);
    }

    private void ClearItemForms()
    {
        foreach (var form in ItemFormList)
        {
            form.PropertyChanged -= StreamExpansionItemForm_PropertyChanged;
        }

        ItemFormList.Clear();
    }

    private void StreamExpansionItemForm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreamExpansionItemForm.Weight))
        {
            UpdateProbabilities();
        }
    }

    private void UpdateProbabilities()
    {
        var totalWeight = ItemFormList.Sum(form => Math.Max(1, form.Weight));
        foreach (var form in ItemFormList)
        {
            var probability = totalWeight == 0
                ? 0
                : Math.Max(1, form.Weight) * 100d / totalWeight;
            form.ProbabilityText = $"（{probability:0.##}%）";
        }
    }


    /// <summary>
    /// 音声・画像ファイル選択
    /// </summary>
    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StreamExpansionItemForm item)
        {
            return;
        }

        var actionType = (sender as Button)?.Tag?.ToString() ?? "Audio";

        var dialog = new OpenFileDialog
        {
            Filter = actionType == "Image"
                ? "画像ファイル|*.png;*.jpg;*.jpeg;*.gif;*.bmp|すべてのファイル|*.*"
                : "音声ファイル|*.wav;*.mp3;*.aac;*.wma;*.m4a|すべてのファイル|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            if (actionType == "Image")
            {
                item.ImageContent = dialog.FileName;
            }
            else
            {
                item.AudioContent = dialog.FileName;
            }
            FileExeItemListBox.Items.Refresh();
        }
    }


    /// <summary>
    /// 
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedHeader is null || string.IsNullOrWhiteSpace(SelectedHeader.HeaderName)) 
        { 
            //StatusText.Text = "実装名を入力してね"; 
            return; 
        }

        var saveItems = new List<T_StreamExpansionItem>();
        for (var groupIndex = 0; groupIndex < ItemFormList.Count; groupIndex++)
        {
            var form = ItemFormList[groupIndex];
            AddSaveItem(saveItems, form.IsImage, "Image", form.ImageContent, form.Weight, 100, groupIndex);
            AddSaveItem(saveItems, form.IsAudio, "Audio", form.AudioContent, form.Weight, form.AudioVolume, groupIndex);
            AddSaveItem(saveItems, form.IsChat, "Chat", form.ChatContent, form.Weight, 100, groupIndex);
        }

        var id = DAO_StreamExpansion.Save(new T_StreamExpansionHeader
        {
            Id = SelectedHeader.HeaderId, 
            Name = SelectedHeader.HeaderName.Trim(), 
            IsActive = SelectedHeader.IsActive,
            IsRaid = SelectedHeader.IsRaid, 
            IsSubscribe = SelectedHeader.IsSubscribe, 
            IsBits = SelectedHeader.IsBits,
            IsFirstChat = SelectedHeader.IsFirstChat,
            IsFollow = SelectedHeader.IsFollow,
            DoShoutout = SelectedHeader.DoShoutout,
            DelaySeconds = Math.Clamp(SelectedHeader.DelaySeconds, 0, 3600),
            TriggerComment = SelectedHeader.TriggerComment?.Trim() ?? "", 
            TriggerChannelPointId = SelectedHeader.TriggerChannelPointId?.Trim() ?? "",
            UpdatedDateTime = DateTime.Now, 
            LastUsedDateTime = DateTime.Now
        }, saveItems);
        
        Reload(id); 
        //StatusText.Text = "保存した";
    }

    private static void AddSaveItem(
        List<T_StreamExpansionItem> saveItems,
        bool isSelected,
        string actionType,
        string content,
        int weight,
        int volume,
        int groupIndex)
    {
        if (!isSelected || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        saveItems.Add(new T_StreamExpansionItem
        {
            ActionType = actionType,
            Content = content.Trim(),
            Weight = weight,
            Volume = volume,
            SortNumber = groupIndex,
            CreatedDateTime = DateTime.Now,
            UpdatedDateTime = DateTime.Now,
            LastUsedDateTime = DateTime.Now
        });
    }


    /// <summary>
    /// 
    /// </summary>
    private void DeleteHeaderButton_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as Button)?.DataContext as StreamExpansionHeaderForm ?? SelectedHeader;
        if (target is null) return;

        if (target.HeaderId != 0)
        {
            DAO_StreamExpansion.Delete(target.HeaderId);
        }
        
        Reload(); 
        //StatusText.Text = "削除した";
    }
}
