using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>
/// Medical Surveillance: the create page (form in #create-frame), the search/edit page (form in
/// #edit-frame), the person picker popup, and the work task picker nested inside the form's frame.
///
/// Methods taking a <c>frame</c> expect the driver in that form frame and leave it there.
/// </summary>
public static class MedicalSurveillancePage
{
    public const string SearchHeading = "Search / Edit Medical Surveillance Records";
    public const string UpdatedMessage = "Record updated successfully";

    private static readonly Random Random = new();

    public static readonly By CreateFrame = By.Id("create-frame");
    public static readonly By EditFrame = By.Id("edit-frame");
    public static readonly By WorkTaskPickerFrame = By.CssSelector("iframe[title='Work Task Picker']");

    // In a form frame
    public static readonly By AppointmentDate = By.Id("appointment-date");
    public static readonly By PersonEvaluated = By.Id("person-evaluated");
    public static readonly By StressorRows = By.XPath("//table[.//th[normalize-space()='Stressor ID']]/tbody/tr");
    public static readonly By FrameAlert = X.Alert;
    public static readonly By FrameAlertLines = By.XPath("//*[@role='alert']/div");
    public static By Button(string name) => X.Button(name);

    // On the page
    public static readonly By SearchResultRows = X.TableRows;

    public static void Open(IWebDriver driver)
    {
        driver.SwitchTo().DefaultContent();
        Navigation.OpenModule(driver, Navigation.MedicalSurveillance);
        driver.ExpectVisible(X.Heading("Medical Surveillance"));
    }

    /// <summary>Leaves the driver in the create frame.</summary>
    public static void OpenCreate(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Create"));
        Popups.EnterFrame(driver, CreateFrame);
        driver.ExpectVisible(X.Heading("Create Medical Surveillance Record"));
    }

    /// <summary>Leaves the driver on the page.</summary>
    public static void OpenSearch(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Edit / Search"));
        driver.ExpectVisible(X.Heading(SearchHeading));
    }

    /// <summary>On the page.</summary>
    public static void Search(IWebDriver driver, string term)
    {
        driver.Fill(X.Label("Search medical surveillance records"), term);
        driver.Click(X.Button("Search"));
        driver.ExpectCount(X.Status("Loading"), 0);
    }

    /// <summary>On the page: opens the only search result in the edit frame and waits for it to load. Leaves the driver in the frame.</summary>
    public static void OpenOnlyResult(IWebDriver driver)
    {
        driver.ExpectCount(SearchResultRows, 1);
        driver.Click(By.XPath("//table/tbody/tr//a[normalize-space()='Edit']"));
        Popups.EnterFrame(driver, EditFrame);
        driver.ExpectVisible(X.Heading("Edit Medical Surveillance Record"));
        driver.ExpectNotValue(PersonEvaluated, "");
    }

    /// <summary>In a form frame: picks today from the calendar icon next to Appointment Date.</summary>
    public static void PickTodayFromCalendar(IWebDriver driver)
    {
        driver.Click(By.XPath("//*[local-name()='svg'][*[local-name()='path']]"));
        driver.Click(X.Link(DateTime.Today.Day.ToString()));
        driver.ExpectValue(AppointmentDate, DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>
    /// In the form frame <paramref name="frame"/>: picks a random person in the person picker popup
    /// (it posts the choice back and closes). Returns the chosen name. <paramref name="except"/>
    /// excludes a name, to force a change.
    /// </summary>
    public static string PickRandomPersonEvaluated(IWebDriver driver, By frame, string? except = null)
    {
        var opener = Popups.OpenFrom(driver, By.Id("person-picker-button"));
        driver.Click(X.Button("Search"));
        var links = By.CssSelector("table tbody tr a");
        driver.ExpectVisible(links);
        var names = driver.Texts(links).Where(n => n != except).ToList();
        Assert.That(names, Is.Not.Empty, "No people in the person picker");
        var name = names[Random.Next(names.Count)];
        Popups.ClickAndExpectClose(driver, X.Link(name), opener);
        Popups.EnterFrame(driver, frame);
        driver.ExpectValue(PersonEvaluated, name);
        return name;
    }

    /// <summary>In the form frame <paramref name="frame"/>: adds one random work task through the nested picker frame; returns the task's name.</summary>
    public static string AddRandomWorkTask(IWebDriver driver, By frame)
    {
        driver.Click(X.Link("Add Work Task(s)"));
        Popups.EnterFrame(driver, frame, WorkTaskPickerFrame);
        driver.Click(X.Button("Search"));
        driver.ExpectVisible(By.CssSelector("tbody tr"));
        var rows = driver.FindElements(By.CssSelector("tbody tr"));
        var pick = rows[Random.Next(rows.Count)];
        var task = pick.FindElements(By.TagName("td"))[1].Text.Trim();
        pick.FindElement(By.CssSelector("input[type='checkbox']")).Click();
        driver.Click(X.Button("Save"));

        Popups.EnterFrame(driver, frame);
        driver.ExpectCount(WorkTaskPickerFrame, 0);
        driver.ExpectVisible(StressorRows);
        return task;
    }

    /// <summary>In a form frame: sets every Exam Type select to a random exam type; returns stressor id → exam type.</summary>
    public static Dictionary<string, string> SelectExamTypesForAllStressors(IWebDriver driver)
    {
        var chosen = new Dictionary<string, string>();
        var rows = driver.FindElements(StressorRows);
        Assert.That(rows, Is.Not.Empty, "No stressors to set exam types for");
        foreach (var row in rows)
        {
            var stressorId = row.FindElement(By.TagName("td")).Text.Trim();
            var select = new SelectElement(row.FindElement(By.CssSelector("select[id^='exam-type-']")));
            var options = select.Options.Select(o => o.GetDomProperty("value") ?? "").Where(v => v != "").ToList();
            var examType = options[Random.Next(options.Count)];
            select.SelectByValue(examType);
            chosen[stressorId] = examType;
        }
        return chosen;
    }

    public static int AppointmentIdFromUrl(string url) =>
        int.Parse(Regex.Match(url, @"/medical-surveillance/appointments/(\d+)").Groups[1].Value);
}
