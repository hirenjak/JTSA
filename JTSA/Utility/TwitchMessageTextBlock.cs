using JTSA.Panels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using XamlAnimatedGif;

namespace JTSA.Controls
{
    public class TwitchMessageRichTextBox : RichTextBox
    {
        private static readonly HttpClient httpClient = new();
        private static readonly Regex urlPattern = new(
            @"https?://[^\s<>""'「」『』【】。、！？]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly JTSA.Utility.AsyncCache<string, byte[]> imageCache = new(256, TimeSpan.FromMinutes(10));

        public static readonly DependencyProperty MessagePartsProperty =
            DependencyProperty.Register(
                nameof(MessageParts),
                typeof(IEnumerable<TwitchChatPart>),
                typeof(TwitchMessageRichTextBox),
                new PropertyMetadata(null, OnMessagePartsChanged));

        public IEnumerable<TwitchChatPart>? MessageParts
        {
            get => (IEnumerable<TwitchChatPart>?)
                GetValue(MessagePartsProperty);

            set => SetValue(MessagePartsProperty, value);
        }

        public TwitchMessageRichTextBox()
        {
            IsReadOnly = true;
            IsUndoEnabled = false;
            IsDocumentEnabled = true;

            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);

            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;

            Document.PagePadding = new Thickness(0);
        }

        private static void OnMessagePartsChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            var richTextBox =
                (TwitchMessageRichTextBox)dependencyObject;

            richTextBox.CreateDocument();
        }

        private void CreateDocument()
        {
            Document.Blocks.Clear();

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            if (MessageParts != null)
            {
                foreach (var part in MessageParts)
                {
                    if (!part.IsEmote)
                    {
                        AddTextInlines(paragraph, part);

                        continue;
                    }

                    var image = new Image
                    {
                        Width = 28,
                        Height = 28,
                        Stretch = Stretch.Uniform,
                        ToolTip = part.Text
                    };

                    var imageUrl = part.ImageUrl!;

                    RoutedEventHandler? loadedHandler = null;

                    loadedHandler = async (_, _) =>
                    {
                        image.Loaded -= loadedHandler;

                        await LoadEmoteAsync(
                            image,
                            imageUrl);
                    };

                    image.Loaded += loadedHandler;

                    paragraph.Inlines.Add(
                        new InlineUIContainer(image)
                        {
                            BaselineAlignment =
                                BaselineAlignment.Center
                        });
                }
            }

            Document.Blocks.Add(paragraph);
        }

        private static void AddTextInlines(Paragraph paragraph, TwitchChatPart part)
        {
            var text = part.Text ?? string.Empty;
            Brush? foreground = string.IsNullOrWhiteSpace(part.Foreground)
                ? null
                : (Brush)new BrushConverter().ConvertFromString(part.Foreground)!;

            void AddText(string value)
            {
                if (value.Length == 0) return;
                var run = new Run(value);
                if (foreground != null) run.Foreground = foreground;
                paragraph.Inlines.Add(run);
            }

            var offset = 0;
            foreach (Match match in urlPattern.Matches(text))
            {
                var url = match.Value.TrimEnd('.', ',', '!', '?', ';', ':');
                // 文中の括弧は除外し、URL自体に含まれる対応した括弧は残す。
                while (url.Length > 0 &&
                    ((url.EndsWith(')') && url.Count(c => c == ')') > url.Count(c => c == '(')) ||
                     (url.EndsWith(']') && url.Count(c => c == ']') > url.Count(c => c == '['))))
                    url = url[..^1];

                AddText(text[offset..match.Index]);
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                    !string.IsNullOrEmpty(uri.Host))
                {
                    var link = new Hyperlink(new Run(url))
                    {
                        NavigateUri = uri,
                        ToolTip = url,
                        Foreground = Brushes.DeepSkyBlue
                    };
                    link.RequestNavigate += OpenChatLink;
                    paragraph.Inlines.Add(link);
                }
                else
                {
                    AddText(url);
                }
                offset = match.Index + url.Length;
            }
            AddText(text[offset..]);
        }

        private static void OpenChatLink(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                    mainWindow.AppLogPanel.Error(nameof(TwitchMessageRichTextBox),
                        $"リンクを開けませんでした。{exception.Message}");
            }
        }

        private static async Task LoadEmoteAsync(
    Image image,
    string imageUrl)
        {
            try
            {
                var imageData = await imageCache.GetAsync(imageUrl, () => httpClient.GetByteArrayAsync(imageUrl));

                if (IsGif(imageData))
                {
                    // Imageごとに別のストリームを作る
                    var stream = new MemoryStream(
                        imageData,
                        writable: false);

                    AnimationBehavior.SetSourceStream(
                        image,
                        stream);
                }
                else
                {
                    // 静止画像
                    using var stream = new MemoryStream(
                        imageData,
                        writable: false);

                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelHeight = 64;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();

                    if (bitmap.CanFreeze)
                        bitmap.Freeze();

                    image.Source = bitmap;
                }
            }
            catch (Exception exception)
            {
                // 失敗結果をキャッシュし続けない
                imageCache.Remove(imageUrl);

                System.Diagnostics.Debug.WriteLine(
                    $"スタンプ画像読込エラー: {imageUrl}");

                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

        private static bool IsGif(byte[] data)
        {
            if (data.Length < 6)
                return false;

            // GIF87a または GIF89a
            return data[0] == (byte)'G' &&
                   data[1] == (byte)'I' &&
                   data[2] == (byte)'F' &&
                   data[3] == (byte)'8' &&
                   (data[4] == (byte)'7' ||
                    data[4] == (byte)'9') &&
                   data[5] == (byte)'a';
        }
    }
}
