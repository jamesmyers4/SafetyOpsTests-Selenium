using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SafetyOpsTestsSelenium.Config;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Helpers;

/// <summary>Starts Chrome and signs it in or out without going through the UI.</summary>
public static class Browser
{
    /// <summary>
    /// A new Chrome session. Selenium Manager finds or downloads a matching ChromeDriver. Dialogs
    /// (window.confirm) stay open until a test accepts or dismisses them.
    /// </summary>
    public static IWebDriver Start()
    {
        var options = new ChromeOptions
        {
            AcceptInsecureCertificates = true, // the dev profile serves https://localhost with a dev certificate
            UnhandledPromptBehavior = UnhandledPromptBehavior.Ignore,
        };
        if (ConfigLoader.Settings.Headless) options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1400,1000");
        return new ChromeDriver(options);
    }

    /// <summary>
    /// Signs in by calling the login API from inside the browser, so Chrome stores the HttpOnly
    /// auth cookie itself; pages, iframes, and popups then share it. Leaves the browser on /healthz.
    /// </summary>
    public static void SignIn(IWebDriver driver, Credentials user)
    {
        driver.Open("/healthz");
        var status = Fetch(driver, "POST", "/api/auth/login", new { username = user.Username, password = user.Password });
        if (status is not (200 or 204))
            throw new InvalidOperationException($"API sign-in as '{user.Username}' failed: {status}");
    }

    /// <summary>
    /// Leaves the app, then drops the auth cookie. Leaving first matters: a page still loading its
    /// data would get a 401 and redirect itself to /login, cutting off the test's next navigation.
    /// </summary>
    public static void SignOut(IWebDriver driver)
    {
        driver.Open("/healthz");
        driver.Manage().Cookies.DeleteAllCookies();
    }

    /// <summary>Calls the API as the browser's signed-in user and returns the HTTP status.</summary>
    public static int Fetch(IWebDriver driver, string method, string url, object? body = null)
    {
        if (!driver.Url.StartsWith(ConfigLoader.Settings.BaseUrl, StringComparison.OrdinalIgnoreCase))
            driver.Open("/healthz"); // fetch needs a page on the app's origin

        const string script = """
            const [method, url, body, done] = arguments;
            fetch(url, { method, credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: body ?? undefined })
                .then(r => done(r.status), () => done(-1));
            """;
        var json = body is null ? null : System.Text.Json.JsonSerializer.Serialize(body);
        return Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteAsyncScript(script, method, url, json));
    }
}
