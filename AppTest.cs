using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Config;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Base fixture: starts a Chrome session per test, signs it in through the API, and opens
/// <see cref="StartPath"/>. Override the virtual members to sign in as someone else, stay signed
/// out, or start elsewhere. Failed tests leave a screenshot and the page source.
///
/// Each test also gets <see cref="Api"/>, an API session of its own signed in as Admin (unaffected
/// by the browser's user), and <see cref="Data"/>, which creates test data through it and deletes
/// that data after the test, pass or fail.
/// </summary>
public abstract class AppTest
{
    private readonly List<IWebDriver> _otherBrowsers = [];
    private IWebDriver? _driver;
    private AppApi? _api;
    private TestDataScope? _data;

    protected static TestSettings Settings => ConfigLoader.Settings;

    /// <summary>Who each test signs in as, or null to stay signed out.</summary>
    protected virtual Credentials? SignInAs => DemoUsers.Admin;

    /// <summary>Page opened after sign-in, or null to stay where sign-in left the browser.</summary>
    protected virtual string? StartPath => "/home";

    protected IWebDriver Driver => _driver ?? throw new InvalidOperationException("Driver is available from SetUp on.");

    /// <summary>Admin API session for arranging, verifying, and cleaning up data.</summary>
    protected AppApi Api => _api ?? throw new InvalidOperationException("Api is available from SetUp on.");

    /// <summary>This test's data: create through it, or put <see cref="TestDataScope.Token"/> in UI-created records.</summary>
    protected TestDataScope Data => _data ?? throw new InvalidOperationException("Data is available from SetUp on.");

    /// <summary>Where failed tests leave a screenshot and page source (uploaded by CI).</summary>
    public static string ArtifactsDirectory => Path.Combine(TestContext.CurrentContext.WorkDirectory, "test-artifacts");

    [SetUp]
    public void StartBrowserAndSignIn()
    {
        _api = AppApi.SignIn(DemoUsers.Admin);
        _data = new TestDataScope(_api);

        _driver = Browser.Start();
        if (SignInAs is { } user)
            Browser.SignIn(_driver, user);
        if (StartPath is { } path)
            _driver.Open(path);
    }

    [TearDown]
    public void QuitBrowsers()
    {
        try
        {
            SaveArtifactsIfFailed();
            _data?.CleanUp();
        }
        finally
        {
            foreach (var browser in _otherBrowsers) browser.Dispose();
            _otherBrowsers.Clear();
            _driver?.Dispose();
            _driver = null;
            _api?.Dispose();
            _api = null;
            _data = null;
        }
    }

    private void SaveArtifactsIfFailed()
    {
        if (_driver is null || TestContext.CurrentContext.Result.Outcome.Status is not (TestStatus.Failed or TestStatus.Warning))
            return;

        var name = string.Concat(TestContext.CurrentContext.Test.FullName.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '"' ? '_' : c));
        var basePath = Path.Combine(ArtifactsDirectory, name);
        Directory.CreateDirectory(ArtifactsDirectory);
        try
        {
            if (_driver.WindowHandles.Count > 0) _driver.SwitchTo().Window(_driver.WindowHandles[0]);
            _driver.SwitchTo().DefaultContent();
            ((ITakesScreenshot)_driver).GetScreenshot().SaveAsFile(basePath + ".png");
            File.WriteAllText(basePath + ".html", _driver.PageSource);
            TestContext.Out.WriteLine($"Screenshot and page source saved to {basePath}.*");
        }
        catch (WebDriverException e)
        {
            TestContext.Out.WriteLine($"Could not save failure artifacts: {e.Message}");
        }
    }

    /// <summary>Signs this test's browser out and back in as <paramref name="user"/>.</summary>
    protected void SwitchUser(Credentials user)
    {
        Browser.SignOut(Driver);
        Browser.SignIn(Driver, user);
    }

    protected void SignOut() => Browser.SignOut(Driver);

    /// <summary>A second, independent browser signed in as <paramref name="user"/>; quit after the test.</summary>
    protected IWebDriver OpenBrowserAs(Credentials user)
    {
        var browser = Browser.Start();
        _otherBrowsers.Add(browser);
        Browser.SignIn(browser, user);
        return browser;
    }
}
