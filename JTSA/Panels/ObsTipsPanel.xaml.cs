using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace JTSA.Panels;

public partial class ObsTipsPanel : UserControl
{
    public ObsTipsPanel()
    {
        InitializeComponent();
    }

    private void TipsLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
