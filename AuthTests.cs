using System.Text.RegularExpressions;
using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>Sign-in guard, return URLs, bad credentials, and sign-out. Tests start signed out.</summary>
[TestFixture]
public class AuthTests : AppTest
{
    protected override Credentials? SignInAs => null;
    protected override string? StartPath => null;

    [TestCase("/training")]
    [TestCase("/personnel/edit")]
    [TestCase("/incidents/new")]
    public void SignedOutVisitRedirectsToLoginWithReturnUrl(string path)
    {
        Driver.Open(path);
        Driver.WaitForUrl($"/login\\?returnUrl={Regex.Escape(Uri.EscapeDataString(path))}$");
        Driver.ExpectVisible(LoginPage.UsernameInput);
    }

    [Test]
    public void FramePagesRequireSignInToo()
    {
        Driver.Open("/medical-surveillance/create-frame");
        Driver.WaitForUrl(@"/login\?returnUrl=%2Fmedical-surveillance%2Fcreate-frame$");
    }

    [Test]
    public void SignInReturnsToTheRequestedPage()
    {
        Driver.Open("/personnel/edit");
        LoginPage.Submit(Driver, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        Driver.WaitForUrl("/personnel/edit$");
        Driver.ExpectVisible(X.Heading("Edit / Search User"));
    }

    [Test]
    public void WrongPasswordShowsErrorAndStaysOnLogin()
    {
        Driver.Open("/login");
        LoginPage.Submit(Driver, DemoUsers.Admin.Username, "not-the-password");
        Driver.ExpectText(LoginPage.ErrorBanner, "Invalid username or password.");
        Driver.WaitForUrl("/login$");
        Driver.ExpectEnabled(LoginPage.LoginButton);
    }

    [Test]
    public void UnknownUserShowsTheSameError()
    {
        Driver.Open("/login");
        LoginPage.Submit(Driver, "no-such-user", "whatever");
        Driver.ExpectText(LoginPage.ErrorBanner, "Invalid username or password.");
    }

    [Test]
    public void PressingEnterSubmitsTheForm()
    {
        Driver.Open("/login");
        Driver.Fill(LoginPage.UsernameInput, DemoUsers.Admin.Username);
        Driver.Fill(LoginPage.PasswordInput, DemoUsers.Admin.Password);
        Driver.Press(LoginPage.PasswordInput, Keys.Enter);
        Driver.WaitForUrl("/home$");
    }

    [TestCase("//example.com")]
    [TestCase("https://example.com/")]
    public void ReturnUrlCannotLeaveTheSite(string returnUrl)
    {
        Driver.Open($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        LoginPage.Submit(Driver, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        Driver.Expect(() => Driver.Url, url => url == $"{Settings.BaseUrl}/home", $"URL {Settings.BaseUrl}/home");
    }

    [Test]
    public void SignOutEndsTheSession()
    {
        Browser.SignIn(Driver, DemoUsers.Admin);
        Driver.Open("/home");
        Driver.ExpectText(Navigation.SignedInUser, "Demo Admin");

        Driver.Click(Navigation.SignOutLink);
        Driver.WaitForUrl("/login$");

        Driver.Open("/home");
        Driver.WaitForUrl(@"/login\?returnUrl=%2Fhome$");
        Assert.That(Browser.Fetch(Driver, "GET", "/api/auth/me"), Is.EqualTo(401));
    }

    [Test]
    public void ApiRejectsSignedOutRequests()
    {
        foreach (var url in new[] { "/api/personnel", "/api/training/classes", "/api/medical-surveillance/appointments", "/api/incidents", "/api/access/assignments" })
            Assert.That(Browser.Fetch(Driver, "GET", url), Is.EqualTo(401), url);
    }

    [TestCase("manager", "Demo Manager")]
    [TestCase("viewer", "Demo Viewer")]
    public void NavBarShowsTheSignedInUser(string username, string displayName)
    {
        LoginPage.SignInFromSplash(Driver, username, username);
        Driver.ExpectText(Navigation.SignedInUser, displayName);
    }
}
