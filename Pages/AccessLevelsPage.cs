using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>Personnel → Access Levels: the org tree and, for admins, role grants and revokes.</summary>
public static class AccessLevelsPage
{
    public const string AdminsOnlyMessage = "Only administrators can view and manage role assignments.";

    public static readonly By Tree = By.XPath("//*[@role='tree'][@aria-label='Organization units']");
    public static readonly By TreeItems = By.XPath("//*[@role='tree'][@aria-label='Organization units']//li[@data-org-unit]");
    public static readonly By GrantButton = X.Button("Grant Role");

    /// <summary>Codes of the org units in the tree, in document order.</summary>
    public static string[] TreeCodes(IWebDriver driver) =>
        driver.FindElements(TreeItems).Select(li => li.GetDomAttribute("data-org-unit") ?? "").ToArray();

    /// <summary>Waits for the tree to list exactly <paramref name="codes"/>, in order.</summary>
    public static void ExpectTreeCodes(IWebDriver driver, params string[] codes)
    {
        driver.ExpectVisible(Tree);
        driver.Expect(() => TreeCodes(driver), c => c.SequenceEqual(codes), $"org tree [{string.Join(", ", codes)}]");
    }

    /// <summary>Role assignment rows for a user (by user name), org unit, and role.</summary>
    public static By AssignmentRow(string userName, string orgUnitName, string role) => By.XPath(AssignmentRowXPath(userName, orgUnitName, role));

    public static By RevokeButton(string userName, string orgUnitName, string role) =>
        By.XPath(AssignmentRowXPath(userName, orgUnitName, role) + "//button[normalize-space()='Revoke']");

    private static string AssignmentRowXPath(string userName, string orgUnitName, string role) =>
        $"//table/tbody/tr[td[1][contains(., {X.Literal($"({userName})")})]]" +
        $"[td[2][normalize-space()={X.Literal(orgUnitName)}]][td[3][normalize-space()={X.Literal(role)}]]";

    public static void Open(IWebDriver driver)
    {
        PersonnelPage.Open(driver);
        driver.Click(X.Link("Access Levels"));
        driver.WaitForUrl("/personnel/access-levels$");
        driver.ExpectVisible(Tree);
    }

    public static void Grant(IWebDriver driver, string userOption, string orgUnitName, string role)
    {
        driver.SelectByText(By.Id("grant-user"), userOption);
        SelectOrgUnit(driver, By.Id("grant-org-unit"), orgUnitName);
        driver.SelectByValue(By.Id("grant-role"), role);
        driver.Click(GrantButton);
    }

    /// <summary>Selects an org unit by name in an indented OrgUnitSelect.</summary>
    public static void SelectOrgUnit(IWebDriver driver, By select, string orgUnitName)
    {
        var element = new SelectElement(driver.Find(select));
        var option = element.Options.FirstOrDefault(o => o.Text.Trim() == orgUnitName);
        Assert.That(option, Is.Not.Null, $"No \"{orgUnitName}\" option");
        element.SelectByValue(option!.GetDomProperty("value")!);
    }

    /// <summary>Option texts of an OrgUnitSelect, without the indentation.</summary>
    public static string[] OrgUnitOptions(IWebDriver driver, By select) =>
        new SelectElement(driver.Find(select)).Options.Select(o => o.Text.Trim()).ToArray();
}
