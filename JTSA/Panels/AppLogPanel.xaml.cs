using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
        private void AddLog(string log, SolidColorBrush color)
        {
            mainWindow.StatusTextBlock.Text = log;
            mainWindow.StatusTextBlock.Foreground = color;

            AppLogFormList.Insert(0,
                new AppLogForm() { 
                LogDateTime = DateTime.Now,
                Content = log,  
                Color = color
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddProcessLog(string log)
        {
            AddLog(log, NORMAL_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddSuccessLog(string log)
        {
            AddLog(log, SUCCSESS_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddErrorLog(string log)
        {
            AddLog(log, ERROR_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="log"></param>
        public void AddCriticalErrorLog(string log)
        {
            AddLog(log, CRITICAL_ERROR_COLOR);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <param name="successLog"></param>
        /// <param name="errorLog"></param>
        public void AddSwitchLog(bool isSuccess, string successLog, string errorLog)
        {
            if (isSuccess)
            {
                AddLog(successLog, SUCCSESS_COLOR);
            } 
            else
            {
                AddLog(errorLog, ERROR_COLOR);
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
