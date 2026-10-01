using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Each test edits its own class (created through the API with a unique location and an old
/// date that no other class shares), so edits never touch seed data.
/// </summary>
[TestFixture]
public class EditClassTests : AppTest
{
    private const string SavedMessage = "Class saved successfully";

    private TrainingClass _class = null!;

    private string Token => Data.Token;

    [SetUp]
    public void CreateClassToEdit()
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(40, 3000));
        _class = Data.Class($"Building 110 Room 300 - {Token}", date);
    }

    /// <summary>Leaves the driver in the frame, on the class's edit form.</summary>
    private void OpenClassForEditing()
    {
        TrainingPage.OpenSearch(Driver);
        TrainingPage.SearchClasses(Driver, Token);
        TrainingPage.OpenOnlyResult(Driver);
        Driver.ExpectValue(TrainingPage.Location, _class.Location);
    }

    /// <summary>Update in the frame, continue past a duplicate warning if any, then Save in the shell.</summary>
    private void UpdateAndSave()
    {
        Driver.Click(TrainingPage.Button("Update"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with update");
        Driver.Click(TrainingPage.ShellSaveButton);
    }

    /// <summary>On the shell.</summary>
    private void ExpectSaved()
    {
        Driver.ExpectVisible(TrainingPage.ShellStatus(SavedMessage));
        Driver.ExpectCount(TrainingPage.ShellAlert, 0);
    }

    /// <summary>In the frame: Update is refused with <paramref name="message"/> and nothing is saved.</summary>
    private void ExpectFrameRejects(string message)
    {
        Driver.Click(TrainingPage.Button("Update"));
        Driver.ExpectText(TrainingPage.FrameAlert, message);
        TrainingPage.Shell(Driver);
        Driver.ExpectHidden(TrainingPage.ShellSaveButton);
        Assert.That(Saved(), Is.EqualTo(_class));
    }

    private TrainingClass Saved() => Api.GetClass(_class.Id);

    [Test]
    public void TrainingModuleLoadsAndSearchLinkIsVisible()
    {
        TrainingPage.OpenSearch(Driver);
        Driver.ExpectVisible(By.Id("class-search"));
        TrainingPage.Shell(Driver);
        Driver.ExpectVisible(X.Link("Search / Edit Classes"));
    }

    [Test]
    public void HappyPath_SearchAndEditClassLocation()
    {
        OpenClassForEditing();
        var newLocation = $"Building 110 Room 300 - Updated {Token}";
        Driver.Fill(TrainingPage.Location, newLocation);
        UpdateAndSave();
        ExpectSaved();
        Assert.That(Saved(), Is.EqualTo(_class with { Location = newLocation }));
    }

    [Test]
    public void Validation_CannotClearRequiredCourseField()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.CourseTitle, "");
        UpdateAndSave();
        Driver.ExpectVisible(X.Role("status"));

        // Clearing the title box doesn't clear the course: the class keeps its course.
        // (No "Course ID is required." message is shown — SafetyOpsApp issue #8.)
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.CourseId, Is.EqualTo(_class.CourseId));
            Assert.That(saved.CourseTitle, Is.EqualTo(_class.CourseTitle));
        });
    }

    [Test]
    public void Validation_CannotClearRequiredDateField()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.ClassDate, "");
        ExpectFrameRejects("Class Date is required.");
    }

    [Test]
    public void Validation_CannotClearRequiredLocationField()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.Location, "");
        ExpectFrameRejects("Specific location is required");
    }

    [Test]
    public void EdgeCase_SpecialCharactersInUpdatedLocationField()
    {
        OpenClassForEditing();
        var location = $"Bldg 110 / Room 300 & Annex <B> \"North\" {Token}";
        Driver.Fill(TrainingPage.Location, location);
        UpdateAndSave();
        ExpectSaved();
        Assert.That(Saved().Location, Is.EqualTo(location));
    }

    [Test]
    public void EdgeCase_OversizedLocationInputOnEdit()
    {
        OpenClassForEditing();
        var longLocation = Token + new string('A', 500 - Token.Length);
        Driver.Fill(TrainingPage.Location, longLocation);
        // The field has no maxlength; the API is the guard (200 characters).
        Driver.ExpectValue(TrainingPage.Location, longLocation);
        UpdateAndSave();

        Driver.ExpectText(TrainingPage.ShellAlert, "The field Location must be a string with a maximum length of 200.");
        Assert.That(Saved(), Is.EqualTo(_class));
    }

    [Test]
    public void EdgeCase_FutureDateIsRejectedOrFlaggedOnEdit()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.ClassDate, "12/31/2099");
        ExpectFrameRejects("Future dates are not allowed.");
    }

    [Test]
    public void EdgeCase_NonsenseDateStringIsRejectedOnEdit()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.ClassDate, "99/99/9999");
        ExpectFrameRejects("Invalid date. Please enter a valid date.");
    }

    [Test]
    public void EdgeCase_DoubleClickUpdateDoesNotProduceDuplicateUpdate()
    {
        OpenClassForEditing();
        var newLocation = $"Building 110 Room 400 - Double Click Test {Token}";
        Driver.Fill(TrainingPage.Location, newLocation);
        Driver.DoubleClick(TrainingPage.Button("Update"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with update");
        Driver.Click(TrainingPage.ShellSaveButton);
        ExpectSaved();

        var matches = Api.SearchClasses(Token);
        Assert.That(matches, Is.EqualTo(new[] { _class with { Location = newLocation } }));
    }

    [Test]
    public void Search_NoResultsReturnsAppropriateMessage()
    {
        TrainingPage.OpenSearch(Driver);
        TrainingPage.SearchClasses(Driver, "NONEXISTENT_CLASS_XYZ_12345");
        Driver.ExpectVisible(X.Text("No results found."));
        Driver.ExpectCount(TrainingPage.SearchResultRows, 0);
    }

    [Test]
    public void Search_WildcardSearchReturnsMultipleResults()
    {
        TrainingPage.OpenSearch(Driver);
        TrainingPage.SearchClasses(Driver, "Electrical");
        Driver.Expect(() => Driver.FindElements(TrainingPage.SearchResultRows).Count, n => n > 1, "more than one result row");
        foreach (var course in Driver.Texts(By.CssSelector("table tbody tr td:first-child")))
            Assert.That(course, Does.Contain("Electrical"));
    }

    [Test]
    public void CancelEdit_ChangesAreDiscarded()
    {
        OpenClassForEditing();
        Driver.Fill(TrainingPage.Location, "TEMPORARY CHANGE - SHOULD NOT SAVE");
        Driver.Click(TrainingPage.Button("Cancel"));
        TrainingPage.Shell(Driver);
        Driver.ExpectHidden(TrainingPage.ShellSaveButton);

        TrainingPage.EnterFrame(Driver);
        TrainingPage.SearchClasses(Driver, Token);
        TrainingPage.OpenOnlyResult(Driver);
        Driver.ExpectValue(TrainingPage.Location, _class.Location);
        Assert.That(Saved(), Is.EqualTo(_class));
    }
}
