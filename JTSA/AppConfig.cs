using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JTSA
{
    public static class AppConfig
    {
        public static string UserName { get; private set; } = "";

        public static void LoadConfig()
        {
            var iniPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.ini");

            if (!System.IO.File.Exists(iniPath))
            {
                MessageBox.Show($"設定ファイルが見つかりません: {iniPath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddIniFile("appsettings.ini", optional: true, reloadOnChange: true)
                .Build();

            UserName = config["Auth:UserName"] ?? "";
        }
    }
}
