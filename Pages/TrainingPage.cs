using System.Text.RegularExpressions;
using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>
/// The Training shell page and the create/search/edit class forms it hosts in an iframe. A frame
/// posts a validated draft to the shell (postMessage); the shell's Save button writes it.
///
/// Each method says where it leaves the driver: "in the frame" or "on the shell" (the top-level
/// page). Use <see cref="EnterFrame"/> and <see cref="Shell"/> to move between them.
/// </summary>
public static class TrainingPage
{
    public const string ElectricalLowVoltage = "Electrical - Low Voltage";

    public static readonly By FrameElement = By.CssSelector("iframe[title='Training Frame']");

    // On the shell
    public static readonly By ShellSaveButton = X.Button("Save");
    public static readonly By ShellAlert = X.Alert;
    public static By ShellStatus(string text) => X.Status(text);

    // In the frame
    public static readonly By FrameAlert = X.Alert;
    public static readonly By FrameAlertLines = By.XPath("//*[@role='alert']/div");
    public static readonly By ClassDate = By.Id("class-date");
    public static readonly By Location = By.Id("class-location");
    public static readonly By CourseTitle = By.Id("course-title");
    public static readonly By CoursePickerButton = By.Id("course-picker-button");
    public static readonly By SearchResultRows = X.TableRows;
    public static By Button(string name) => X.Button(name);

    public static void EnterFrame(IWebDriver driver) => Popups.EnterFrame(driver, FrameElement);

    public static void Shell(IWebDriver driver) => driver.SwitchTo().DefaultContent();

    /// <summary>Opens Training. Leaves the driver on the shell.</summary>
    public static void Open(IWebDriver driver)
    {
        Shell(driver);
        Navigation.OpenModule(driver, Navigation.Training);
        driver.ExpectVisible(X.Heading("Training"));
    }

    /// <summary>Opens Training; the frame starts on the class search. Leaves the driver in the frame.</summary>
    public static void OpenSearch(IWebDriver driver)
    {
        Open(driver);
        EnterFrame(driver);
        driver.ExpectVisible(X.Link("Find / Search Classes"));
    }

    /// <summary>Leaves the driver in the frame.</summary>
    public static void OpenCreateClass(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Create Class"));
        EnterFrame(driver);
        driver.ExpectVisible(X.Heading("Create Training Class"));
    }

    /// <summary>In the frame: opens the calendar and picks today.</summary>
    public static void PickToday(IWebDriver driver)
    {
        driver.Click(ClassDate);
        driver.Click(X.Link(DateTime.Today.Day.ToString()));
        driver.ExpectValue(ClassDate, DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>
    /// In the frame: picks a course in the course picker popup, which posts it back to the frame
    /// and closes. Leaves the driver in the frame.
    /// </summary>
    public static void SelectCourseViaPopup(IWebDriver driver, string courseName)
    {
        var opener = Popups.OpenFrom(driver, CoursePickerButton);
        driver.Click(X.Button("Search"));
        Popups.ClickAndExpectClose(driver, X.Link(courseName), opener);
        EnterFrame(driver);
        driver.ExpectValue(CourseTitle, courseName);
    }

    /// <summary>In the frame: today, the given location, and <see cref="ElectricalLowVoltage"/>.</summary>
    public static void FillValidClass(IWebDriver driver, string location)
    {
        PickToday(driver);
        driver.Fill(Location, location);
        SelectCourseViaPopup(driver, ElectricalLowVoltage);
    }

    /// <summary>
    /// After Create/Update, waits until the frame has either handed the draft to the shell (Save
    /// appears) or shown the duplicate warning. Returns true if it was the duplicate warning.
    /// Leaves the driver in the frame.
    /// </summary>
    public static bool WaitForSaveOrDuplicate(IWebDriver driver, string continueButton)
    {
        var deadline = DateTime.UtcNow + Ui.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            EnterFrame(driver);
            if (driver.FindElements(Button(continueButton)).Any(e => e.Displayed)) return true;
            Shell(driver);
            if (driver.FindElements(ShellSaveButton).Any(e => e.Displayed))
            {
                EnterFrame(driver);
                return false;
            }
            Thread.Sleep(100);
        }
        throw new WebDriverTimeoutException("Neither the shell's Save button nor the duplicate warning appeared.");
    }

    /// <summary>
    /// Continues past the duplicate warning if it appears, then waits for the shell's Save button.
    /// Leaves the driver on the shell.
    /// </summary>
    public static bool DismissDuplicateWarningIfPresent(IWebDriver driver, string continueButton)
    {
        var wasDuplicate = WaitForSaveOrDuplicate(driver, continueButton);
        if (wasDuplicate) driver.Click(Button(continueButton));
        Shell(driver);
        driver.ExpectVisible(ShellSaveButton);
        return wasDuplicate;
    }

    /// <summary>On the shell: clicks Save for a new class and returns the created class id from the detail page URL.</summary>
    public static int SaveNewClass(IWebDriver driver)
    {
        driver.Click(ShellSaveButton);
        driver.WaitForUrl(@"/training/classes/\d+$");
        driver.ExpectVisible(X.Heading("Training Class Details"));
        return ClassIdFromUrl(driver.Url);
    }

    public static int ClassIdFromUrl(string url) => int.Parse(Regex.Match(url, @"/training/classes/(\d+)").Groups[1].Value);

    /// <summary>In the frame: searches classes.</summary>
    public static void SearchClasses(IWebDriver driver, string term)
    {
        driver.Fill(By.Id("class-search"), term);
        driver.Click(Button("Search"));
        driver.ExpectCount(X.Status("Loading"), 0);
    }

    /// <summary>In the frame: opens the only search result in the edit form.</summary>
    public static void OpenOnlyResult(IWebDriver driver)
    {
        driver.ExpectCount(SearchResultRows, 1);
        driver.Click(By.CssSelector("table tbody tr a"));
        driver.ExpectVisible(X.Heading("Edit Training Class"));
    }
}
