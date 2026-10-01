using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>Personnel home, Add User, Edit/Search User, and Edit User pages.</summary>
public static class PersonnelPage
{
    private static readonly Random Random = new();

    private static readonly Dictionary<string, (string Button, string DialogTitle)> SelectLists = new()
    {
        ["Department"] = ("Open Select List", "Select a Department"),
        ["Subscriptions"] = ("Subscriptions", "Subscriptions"),
        ["Employee Category"] = ("Open Select List", "Select an Employee Category"),
    };

    public static readonly By FirstName = X.Label("First Name");
    public static readonly By LastName = X.Label("Last Name");
    public static readonly By MiddleName = X.Label("Middle Name");
    public static readonly By OrgUnit = By.Id("org-unit");
    public static readonly By UpdateButton = X.Button("Update");
    public static readonly By AddUserButton = X.Button("Add User");
    public static readonly By FilterUsers = X.Label("Filter users");
    public static readonly By GenderCombobox = By.CssSelector("[role='combobox'][aria-label='Gender']");

    public static void Open(IWebDriver driver)
    {
        Navigation.OpenModule(driver, Navigation.Personnel);
        driver.ExpectVisible(X.Heading("Personnel"));
    }

    public static void OpenAddUser(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Add New User"));
        driver.WaitForUrl("/personnel/create$");
        driver.ExpectVisible(X.Heading("Add New User"));
    }

    public static void OpenEditSearch(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Edit/Search User"));
        driver.WaitForUrl("/personnel/edit$");
        driver.ExpectVisible(X.Heading("Edit / Search User"));
    }

    /// <summary>Searches on the Edit/Search User page.</summary>
    public static void SearchUsers(IWebDriver driver, string term)
    {
        driver.Fill(X.Label("Search users"), term);
        driver.Click(X.Button("Search"));
        driver.ExpectCount(X.Status("Loading"), 0);
    }

    /// <summary>Rows in a results table (header excluded).</summary>
    public static readonly By SearchResultRows = X.TableRows;

    /// <summary>Rows of the Personnel home table that contain <paramref name="text"/>.</summary>
    public static By RowWith(string text) => By.XPath($"//table/tbody/tr[contains(., {X.Literal(text)})]");

    /// <summary>The Delete button in the Personnel home row that contains <paramref name="text"/>.</summary>
    public static By DeleteButtonIn(string text) => By.XPath($"//table/tbody/tr[contains(., {X.Literal(text)})]//button[normalize-space()='Delete']");

    /// <summary>Opens the only search result for editing.</summary>
    public static void OpenOnlyResult(IWebDriver driver)
    {
        driver.ExpectCount(SearchResultRows, 1);
        driver.Click(By.XPath("//table/tbody/tr//a[normalize-space()='Edit']"));
        driver.ExpectVisible(X.Heading("Edit User"));
        driver.ExpectNotValue(FirstName, "");
    }

    /// <summary>Picks a random option in one of the checkbox select dialogs and returns it.</summary>
    public static string PickRandomFromSelectList(IWebDriver driver, string field)
    {
        var (button, title) = SelectLists[field];
        var fieldRow = By.XPath($"//label[normalize-space()={X.Literal(field)}]/..");
        driver.Click(By.XPath($"//label[normalize-space()={X.Literal(field)}]/..//button[normalize-space()={X.Literal(button)}]"));

        var dialog = By.XPath($"//*[@role='dialog'][@aria-label={X.Literal(title)}]");
        var rows = driver.Find(dialog).FindElements(By.CssSelector("tbody tr"));
        Assert.That(rows, Is.Not.Empty, $"No options in the \"{title}\" dialog");

        var pick = rows[Random.Next(rows.Count)];
        var value = pick.FindElements(By.TagName("td"))[1].Text.Trim();
        pick.FindElement(By.CssSelector("input[type='checkbox']")).Click();
        driver.Find(dialog).FindElement(By.XPath(".//button[normalize-space()='Save']")).Click();
        driver.ExpectCount(dialog, 0);
        driver.ExpectContainsText(fieldRow, value);
        return value;
    }

    /// <summary>Add User's Gender is a native select.</summary>
    public static string SelectRandomGender(IWebDriver driver)
    {
        var select = new SelectElement(driver.Find(By.CssSelector("select[aria-label='Gender']")));
        var options = select.Options.Select(o => o.GetDomProperty("value") ?? "").Where(v => v != "").ToList();
        var pick = options[Random.Next(options.Count)];
        select.SelectByValue(pick);
        return pick;
    }

    /// <summary>Edit User's Gender is a custom combobox with a listbox.</summary>
    public static string PickRandomGender(IWebDriver driver)
    {
        driver.Click(GenderCombobox);
        var options = driver.Find(By.CssSelector("[role='listbox'][aria-label='Gender']")).FindElements(By.CssSelector("[role='option']"));
        var pick = options[Random.Next(options.Count)];
        var value = pick.Text.Trim();
        pick.Click();
        driver.ExpectCount(By.CssSelector("[role='listbox'][aria-label='Gender']"), 0);
        return value;
    }

    /// <summary>Picks a random org unit in the Organization Unit select and returns its name.</summary>
    public static string SelectRandomOrgUnit(IWebDriver driver)
    {
        var select = new SelectElement(driver.Find(OrgUnit));
        var index = Random.Next(select.Options.Count);
        var name = select.Options[index].Text.Trim();
        select.SelectByIndex(index);
        return name;
    }
}
