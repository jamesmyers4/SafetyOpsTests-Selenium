using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

[TestFixture]
public class AddUserTests : AppTest
{
    [Test]
    public void FillsAndSubmitsAddNewUserForm()
    {
        PersonnelPage.OpenAddUser(Driver);

        var token = Data.Token;
        var first = RandomName.WeightedRandomFirstName();
        var middle = RandomName.WeightedRandomMiddleName();
        var last = RandomName.WeightedRandomLastName();
        var firstName = $"{first.Resolve()} {token}";
        var middleName = $"{middle.Resolve()} {token}";
        var lastName = $"{last.Resolve()} {token}";

        var reasons = new[] { first.GetReason(), middle.GetReason(), last.GetReason() }.Where(r => r != null).ToList();
        if (reasons.Count > 0)
            TestContext.Out.WriteLine($"Adversarial: {string.Join(" | ", reasons)}");

        var orgUnit = PersonnelPage.SelectRandomOrgUnit(Driver);
        var department = PersonnelPage.PickRandomFromSelectList(Driver, "Department");
        PersonnelPage.PickRandomFromSelectList(Driver, "Subscriptions");
        PersonnelPage.SelectRandomGender(Driver);
        PersonnelPage.PickRandomFromSelectList(Driver, "Employee Category");
        Driver.Fill(PersonnelPage.FirstName, firstName);
        Driver.Fill(PersonnelPage.LastName, lastName);
        Driver.Fill(PersonnelPage.MiddleName, middleName);
        Driver.Click(X.Button("Generate Random Number"));
        Driver.Click(PersonnelPage.AddUserButton);

        if (new[] { firstName, middleName, lastName }.Any(n => n.Length > TestData.MaxNameLength))
        {
            // The server caps names at 100 characters; the form shows its validation message.
            Driver.ExpectContainsText(X.Alert, "maximum length of 100");
            Driver.WaitForUrl("/personnel/create$");
            return;
        }

        Driver.ExpectVisible(X.Heading("User Successfully Added"));
        Driver.ExpectVisible(X.Button("Add Another User"));
        Driver.ExpectVisible(X.Button("Return to Personnel"));

        var saved = Api.SearchPeople(token);
        Assert.That(saved, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(saved[0].FirstName, Is.EqualTo(firstName));
            Assert.That(saved[0].LastName, Is.EqualTo(lastName));
            Assert.That(saved[0].Department, Is.EqualTo(department));
            Assert.That(saved[0].OrgUnitName, Is.EqualTo(orgUnit));
            Assert.That(saved[0].EmployeeNumber, Does.Match("^[0-9]{7}$"));
        });
    }

    [Test]
    public void AddUserRequiresFirstAndLastName()
    {
        PersonnelPage.OpenAddUser(Driver);
        Driver.Click(PersonnelPage.AddUserButton);
        Driver.ExpectContainsText(X.Alert, "The FirstName field is required.");
        Driver.ExpectContainsText(X.Alert, "The LastName field is required.");
        Driver.WaitForUrl("/personnel/create$");
    }

    [Test]
    public void DeleteFromPersonnelListAsksForConfirmation()
    {
        var token = Data.Token;
        Data.Person("Delete", $"Me {token}");

        PersonnelPage.Open(Driver);
        Driver.Fill(PersonnelPage.FilterUsers, token);
        Driver.Press(PersonnelPage.FilterUsers, Keys.Enter);
        var row = PersonnelPage.RowWith(token);
        Driver.ExpectCount(row, 1);

        var message = Popups.AnswerConfirm(Driver, PersonnelPage.DeleteButtonIn(token), accept: true);

        Driver.ExpectCount(row, 0);
        Assert.That(message, Is.EqualTo($"Delete Delete Me {token}?"));
        Assert.That(Api.SearchPeople(token), Is.Empty);
    }

    [Test]
    public void DismissingDeleteConfirmationKeepsThePerson()
    {
        var token = Data.Token;
        Data.Person("Keep", $"Me {token}");

        PersonnelPage.Open(Driver);
        Driver.Fill(PersonnelPage.FilterUsers, token);
        Driver.Click(X.Button("Search"));
        var row = PersonnelPage.RowWith(token);
        Driver.ExpectCount(row, 1);

        Popups.AnswerConfirm(Driver, PersonnelPage.DeleteButtonIn(token), accept: false);

        Driver.ExpectCount(row, 1);
        Assert.That(Api.SearchPeople(token), Has.Count.EqualTo(1));
    }
}
