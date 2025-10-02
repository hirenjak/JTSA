using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JTSA.Panels
{
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

            AppLogFormList.Insert(0,
                new AppLogForm() { 
                LogDateTime = DateTime.Now,
                Content = "【 " + traceClassName + "】 " + log,  
                Color = color
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddProcessLog(string traceClassName, string processTitle ,string log)
        {
            AddLog($"{traceClassName} ： {processTitle} ", log, NORMAL_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddSuccessLog(string traceClassName, string log)
        {
            AddLog(traceClassName, log, SUCCSESS_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddErrorLog(string traceClassName, string log)
        {
            AddLog(traceClassName, log, ERROR_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddCriticalErrorLog(string traceClassName, string log)
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

        private void AppLogListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (sender as ListBox)?.SelectedItem;
            if (item != null)
            {
                // AppLogForm型でContentプロパティがある前提
                var contentProp = item.GetType().GetProperty("Content");
                if (contentProp != null)
                {
                    var content = contentProp.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        Clipboard.SetText(content);
                    }
                }
            }
        }
    }
}
