using System.Text.RegularExpressions;
using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>Incident Reports: the filterable list, the report form, and the detail page.</summary>
public static class IncidentsPage
{
    public static readonly By Rows = X.TableRows;
    public static readonly By PagerText = By.XPath("//span[starts-with(normalize-space(), 'Page ')]");
    public static readonly By Alert = X.Alert;
    public static readonly By AlertLines = By.XPath("//*[@role='alert']/div");

    public static readonly By OccurredAt = By.Id("incident-occurred-at");
    public static readonly By OrgUnit = By.Id("incident-org-unit");
    public static readonly By Location = By.Id("incident-location");
    public static readonly By Category = By.Id("incident-category");
    public static readonly By Severity = By.Id("incident-severity");
    public static readonly By ReportedBy = By.Id("incident-reported-by");
    public static readonly By Status = By.Id("incident-status");
    public static readonly By Description = By.Id("incident-description");

    public static void Open(IWebDriver driver)
    {
        Navigation.OpenModule(driver, Navigation.IncidentReports);
        driver.ExpectVisible(X.Heading("Incident Reports"));
    }

    public static void OpenCreate(IWebDriver driver)
    {
        Open(driver);
        driver.Click(X.Link("Create Incident"));
        driver.WaitForUrl("/incidents/new$");
        driver.ExpectVisible(X.Heading("Report an Incident"));
    }

    public static void Search(IWebDriver driver, string term)
    {
        driver.Fill(X.Label("Search incidents"), term);
        driver.Click(X.Button("Search"));
    }

    public static void FilterByStatus(IWebDriver driver, string label) => driver.SelectByText(X.Label("Filter by status"), label);

    public static void FilterByCategory(IWebDriver driver, string label) => driver.SelectByText(X.Label("Filter by category"), label);

    /// <summary>A detail-page field such as "Status: Open".</summary>
    public static By Detail(string label, string value) => By.XPath($"//p[normalize-space()={X.Literal($"{label}: {value}")}]");

    public static int IncidentIdFromUrl(string url) => int.Parse(Regex.Match(url, @"/incidents/(\d+)").Groups[1].Value);
}
