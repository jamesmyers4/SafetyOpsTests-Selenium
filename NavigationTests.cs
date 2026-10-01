using System.Text.RegularExpressions;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

[TestFixture]
public class NavigationTests : AppTest
{
    [Test]
    public void UiSignInFromSplashLandsOnHome()
    {
        SignOut();
        LoginPage.SignInFromSplash(Driver, DemoUsers.Admin.Username, DemoUsers.Admin.Password);
        Driver.ExpectVisible(X.Heading("Welcome to SAFETYOPS"));
        Driver.ExpectText(Navigation.SignedInUser, "Demo Admin");
    }

    [Test]
    public void ModulesMenuListsEveryModule()
    {
        Driver.Click(Navigation.ModulesLink);
        Driver.ExpectTexts(Navigation.ModulesMenuLinks, Navigation.Modules);
    }

    [TestCase(Navigation.Personnel, "/personnel", "Personnel")]
    [TestCase(Navigation.Training, "/training", "Training")]
    [TestCase(Navigation.MedicalSurveillance, "/medical-surveillance", "Medical Surveillance")]
    [TestCase(Navigation.IncidentReports, "/incidents", "Incident Reports")]
    public void ModulesMenuOpensModule(string module, string path, string heading)
    {
        Navigation.OpenModule(Driver, module);
        Driver.WaitForUrl(Regex.Escape(path) + "$");
        Driver.ExpectVisible(X.Heading(heading));
    }
}
