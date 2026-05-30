using Ritmo.Core.Focus;

namespace Ritmo.Core.Tests;

public class BrowserLaunchTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" -osint -url \"%1\"", "C:\\Program Files\\Mozilla Firefox\\firefox.exe")]
    [InlineData("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\" --single-argument %1", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe")]
    [InlineData("C:\\edge\\msedge.exe %1", "C:\\edge\\msedge.exe")]
    public void ExtractExePath(string cmd, string expected)
        => Assert.Equal(expected, BrowserLaunch.ExtractExePath(cmd));

    [Fact]
    public void ExtractExePath_vacio_da_null()
    {
        Assert.Null(BrowserLaunch.ExtractExePath(null));
        Assert.Null(BrowserLaunch.ExtractExePath("   "));
    }

    [Theory]
    [InlineData("C:\\x\\firefox.exe", BrowserFamily.Firefox)]
    [InlineData("C:\\x\\chrome.exe", BrowserFamily.Chromium)]
    [InlineData("C:\\x\\msedge.exe", BrowserFamily.Chromium)]
    [InlineData("C:\\x\\brave.exe", BrowserFamily.Chromium)]
    [InlineData("C:\\x\\notepad.exe", BrowserFamily.Other)]
    [InlineData("", BrowserFamily.Other)]
    public void FamilyFromExe(string exe, BrowserFamily expected)
        => Assert.Equal(expected, BrowserLaunch.FamilyFromExe(exe));

    [Fact]
    public void NewWindowArgs_chromium()
    {
        var args = BrowserLaunch.NewWindowArgs(BrowserFamily.Chromium, ["https://a.com", "https://b.com"]);
        Assert.Equal(["--new-window", "https://a.com", "https://b.com"], args);
    }

    [Fact]
    public void NewWindowArgs_firefox()
    {
        var args = BrowserLaunch.NewWindowArgs(BrowserFamily.Firefox, ["https://a.com", "https://b.com", "https://c.com"]);
        Assert.Equal(["-new-window", "https://a.com", "-new-tab", "https://b.com", "-new-tab", "https://c.com"], args);
    }

    [Fact]
    public void NewWindowArgs_other_o_vacio()
    {
        Assert.Empty(BrowserLaunch.NewWindowArgs(BrowserFamily.Other, ["https://a.com"]));
        Assert.Empty(BrowserLaunch.NewWindowArgs(BrowserFamily.Chromium, []));
    }
}
