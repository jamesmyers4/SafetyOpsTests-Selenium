using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>The shared nav bar: the Modules menu, the signed-in user, and Sign out.</summary>
public static class Navigation
{
    public const string Personnel = "Personnel";
    public const string Training = "Training";
    public const string MedicalSurveillance = "Medical Surveillance";
    public const string IncidentReports = "Incident Reports";

    public static readonly string[] Modules = [Personnel, Training, MedicalSurveillance, IncidentReports];

    public static readonly By ModulesLink = By.Id("modules-menu");
    public static readonly By ModulesMenuLinks = By.XPath("//*[@role='menu']//a");
    public static readonly By SignedInUser = By.XPath("//*[@aria-label='Signed in user']");
    public static readonly By SignOutLink = X.Link("Sign out");

    /// <summary>Opens the Modules menu and picks a module by its menu label.</summary>
    public static void OpenModule(IWebDriver driver, string module)
    {
        driver.Click(ModulesLink);
        driver.Click(By.XPath($"//*[@role='menu']//a[normalize-space()={X.Literal(module)}]"));
    }
}
