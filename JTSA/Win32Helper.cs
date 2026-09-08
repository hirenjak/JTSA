using JTSA.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JTSA
{
    static class Win32Helper
    {
        // Win32 APIの宣言
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(System.Drawing.Point p);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

        // 既存のP/Invokeの下に追加
        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        public const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);   // 最小化？

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);   // 最大化？

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private const uint GW_OWNER = 4;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINIMIZED = 2;

        public sealed class TopLevelWindowInfo
        {
            public IntPtr Handle { get; init; }
            public int ProcessId { get; init; }
            public required string ProcessName { get; init; }
            public required string Title { get; init; }
            public string AppExePath { get; init; } = "";
        }

        public static List<TopLevelWindowInfo> ListTopLevelWindows()
        {
            var windows = new List<TopLevelWindowInfo>();
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd) && !IsIconic(hWnd)) return true;
                if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return true;

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title)) return true;

                GetWindowThreadProcessId(hWnd, out var processId);
                string processName;
                var appExePath = "";
                try
                {
                    using var process = Process.GetProcessById((int)processId);
                    processName = process.ProcessName;
                    try { appExePath = process.MainModule?.FileName ?? ""; } catch { }
                }
                catch
                {
                    return true;
                }

                windows.Add(new TopLevelWindowInfo
                {
                    Handle = hWnd,
                    ProcessId = (int)processId,
                    ProcessName = processName,
                    Title = title,
                    AppExePath = appExePath
                });
                return true;
            }, IntPtr.Zero);

            return windows
                .OrderBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool TryFindUniqueWindow(AppInfoForm app, out IntPtr hWnd, out string error)
        {
            hWnd = IntPtr.Zero;
            error = "";
            if (string.IsNullOrWhiteSpace(app.WindowTitle))
            {
                error = "ウィンドウタイトルが未設定のため、対象を特定できません。";
                return false;
            }

            var processName = app.GetWindowProcessName();
            if (string.IsNullOrWhiteSpace(processName))
            {
                error = "ウィンドウのプロセス名が未設定のため、対象を特定できません。";
                return false;
            }

            var matches = ListTopLevelWindows()
                .Where(item => string.Equals(item.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                .Where(item => TitleMatches(item.Title, app.WindowTitle, app.WindowTitleMatchMode))
                .ToList();

            if (matches.Count == 0)
            {
                error = $"一致するウィンドウが見つかりません: {processName} / {app.WindowTitle}";
                return false;
            }

            if (matches.Count > 1)
            {
                error = $"一致するウィンドウが {matches.Count} 件あるため操作しません。タイトル条件を狭めてください。";
                return false;
            }

            hWnd = matches[0].Handle;
            return true;
        }

        private static bool TitleMatches(string actualTitle, string expectedTitle, Models.WindowTitleMatchMode matchMode)
        {
            if (matchMode == Models.WindowTitleMatchMode.Contains)
            {
                return actualTitle.Contains(expectedTitle, StringComparison.Ordinal);
            }

            return string.Equals(actualTitle, expectedTitle, StringComparison.Ordinal);
        }

        public static string GetWindowTitle(IntPtr hWnd)
        {
            var builder = new StringBuilder(512);
            _ = GetWindowTextW(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public static bool TryGetRestoredWindowRect(IntPtr hWnd, out RECT rect, out bool isMinimized)
        {
            rect = default;
            isMinimized = false;
            if (hWnd == IntPtr.Zero) return false;

            var placement = new WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<WINDOWPLACEMENT>()
            };
            if (!GetWindowPlacement(hWnd, ref placement)) return false;

            isMinimized = placement.showCmd == SW_SHOWMINIMIZED || IsIconic(hWnd);
            rect = placement.rcNormalPosition;
            return rect.Right > rect.Left && rect.Bottom > rect.Top;
        }


        /// <summary>
        /// ウィンドウ位置を制御
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static bool SetAppWindowRect(string processName, int x, int y, int width, int height)
        {
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return false;

            return Win32Helper.SetWindowPos(proc.MainWindowHandle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static bool SetAppWindowRect(AppInfoForm app) => SetAppWindowRect(app, out _);

        public static bool SetAppWindowRect(AppInfoForm app, out string error)
        {
            error = "";
            if (!(app.X.HasValue && app.Y.HasValue && app.Width.HasValue && app.Height.HasValue))
            {
                error = "保存された位置がありません。";
                return false;
            }

            if (!TryFindUniqueWindow(app, out var hWnd, out error)) return false;

            if (IsIconic(hWnd) || IsZoomed(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            var moved = SetWindowPos(hWnd, IntPtr.Zero, app.X.Value, app.Y.Value, app.Width.Value, app.Height.Value,
                                SWP_NOZORDER | SWP_NOACTIVATE);
            if (!moved)
            {
                error = $"配置失敗: {app.GetWindowProcessName()}";
                return false;
            }

            if (app.IsMinimized)
            {
                ShowWindow(hWnd, SW_MINIMIZE);
            }
            return true;
        }

        private const uint WM_CLOSE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static bool TryCloseUniqueWindow(AppInfoForm app, out string error)
        {
            if (!TryFindUniqueWindow(app, out var hWnd, out error)) return false;
            if (!PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
            {
                error = $"ウィンドウを閉じられませんでした: {app.GetWindowProcessName()}";
                return false;
            }
            error = "";
            return true;
        }
    }
}
