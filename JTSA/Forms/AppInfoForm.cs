using JTSA.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JTSA.Forms
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // アプリ情報用クラス
    public class AppInfoForm : INotifyPropertyChanged
    {
        private string? _processName;
        public string ProcessName
        {
            get => _processName ?? "";
            set
            {
                if (_processName != value)
                {
                    _processName = value;
                    OnPropertyChanged(nameof(ProcessName));
                }
            }
        }

        private string? _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle ?? "";
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        private int? _x;
        public int? X
        {
            get => _x;
            set
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged(nameof(X));
                }
            }
        }

        private int? _y;
        public int? Y
        {
            get => _y;
            set
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged(nameof(Y));
                }
            }
        }

        private int? _width;
        public int? Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        private int? _height;
        public int? Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }

        private string? _status;
        public string Status
        {
            get => _status ?? "";
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private string? _oldstatus;
        public string OldStatus
        {
            get => _oldstatus ?? "";
            set
            {
                if (_oldstatus != value)
                {
                    _oldstatus = value;
                    OnPropertyChanged(nameof(OldStatus));
                }
            }
        }

        private string? _appExePath;
        public string AppExePath
        {
            get => _appExePath ?? "";
            set
            {
                if (_appExePath != value)
                {
                    _appExePath = value;
                    OnPropertyChanged(nameof(AppExePath));
                    OnPropertyChanged(nameof(AppIcon));
                }
            }
        }

        [JsonIgnore]
        public ImageSource AppIcon => ExternalAppIconLoader.Get(AppExePath);

        private string? _windowProcessName;
        public string WindowProcessName
        {
            get => _windowProcessName ?? "";
            set
            {
                if (_windowProcessName != value)
                {
                    _windowProcessName = value;
                    OnPropertyChanged(nameof(WindowProcessName));
                    OnPropertyChanged(nameof(WindowProcessDisplay));
                }
            }
        }

        public string WindowProcessDisplay =>
            string.IsNullOrWhiteSpace(WindowProcessName) ? "" : $"ウィンドウ: {WindowProcessName}";

        private WindowTitleMatchMode _windowTitleMatchMode;
        public WindowTitleMatchMode WindowTitleMatchMode
        {
            get => _windowTitleMatchMode;
            set
            {
                if (_windowTitleMatchMode != value)
                {
                    _windowTitleMatchMode = value;
                    OnPropertyChanged(nameof(WindowTitleMatchMode));
                    OnPropertyChanged(nameof(TitleMatchModeText));
                }
            }
        }

        public string TitleMatchModeText =>
            WindowTitleMatchMode == Models.WindowTitleMatchMode.Contains ? "含む" : "完全一致";

        private int _listenPort;
        public int ListenPort
        {
            get => _listenPort;
            set
            {
                if (_listenPort != value)
                {
                    _listenPort = value;
                    OnPropertyChanged(nameof(ListenPort));
                    OnPropertyChanged(nameof(ListenPortDisplay));
                }
            }
        }

        public string ListenPortDisplay => ListenPort > 0 ? $"ポート: {ListenPort}" : "";

        public string GetWindowProcessName()
        {
            if (!string.IsNullOrWhiteSpace(WindowProcessName)) return WindowProcessName.Trim();
            if (!string.IsNullOrWhiteSpace(ProcessName)) return ProcessName.Trim();
            if (!string.IsNullOrWhiteSpace(AppExePath))
            {
                return Path.GetFileNameWithoutExtension(AppExePath) ?? "";
            }
            return "";
        }

        public bool HasSeparateWindowProcess()
        {
            if (string.IsNullOrWhiteSpace(AppExePath)) return false;
            var windowProcessName = GetWindowProcessName();
            if (string.IsNullOrWhiteSpace(windowProcessName)) return false;
            var launchName = Path.GetFileNameWithoutExtension(AppExePath) ?? "";
            return !string.Equals(windowProcessName, launchName, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeWindowProcessName(string windowProcessName, string processName, string appExePath)
        {
            var trimmed = (windowProcessName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return "";
            if (string.Equals(trimmed, processName, StringComparison.OrdinalIgnoreCase)) return "";
            var launchName = string.IsNullOrWhiteSpace(appExePath)
                ? ""
                : Path.GetFileNameWithoutExtension(appExePath) ?? "";
            if (string.Equals(trimmed, launchName, StringComparison.OrdinalIgnoreCase)
                && !IsBatchPath(appExePath))
            {
                return "";
            }
            return trimmed;
        }

        public static bool IsBatchPath(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private bool _isAutoStart = true;
        public bool IsAutoStart
        {
            get => _isAutoStart;
            set
            {
                if (_isAutoStart != value)
                {
                    _isAutoStart = value;
                    OnPropertyChanged(nameof(IsAutoStart));
                }
            }
        }

        private bool _isMinimized;
        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                if (_isMinimized != value)
                {
                    _isMinimized = value;
                    OnPropertyChanged(nameof(IsMinimized));
                    OnPropertyChanged(nameof(WindowStateText));
                }
            }
        }
        

        public string WindowStateText => IsMinimized ? "最小化" : "通常表示";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal static class ExternalAppIconLoader
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiSmallIcon = 0x000000001;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeNormal = 0x00000080;
        private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr IconHandle;
            public int IconIndex;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string path, uint fileAttributes, out ShFileInfo fileInfo, uint fileInfoSize, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr iconHandle);

        public static ImageSource Get(string path)
        {
            var key = "__fallback__";
            if (!string.IsNullOrWhiteSpace(path))
            {
                try { key = Path.GetFullPath(path); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    key = path.Trim();
                }
            }
            lock (Sync)
            {
                if (Cache.TryGetValue(key, out var cached)) return cached;
            }

            var source = Load(path) ?? Load("app.exe", useFileAttributes: true) ?? new DrawingImage();
            if (source.CanFreeze) source.Freeze();
            lock (Sync) Cache[key] = source;
            return source;
        }

        private static ImageSource? Load(string path, bool useFileAttributes = false)
        {
            var flags = ShgfiIcon | ShgfiSmallIcon;
            if (useFileAttributes || !File.Exists(path)) flags |= ShgfiUseFileAttributes;
            var result = SHGetFileInfo(
                path ?? string.Empty,
                FileAttributeNormal,
                out var info,
                (uint)Marshal.SizeOf<ShFileInfo>(),
                flags);
            if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero) return null;
            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(
                    info.IconHandle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DestroyIcon(info.IconHandle);
            }
        }
    }
}
