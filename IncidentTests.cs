using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Incident Reports: create, search/filter, edit, delete. Every incident a test creates has the
/// test's unique token in its location, so the base fixture's cleanup finds and deletes it.
/// </summary>
[TestFixture]
public class IncidentTests : AppTest
{
    private string Token => Data.Token;

    private Incident Arrange(string status = "Open", string category = "NearMiss") =>
        Data.Incident($"Loading Dock {Token}-{status}-{category}", status, category);

    [Test]
    public void ListShowsSeededIncidentsAndPager()
    {
        IncidentsPage.Open(Driver);
        Driver.ExpectVisible(X.Link("Create Incident"));
        Driver.ExpectCount(PersonnelPage.RowWith("Building 300 Paint Shop"), 1);
        Driver.ExpectVisible(IncidentsPage.PagerText);
    }

    [Test]
    public void CreateIncidentWithAllFields()
    {
        IncidentsPage.OpenCreate(Driver);
        var occurredAt = DateTime.Today.AddDays(-2).AddHours(14).AddMinutes(15);
        var location = $"Building 500 Warehouse Aisle 3 {Token}";

        Driver.SetValue(IncidentsPage.OccurredAt, occurredAt.ToString("yyyy-MM-ddTHH:mm"));
        AccessLevelsPage.SelectOrgUnit(Driver, IncidentsPage.OrgUnit, OrgUnits.EastWarehouse);
        Driver.Fill(IncidentsPage.Location, location);
        Driver.SelectByText(IncidentsPage.Category, "Property Damage");
        Driver.SelectByText(IncidentsPage.Severity, "High");
        Driver.SelectByText(IncidentsPage.ReportedBy, "John Johnson");
        Driver.Fill(IncidentsPage.Description, "Pallet jack struck a racking upright; rack inspected and tagged out.");
        Driver.ExpectCount(IncidentsPage.Status, 0); // status is only editable later
        Driver.Click(X.Button("Submit Report"));

        Driver.WaitForUrl(@"/incidents/\d+$");
        var id = IncidentsPage.IncidentIdFromUrl(Driver.Url);
        Driver.ExpectVisible(X.Heading($"Incident Report #{id}"));
        Driver.ExpectVisible(IncidentsPage.Detail("Status", "Open"));
        Driver.ExpectVisible(IncidentsPage.Detail("Occurred", occurredAt.ToString("MM/dd/yyyy HH:mm")));
        Driver.ExpectVisible(IncidentsPage.Detail("Category", "Property Damage"));
        Driver.ExpectVisible(IncidentsPage.Detail("Org Unit", OrgUnits.EastWarehouse));

        var saved = Api.GetIncident(id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Location, Is.EqualTo(location));
            Assert.That(saved.OccurredAt, Does.StartWith(occurredAt.ToString("yyyy-MM-ddTHH:mm")));
            Assert.That(saved.Category, Is.EqualTo("PropertyDamage"));
            Assert.That(saved.Severity, Is.EqualTo("High"));
            Assert.That(saved.ReportedByName, Is.EqualTo("John Johnson"));
            Assert.That(saved.Status, Is.EqualTo("Open"));
            Assert.That(saved.OrgUnitName, Is.EqualTo(OrgUnits.EastWarehouse));
        });
    }

    [Test]
    public void CreateRequiresLocationDescriptionAndReporter()
    {
        IncidentsPage.OpenCreate(Driver);
        Driver.Click(X.Button("Submit Report"));
        Driver.ExpectTexts(IncidentsPage.AlertLines, "Location is required.", "Description is required.", "Reported by is required.");
        Driver.WaitForUrl("/incidents/new$");
    }

    [Test]
    public void FutureOccurredAtIsRejectedByTheServer()
    {
        IncidentsPage.OpenCreate(Driver);
        Driver.SetValue(IncidentsPage.OccurredAt, DateTime.Today.AddDays(2).AddHours(9).ToString("yyyy-MM-ddTHH:mm"));
        Driver.Fill(IncidentsPage.Location, $"Future {Token}");
        Driver.SelectByText(IncidentsPage.ReportedBy, "John Smith");
        Driver.Fill(IncidentsPage.Description, "Should not save.");
        Driver.Click(X.Button("Submit Report"));

        Driver.ExpectText(IncidentsPage.Alert, "The incident can't be in the future.");
        Assert.That(Api.SearchIncidents(Token), Is.Empty);
    }

    [Test]
    public void SearchFindsIncidentsByLocation()
    {
        Arrange("Open");
        Arrange("Closed");
        IncidentsPage.Open(Driver);
        IncidentsPage.Search(Driver, Token);
        Driver.ExpectCount(IncidentsPage.Rows, 2);
        Driver.ExpectText(IncidentsPage.PagerText, "Page 1 of 1 (2 incidents)");
    }

    [Test]
    public void SearchWithNoMatchesSaysSo()
    {
        IncidentsPage.Open(Driver);
        IncidentsPage.Search(Driver, $"NONEXISTENT {Token}");
        Driver.ExpectVisible(X.Text("No incidents match."));
        Driver.ExpectCount(IncidentsPage.Rows, 0);
    }

    [Test]
    public void FilterByStatus()
    {
        Arrange("Open");
        var closed = Arrange("Closed");
        IncidentsPage.Open(Driver);
        IncidentsPage.Search(Driver, Token);
        Driver.ExpectCount(IncidentsPage.Rows, 2);

        IncidentsPage.FilterByStatus(Driver, "Closed");
        Driver.ExpectCount(IncidentsPage.Rows, 1);
        Driver.ExpectContainsText(IncidentsPage.Rows, closed.Location);
        Driver.ExpectText(IncidentsPage.PagerText, "Page 1 of 1 (1 incident)");

        IncidentsPage.FilterByStatus(Driver, "All statuses");
        Driver.ExpectCount(IncidentsPage.Rows, 2);
    }

    [Test]
    public void FilterByCategoryCombinesWithStatus()
    {
        var fire = Arrange("Open", "Fire");
        Arrange("Open", "Injury");
        Arrange("Closed", "Fire");
        IncidentsPage.Open(Driver);
        IncidentsPage.Search(Driver, Token);
        Driver.ExpectCount(IncidentsPage.Rows, 3);

        IncidentsPage.FilterByCategory(Driver, "Fire");
        Driver.ExpectCount(IncidentsPage.Rows, 2);
        IncidentsPage.FilterByStatus(Driver, "Open");
        Driver.ExpectCount(IncidentsPage.Rows, 1);
        Driver.ExpectContainsText(IncidentsPage.Rows, fire.Location);
    }

    [Test]
    public void EditIncidentStatusAndSeverity()
    {
        var incident = Arrange();
        Driver.Open($"/incidents/{incident.Id}");
        Driver.Click(X.Button("Edit"));
        Driver.ExpectValue(IncidentsPage.Location, incident.Location);
        Driver.SelectByText(IncidentsPage.Status, "Under Review");
        Driver.SelectByText(IncidentsPage.Severity, "Critical");
        Driver.Click(X.Button("Save Changes"));

        Driver.ExpectVisible(X.Status("Incident updated successfully"));
        Driver.ExpectVisible(IncidentsPage.Detail("Status", "Under Review"));
        Driver.ExpectVisible(IncidentsPage.Detail("Severity", "Critical"));
        Assert.That(Api.GetIncident(incident.Id), Is.EqualTo(incident with { Status = "UnderReview", Severity = "Critical" }));
    }

    [Test]
    public void EditRejectsClearedRequiredFields()
    {
        var incident = Arrange();
        Driver.Open($"/incidents/{incident.Id}");
        Driver.Click(X.Button("Edit"));
        Driver.Fill(IncidentsPage.Location, "");
        Driver.Fill(IncidentsPage.Description, "");
        Driver.Click(X.Button("Save Changes"));

        Driver.ExpectTexts(IncidentsPage.AlertLines, "Location is required.", "Description is required.");
        Assert.That(Api.GetIncident(incident.Id), Is.EqualTo(incident));
    }

    [Test]
    public void CancelEditKeepsTheIncident()
    {
        var incident = Arrange();
        Driver.Open($"/incidents/{incident.Id}");
        Driver.Click(X.Button("Edit"));
        Driver.Fill(IncidentsPage.Location, "TEMPORARY CHANGE");
        Driver.Click(X.Button("Cancel"));

        Driver.ExpectVisible(IncidentsPage.Detail("Location", incident.Location));
        Assert.That(Api.GetIncident(incident.Id), Is.EqualTo(incident));
    }

    [Test]
    public void DeleteIncidentAfterConfirmation()
    {
        var incident = Arrange();
        Driver.Open($"/incidents/{incident.Id}");
        Driver.Click(X.Button("Delete"));
        Driver.ExpectVisible(X.Text("Delete this incident?"));
        Driver.Click(X.Button("Confirm"));

        Driver.WaitForUrl("/incidents$");
        Assert.That(Api.Status(HttpMethod.Get, $"/api/incidents/{incident.Id}"), Is.EqualTo(System.Net.HttpStatusCode.NotFound));
    }

    [Test]
    public void ViewerCanReadButNotEditNorthPlantIncidents()
    {
        var incident = Data.Incident($"North Dock {Token}", orgUnitId: 4);
        var logistics = Data.Incident($"East Dock {Token}", orgUnitId: 6);
        SwitchUser(DemoUsers.Viewer);

        Driver.Open("/incidents");
        IncidentsPage.Search(Driver, Token);
        Driver.ExpectCount(IncidentsPage.Rows, 1);
        Driver.ExpectContainsText(IncidentsPage.Rows, incident.Location);

        Driver.Open($"/incidents/{incident.Id}");
        Driver.ExpectVisible(IncidentsPage.Detail("Location", incident.Location));
        Driver.ExpectCount(X.Button("Edit"), 0);
        Driver.ExpectCount(X.Button("Delete"), 0);

        Assert.That(Browser.Fetch(Driver, "GET", $"/api/incidents/{logistics.Id}"), Is.EqualTo(404));
    }
}
