using JTSA.Forms;
using Xunit;

namespace JTSA.Tests;

public class ExternalAppRegistrationTests
{
    [Theory]
    [InlineData("JTSA", "", "", true)]
    [InlineData("other", " jtsa.EXE ", "", true)]
    [InlineData("other", "", @"C:\Apps\JTSA.exe", true)]
    [InlineData("other", "JTSA", @"C:\Apps\launch.cmd", true)]
    [InlineData("obs64", "", @"C:\Apps\obs64.exe", false)]
    [InlineData("JTSA.Helper", "", @"C:\Apps\JTSA.Helper.exe", false)]
    [InlineData("", "", @"C:\JTSA\other.exe", false)]
    [InlineData("", "", "", false)]
    public void DetectsJtsaInLaunchAndWindowTargets(
        string processName, string windowProcessName, string path, bool expected)
    {
        var app = new AppInfoForm
        {
            ProcessName = processName,
            WindowProcessName = windowProcessName,
            AppExePath = path
        };

        Assert.Equal(expected, app.IsJtsaApplication());
    }
}
