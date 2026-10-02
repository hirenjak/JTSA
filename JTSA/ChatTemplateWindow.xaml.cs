using JTSA.Dao;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace JTSA;

public partial class ChatTemplateWindow : Window
{
    public sealed class ChatTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    private readonly ObservableCollection<ChatTemplate> templates = [];
    public string SelectedText { get; private set; } = string.Empty;

    public ChatTemplateWindow(string currentChatText)
    {
        InitializeComponent();
        TemplateListBox.ItemsSource = templates;
        TemplateTitleTagPanel.InsertRequested += InsertTitleTag;
        TemplateTitleTagPanel.ReloadTitleTag();
        TemplateTextBox.Text = currentChatText;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        try
        {
            var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ChatTemplates)?.Value;
            if (string.IsNullOrWhiteSpace(json)) return;
            foreach (var item in JsonSerializer.Deserialize<List<ChatTemplate>>(json) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Name)) templates.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"テンプレートを読み込めませんでした: {ex.Message}";
        }
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is not ChatTemplate selected) return;
        TemplateNameTextBox.Text = selected.Name;
        TemplateTextBox.Text = selected.Text;
        StatusTextBlock.Text = string.Empty;
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        TemplateListBox.SelectedItem = null;
        TemplateNameTextBox.Clear();
        TemplateTextBox.Clear();
        TemplateNameTextBox.Focus();
        StatusTextBlock.Text = string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = TemplateNameTextBox.Text.Trim();
        var body = TemplateTextBox.Text.Trim();
        if (name.Length == 0 || body.Length == 0)
        {
            StatusTextBlock.Text = "名前と本文を入力してください。";
            return;
        }

        var duplicate = templates.FirstOrDefault(item =>
            item != TemplateListBox.SelectedItem &&
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (duplicate is not null)
        {
            StatusTextBlock.Text = "同じ名前のテンプレートがあります。";
            return;
        }

        var selected = TemplateListBox.SelectedItem as ChatTemplate;
        var index = selected is null ? -1 : templates.IndexOf(selected);
        var updated = new ChatTemplate { Name = name, Text = body };
        if (index < 0) templates.Add(updated);
        else templates[index] = updated;
        if (!Persist())
        {
            if (index < 0) templates.Remove(updated);
            else templates[index] = selected!;
            return;
        }
        TemplateListBox.SelectedItem = updated;
        StatusTextBlock.Text = "保存しました。";
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is not ChatTemplate selected) return;
        var index = templates.IndexOf(selected);
        templates.RemoveAt(index);
        if (!Persist())
        {
            templates.Insert(index, selected);
            TemplateListBox.SelectedItem = selected;
            return;
        }
        NewButton_Click(sender, e);
        StatusTextBlock.Text = "削除しました。";
    }

    private bool Persist()
    {
        try
        {
            DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ChatTemplates,
                JsonSerializer.Serialize(templates));
            return true;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"テンプレートを保存できませんでした: {ex.Message}";
            return false;
        }
    }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TemplateTextBox.Text))
        {
            StatusTextBlock.Text = "本文を入力してください。";
            return;
        }
        SelectedText = TemplateTextBox.Text;
        DialogResult = true;
    }

    private void InsertTitleTag(string placeholder)
    {
        var index = TemplateTextBox.CaretIndex;
        TemplateTextBox.Text = TemplateTextBox.Text.Insert(index, placeholder);
        TemplateTextBox.CaretIndex = index + placeholder.Length;
        TemplateTextBox.Focus();
    }
}
