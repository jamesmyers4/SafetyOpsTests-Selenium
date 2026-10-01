using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>The splash page ("/") and the sign-in form ("/login").</summary>
public static class LoginPage
{
    public static readonly By UsernameInput = By.Id("username");
    public static readonly By PasswordInput = By.Id("password");
    public static readonly By LoginButton = X.Button("Login");
    public static readonly By ErrorBanner = X.Alert;

    /// <summary>Signs in through the UI from the splash page and waits for the home page.</summary>
    public static void SignInFromSplash(IWebDriver driver, string username, string password)
    {
        driver.Open("/");
        driver.Click(X.Button("Sign in"));
        driver.WaitForUrl("/login");
        Submit(driver, username, password);
        driver.WaitForUrl("/home$");
    }

    /// <summary>Fills and submits the sign-in form on the current page without waiting for a result.</summary>
    public static void Submit(IWebDriver driver, string username, string password)
    {
        driver.Fill(UsernameInput, username);
        driver.Fill(PasswordInput, password);
        driver.Click(LoginButton);
    }
}
