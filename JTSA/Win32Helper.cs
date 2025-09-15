using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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


        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_RESTORE = 9;


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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool SetAppWindowRect(AppInfoForm app)
        {
            if (!(app.X.HasValue && app.Y.HasValue && app.Width.HasValue && app.Height.HasValue))
                return false;

            var proc = FindProcessFor(app);
            if (proc == null || proc.MainWindowHandle == IntPtr.Zero) return false;

            var hWnd = proc.MainWindowHandle;

            // 最小化/最大化解除
            if (Win32Helper.IsIconic(hWnd) || Win32Helper.IsZoomed(hWnd))
            {
                Win32Helper.ShowWindow(hWnd, SW_RESTORE);
                // ほんの少し待たせたいなら DispatcherTimer/Task.Delay を使う
            }

            return Win32Helper.SetWindowPos(hWnd, IntPtr.Zero, app.X.Value, app.Y.Value, app.Width.Value, app.Height.Value,
                                SWP_NOZORDER | SWP_NOACTIVATE);
        }


        /// <summary>
        /// AppInfo を受けて「タイトル一致」を優先してプロセスを見つける
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        private static Process? FindProcessFor(AppInfoForm app)
        {
            var list = Process.GetProcessesByName(app.ProcessName);
            // タイトル完全一致を最優先
            var exact = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle)
                                              && p.MainWindowTitle == app.WindowTitle);
            if (exact != null) return exact;

            // タイトル部分一致（保険）
            var partial = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.MainWindowTitle)
                                                && app.WindowTitle != null
                                                && p.MainWindowTitle.Contains(app.WindowTitle));
            if (partial != null) return partial;

            // 最後の手段：メインウィンドウハンドルがあるもの
            return list.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        }
    }
}
