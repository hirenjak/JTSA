using JTSA.Panels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace JTSA.Controls
{
    public class TwitchMessageRichTextBox : RichTextBox
    {
        private static readonly HttpClient httpClient = new();

        private static readonly ConcurrentDictionary<
            string,
            Lazy<Task<byte[]>>> imageCache = new();

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
                        paragraph.Inlines.Add(
                            new Run(part.Text ?? ""));

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

        private static async Task LoadEmoteAsync(
    Image image,
    string imageUrl)
        {
            try
            {
                var lazyData = imageCache.GetOrAdd(
                    imageUrl,
                    url => new Lazy<Task<byte[]>>(
                        () => httpClient.GetByteArrayAsync(url)));

                var imageData = await lazyData.Value;

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
                imageCache.TryRemove(imageUrl, out _);

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