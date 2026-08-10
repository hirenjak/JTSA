using JTSA.Forms;
using Microsoft.VisualBasic.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JTSA.Panels
{
    public class ProcessLog
    {
        AppLogPanel appLogPanel;

        private SolidColorBrush NORMAL_COLOR = Brushes.White;
        private SolidColorBrush SUCCSESS_COLOR = Brushes.LightGreen;
        private SolidColorBrush ERROR_COLOR = Brushes.OrangeRed;
        private SolidColorBrush CRITICAL_ERROR_COLOR = Brushes.Red;

        public string TargetClassName { get; set; }
        public string ProcessName { get; set; }

        public ProcessLog(AppLogPanel appLogPanel, string targetClassName , string processName)
        {
            this.appLogPanel = appLogPanel;
            ProcessName = processName;
            TargetClassName = targetClassName;

        }

        public void EventStartLogWrite()
        {
            string logText = $"処理Start [ {TargetClassName} ： {ProcessName} ]";
            appLogPanel.AddLog(logText, NORMAL_COLOR);
        }

        public void EventEndLogWrite()
        {
            string logText = $"処理End [ {TargetClassName} ： {ProcessName} ]";
            appLogPanel.AddLog(logText, NORMAL_COLOR);
        }

        public void SuccessLogWrite()
        {
            string logText = $"[ {TargetClassName} ： {ProcessName} ] 処理完了";
            appLogPanel.AddLog(logText, SUCCSESS_COLOR);
        }

        public void SuccessLogWrite(string sucLogStr)
        {
            string logText = $"[ {TargetClassName} ： {ProcessName} ]";
            appLogPanel.AddLog(logText, SUCCSESS_COLOR);
        }

        public void ErrorLogWrite(string errLogStr)
        {
            string logText = $"ERROR [ {TargetClassName} ： {ProcessName} ] : {errLogStr}";
            appLogPanel.AddLog(logText, ERROR_COLOR);
        }

        public void CriticalErrorLogWrite(string errLogStr)
        {
            string logText = $"ERROR [ {TargetClassName} ： {ProcessName} ] : {errLogStr}";
            appLogPanel.AddLog(logText, CRITICAL_ERROR_COLOR);
        }
    }

    /// <summary>
    /// AppLogPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class AppLogPanel : UserControl
    {
        /// <summary> メインウィンドウ </summary>
        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;

        /// <summary>  </summary>
        public ObservableCollection<AppLogForm> AppLogFormList { get; } = [];

        private SolidColorBrush NORMAL_COLOR = Brushes.White;
        private SolidColorBrush SUCCSESS_COLOR = Brushes.LightGreen;
        private SolidColorBrush ERROR_COLOR = Brushes.OrangeRed;
        private SolidColorBrush CRITICAL_ERROR_COLOR = Brushes.Red;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public AppLogPanel()
        {
            InitializeComponent();

            // 画面紐づけ
            DataContext = this;
            AppLogFormList.Clear();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        private void AddLog(string traceClassName, string log, SolidColorBrush color)
        {
            mainWindow.StatusTextBlock.Text = log;
            mainWindow.StatusTextBlock.Foreground = color;

            AppLogFormList.Add(
                new AppLogForm() { 
                LogDateTime = DateTime.Now,
                Content = "【 " + traceClassName + "】 " + log,  
                Color = color
            });
        }

        public void AddLog(string logText, SolidColorBrush color)
        {
            AppLogFormList.Add(
                new AppLogForm()
                {
                    LogDateTime = DateTime.Now,
                    Content = logText,
                    Color = color
                });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public string ProcessStart(string traceClassName, string processTitle)
        {
            AddLog($"{traceClassName} ： {processTitle} ", "処理Start", NORMAL_COLOR);
            return processTitle;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void ProcessEnd(string traceClassName, string processTitle)
        {
            AddLog($"{traceClassName} ： {processTitle} ", "処理End", NORMAL_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void Success(string traceClassName, string log)
        {
            AddLog(traceClassName, "Success：" + log, SUCCSESS_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void Error(string traceClassName, string log)
        {
            AddLog(traceClassName, "Error：" + log, ERROR_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void CriticalError(string traceClassName, string log)
        {
            AddLog(traceClassName, log, CRITICAL_ERROR_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="successLog"></param>
        /// <param name="errorLog"></param>
        public void AddSwitchLog(bool isSuccess, string traceClassName, string successLog, string errorLog)
        {
            if (isSuccess)
            {
                AddLog(traceClassName, successLog, SUCCSESS_COLOR);
            } 
            else
            {
                AddLog(traceClassName, errorLog, ERROR_COLOR);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
