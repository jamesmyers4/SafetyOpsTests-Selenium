using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Org-scoped roles: what each demo user can see and do, and granting/revoking roles as Admin.
/// Seeded roles: admin = Admin on the whole org, manager = Manager on Manufacturing Division,
/// viewer = Viewer on North Plant.
/// </summary>
[TestFixture]
public class AccessLevelsTests : AppTest
{
    private static readonly By OrgUnitColumn = By.CssSelector("table tbody tr td:nth-child(4)");

    [Test]
    public void AdminSeesTheWholeOrgTreeAndRoleAssignments()
    {
        AccessLevelsPage.Open(Driver);
        AccessLevelsPage.ExpectTreeCodes(Driver, OrgUnits.AllCodes);
        Driver.ExpectVisible(AccessLevelsPage.GrantButton);
        Driver.ExpectCount(AccessLevelsPage.AssignmentRow("manager", OrgUnits.Manufacturing, "Manager"), 1);
        Driver.ExpectCount(AccessLevelsPage.AssignmentRow("viewer", OrgUnits.NorthPlant, "Viewer"), 1);
        // Admins can't revoke their own role.
        var adminRow = AccessLevelsPage.AssignmentRow("admin", OrgUnits.Organization, "Admin");
        Driver.ExpectContainsText(adminRow, "(you)");
        Assert.That(Driver.Find(adminRow).FindElements(By.TagName("button")), Is.Empty);
    }

    [Test]
    public void ViewerSeesOnlyNorthPlantAndNoRoleManagement()
    {
        SwitchUser(DemoUsers.Viewer);
        Driver.Open("/home");
        AccessLevelsPage.Open(Driver);
        AccessLevelsPage.ExpectTreeCodes(Driver, "MFG-N");
        Driver.ExpectVisible(X.Text(AccessLevelsPage.AdminsOnlyMessage));
        Driver.ExpectCount(AccessLevelsPage.GrantButton, 0);
    }

    [Test]
    public void ViewerSeesOnlyNorthPlantPeopleAndNoWriteActions()
    {
        SwitchUser(DemoUsers.Viewer);
        Driver.Open("/home");
        PersonnelPage.Open(Driver);

        Driver.ExpectVisible(OrgUnitColumn);
        Assert.That(Driver.Texts(OrgUnitColumn).Distinct(), Is.EqualTo(new[] { OrgUnits.NorthPlant }));
        Driver.ExpectCount(X.Link("Add New User"), 0);
        Driver.ExpectCount(X.Button("Delete"), 0);
    }

    [Test]
    public void ViewerEditUserPageIsReadOnly()
    {
        SwitchUser(DemoUsers.Viewer);
        Driver.Open("/home");
        PersonnelPage.OpenEditSearch(Driver);
        PersonnelPage.SearchUsers(Driver, "Jane");
        PersonnelPage.OpenOnlyResult(Driver);

        Driver.ExpectVisible(X.Status("You have read-only access to this record."));
        Driver.ExpectEnabled(PersonnelPage.UpdateButton, enabled: false);
        Driver.ExpectEnabled(PersonnelPage.OrgUnit, enabled: false);
    }

    [Test]
    public void ViewerSeesNoCreateActionsInAnyModule()
    {
        SwitchUser(DemoUsers.Viewer);
        Driver.Open("/home");

        TrainingPage.Open(Driver);
        Driver.ExpectVisible(X.Link("Search / Edit Classes"));
        Driver.ExpectCount(X.Link("Create Class"), 0);

        MedicalSurveillancePage.Open(Driver);
        Driver.ExpectVisible(X.Link("Edit / Search"));
        Driver.ExpectCount(X.Link("Create"), 0);

        IncidentsPage.Open(Driver);
        Driver.ExpectCount(X.Link("Create Incident"), 0);
    }

    [Test]
    public void ViewerWritesAreRejectedByTheApi()
    {
        SwitchUser(DemoUsers.Viewer);
        Assert.That(Browser.Fetch(Driver, "POST", "/api/personnel", new { firstName = "Not", lastName = "Allowed" }), Is.EqualTo(403));
    }

    [Test]
    public void ManagerOrgUnitPickerIsTheManufacturingSubtree()
    {
        SwitchUser(DemoUsers.Manager);
        Driver.Open("/home");
        AccessLevelsPage.Open(Driver);
        AccessLevelsPage.ExpectTreeCodes(Driver, "MFG", "MFG-N", "MFG-S");
        Driver.ExpectVisible(X.Text(AccessLevelsPage.AdminsOnlyMessage));

        PersonnelPage.OpenAddUser(Driver);
        Assert.That(AccessLevelsPage.OrgUnitOptions(Driver, PersonnelPage.OrgUnit), Is.EqualTo(OrgUnits.ManufacturingSubtree));

        PersonnelPage.Open(Driver);
        Driver.ExpectVisible(OrgUnitColumn);
        Assert.That(Driver.Texts(OrgUnitColumn).Distinct(), Is.EquivalentTo(new[] { OrgUnits.NorthPlant, OrgUnits.SouthPlant }));
    }

    [Test]
    public void AdminGrantIsVisibleToTheGranteeImmediatelyThenRevoke()
    {
        // Whatever happens below, the viewer must not keep the East Warehouse role.
        Data.OnCleanUp(api =>
        {
            foreach (var a in api.ListRoleAssignments())
                if (a.UserName == "viewer" && a.OrgUnitName == OrgUnits.EastWarehouse)
                    api.RevokeRole(a.Id);
        });

        var viewer = OpenBrowserAs(DemoUsers.Viewer);
        viewer.Open("/personnel/access-levels");
        AccessLevelsPage.ExpectTreeCodes(viewer, "MFG-N");

        AccessLevelsPage.Open(Driver);
        AccessLevelsPage.Grant(Driver, "Demo Viewer (viewer)", OrgUnits.EastWarehouse, "Viewer");
        Driver.ExpectVisible(X.Status("Granted Viewer on East Warehouse to Demo Viewer."));
        var row = AccessLevelsPage.AssignmentRow("viewer", OrgUnits.EastWarehouse, "Viewer");
        Driver.ExpectCount(row, 1);

        // The viewer's existing session picks up the new role on its next page load.
        viewer.Navigate().Refresh();
        AccessLevelsPage.ExpectTreeCodes(viewer, "MFG-N", "LOG-E");
        viewer.Open("/personnel");
        viewer.ExpectCount(PersonnelPage.RowWith("John Johnson"), 1);

        var message = Popups.AnswerConfirm(Driver, AccessLevelsPage.RevokeButton("viewer", OrgUnits.EastWarehouse, "Viewer"), accept: true);
        Driver.ExpectVisible(X.Status("Revoked Viewer on East Warehouse from Demo Viewer."));
        Driver.ExpectCount(row, 0);
        Assert.That(message, Is.EqualTo("Revoke Viewer on East Warehouse from Demo Viewer?"));

        viewer.Open("/personnel/access-levels");
        AccessLevelsPage.ExpectTreeCodes(viewer, "MFG-N");
    }

    [Test]
    public void GrantWithoutAUserShowsAnError()
    {
        AccessLevelsPage.Open(Driver);
        Driver.Click(AccessLevelsPage.GrantButton);
        Driver.ExpectText(X.Alert, "Choose a user and an organization unit.");
    }

    [Test]
    public void ViewerCannotReachRoleAssignmentsThroughTheApi()
    {
        SwitchUser(DemoUsers.Viewer);
        Assert.That(Browser.Fetch(Driver, "GET", "/api/access/assignments"), Is.EqualTo(403));
        Driver.Open("/personnel/access-levels");
        Driver.WaitForUrl("/personnel/access-levels$");
        Driver.ExpectVisible(X.Text(AccessLevelsPage.AdminsOnlyMessage));
    }
}
