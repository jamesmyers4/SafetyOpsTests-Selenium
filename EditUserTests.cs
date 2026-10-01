using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Each test edits its own person (created through the API by <see cref="AppTest.Data"/>), so tests never
/// change seed data or depend on each other.
/// </summary>
[TestFixture]
public class EditUserTests : AppTest
{
    private const string UpdatedMessage = "User updated successfully";

    private Person _person = null!;

    private string Token => Data.Token;

    [SetUp]
    public void CreatePersonToEdit() => _person = Data.Person($"Edit {Token}", $"Smith {Token}");

    private void OpenPersonForEditing()
    {
        PersonnelPage.OpenEditSearch(Driver);
        PersonnelPage.SearchUsers(Driver, Token);
        PersonnelPage.OpenOnlyResult(Driver);
    }

    private void ExpectUpdated()
    {
        Driver.ExpectVisible(X.Status(UpdatedMessage));
        Driver.ExpectCount(X.Alert, 0);
    }

    private Person Saved() => Api.GetPerson(_person.Id);

    [Test]
    public void PersonnelModuleLoadsAndEditSearchUserLinkIsVisible()
    {
        PersonnelPage.Open(Driver);
        Driver.ExpectVisible(X.Link("Edit/Search User"));
        PersonnelPage.OpenEditSearch(Driver);
    }

    [Test]
    public void HappyPath_SearchAndEditUserDepartment()
    {
        OpenPersonForEditing();
        var department = PersonnelPage.PickRandomFromSelectList(Driver, "Department");
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        Assert.That(Saved().Department, Is.EqualTo(department));
    }

    [Test]
    public void HappyPath_SearchAndEditUserNameFields()
    {
        OpenPersonForEditing();
        var first = RandomName.WeightedRandomFirstName();
        var middle = RandomName.WeightedRandomMiddleName();
        var last = RandomName.WeightedRandomLastName();
        var reasons = new[] { first.GetReason(), middle.GetReason(), last.GetReason() }.Where(r => r != null).ToList();
        if (reasons.Count > 0)
            TestContext.Out.WriteLine($"Adversarial: {string.Join(" | ", reasons)}");

        PersonnelPage.PickRandomFromSelectList(Driver, "Department");
        PersonnelPage.PickRandomFromSelectList(Driver, "Subscriptions");
        PersonnelPage.PickRandomGender(Driver);
        Driver.Fill(PersonnelPage.FirstName, first.Resolve());
        Driver.Fill(PersonnelPage.LastName, last.Resolve());
        Driver.Fill(PersonnelPage.MiddleName, middle.Resolve());
        Driver.Click(PersonnelPage.UpdateButton);

        // The expected outcome follows from the input: the form requires first and last name,
        // and the server caps names at 100 characters.
        if (string.IsNullOrWhiteSpace(first.Resolve()) || string.IsNullOrWhiteSpace(last.Resolve()))
        {
            Driver.ExpectContainsText(X.Alert, "is required");
            Assert.That(Saved().FirstName, Is.EqualTo(_person.FirstName));
        }
        else if (new[] { first, middle, last }.Any(n => n.Resolve().Length > TestData.MaxNameLength))
        {
            Driver.ExpectContainsText(X.Alert, "maximum length of 100");
            Assert.That(Saved().FirstName, Is.EqualTo(_person.FirstName));
        }
        else
        {
            ExpectUpdated();
            var saved = Saved();
            Assert.Multiple(() =>
            {
                Assert.That(saved.FirstName, Is.EqualTo(first.Resolve()));
                Assert.That(saved.LastName, Is.EqualTo(last.Resolve()));
                Assert.That(saved.MiddleName, Is.EqualTo(middle.Resolve()));
            });
        }
    }

    [Test]
    public void Validation_SearchWithNoResultsReturnsMessage()
    {
        PersonnelPage.OpenEditSearch(Driver);
        PersonnelPage.SearchUsers(Driver, "NONEXISTENT_USER_XYZ_12345");
        Driver.ExpectVisible(X.Text("No results found for your search."));
        Driver.ExpectCount(PersonnelPage.SearchResultRows, 0);
    }

    [Test]
    public void Validation_SearchWithPartialNameReturnsMultipleResults()
    {
        PersonnelPage.OpenEditSearch(Driver);
        PersonnelPage.SearchUsers(Driver, "a");
        Driver.Expect(() => Driver.FindElements(PersonnelPage.SearchResultRows).Count, n => n > 1, "more than one result row");
    }

    [Test]
    public void Validation_CannotClearRequiredFirstName()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.FirstName, "");
        Driver.Click(PersonnelPage.UpdateButton);
        Driver.ExpectText(X.Alert, "First Name is required");
        Assert.That(Saved().FirstName, Is.EqualTo(_person.FirstName));
    }

    [Test]
    public void Validation_CannotClearRequiredLastName()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.LastName, "");
        Driver.Click(PersonnelPage.UpdateButton);
        Driver.ExpectText(X.Alert, "Last Name is required");
        Assert.That(Saved().LastName, Is.EqualTo(_person.LastName));
    }

    [Test]
    public void EdgeCase_SpecialCharactersInMiddleName()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.MiddleName, "O'Brien-Garcia Jr.");
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        Assert.That(Saved().MiddleName, Is.EqualTo("O'Brien-Garcia Jr."));
    }

    [Test]
    public void EdgeCase_OversizedNameInputBoundaryCheck()
    {
        OpenPersonForEditing();
        var longName = new string('A', 200);
        Driver.Fill(PersonnelPage.FirstName, longName);
        // The input has no maxlength, so the server is the only guard.
        Driver.ExpectValue(PersonnelPage.FirstName, longName);
        Driver.Click(PersonnelPage.UpdateButton);
        Driver.ExpectContainsText(X.Alert, "maximum length of 100");
        Assert.That(Saved().FirstName, Is.EqualTo(_person.FirstName));
    }

    [Test]
    public void EdgeCase_OnlyEditingOneFieldAndSaving()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.MiddleName, "NewMiddle123");
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.MiddleName, Is.EqualTo("NewMiddle123"));
            Assert.That(saved with { MiddleName = _person.MiddleName }, Is.EqualTo(_person));
        });
    }

    [Test]
    public void EdgeCase_ChangingDepartmentOnly()
    {
        OpenPersonForEditing();
        var department = PersonnelPage.PickRandomFromSelectList(Driver, "Department");
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Department, Is.EqualTo(department));
            Assert.That(saved with { Department = _person.Department }, Is.EqualTo(_person));
        });
    }

    [Test]
    public void CancelEdit_ChangesAreDiscarded()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.FirstName, "TEMPORARY_CHANGE_XYZ");
        Driver.Click(X.Button("Cancel"));
        Driver.WaitForUrl("/personnel/edit$");

        PersonnelPage.SearchUsers(Driver, Token);
        PersonnelPage.OpenOnlyResult(Driver);
        Driver.ExpectValue(PersonnelPage.FirstName, _person.FirstName);
    }

    [Test]
    public void DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        OpenPersonForEditing();
        Driver.Fill(PersonnelPage.MiddleName, "DoubleClickTest");
        Driver.DoubleClick(PersonnelPage.UpdateButton);
        ExpectUpdated();
        var matches = Api.SearchPeople(Token);
        Assert.That(matches.Select(p => p.Id), Is.EqualTo(new[] { _person.Id }));
        Assert.That(matches[0].MiddleName, Is.EqualTo("DoubleClickTest"));
    }

    [Test]
    public void EditGenderSelection()
    {
        OpenPersonForEditing();
        var gender = PersonnelPage.PickRandomGender(Driver);
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        Assert.That(Saved().Gender, Is.EqualTo(gender));

        Driver.Navigate().Refresh();
        Driver.ExpectText(PersonnelPage.GenderCombobox, gender);
    }

    [Test]
    public void EditSubscriptions()
    {
        OpenPersonForEditing();
        var subscription = PersonnelPage.PickRandomFromSelectList(Driver, "Subscriptions");
        Driver.Click(PersonnelPage.UpdateButton);
        ExpectUpdated();
        Assert.That(Saved().Subscription, Is.EqualTo(subscription));
    }
}
