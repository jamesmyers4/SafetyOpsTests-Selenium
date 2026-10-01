using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Every class a test creates has the test's unique token in its location, so the base fixture's
/// cleanup finds and deletes it; no test depends on another's leftovers.
/// </summary>
[TestFixture]
public class CreateClassTests : AppTest
{
    private string Token => Data.Token;

    private string UniqueLocation => $"Building 110 Room 200 - {Token}";

    /// <summary>An existing Electrical - Low Voltage class dated today, so the form's duplicate check fires.</summary>
    private TrainingClass ArrangeExistingClassToday() => Data.Class($"Existing class - {Token}");

    private List<TrainingClass> SavedClasses() => Api.SearchClasses(Token);

    /// <summary>From the frame: the shell shows no Save button.</summary>
    private void ExpectNoShellSave()
    {
        TrainingPage.Shell(Driver);
        Driver.ExpectHidden(TrainingPage.ShellSaveButton);
    }

    [Test]
    public void TrainingModuleLoadsAndCreateClassLinkIsVisible()
    {
        TrainingPage.Open(Driver);
        Driver.ExpectVisible(X.Link("Create Class"));
    }

    [Test]
    public void HappyPath_CreateClassWithAllFields()
    {
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with create");

        var id = TrainingPage.SaveNewClass(Driver);

        Driver.ExpectVisible(TrainingPage.ShellStatus("Class saved successfully"));
        Driver.ExpectVisible(X.Text($"Location: {UniqueLocation}"));
        var saved = Api.GetClass(id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CourseId, Is.EqualTo("ELV-001"));
            Assert.That(saved.ClassDate, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
            Assert.That(saved.Location, Is.EqualTo(UniqueLocation));
            Assert.That(saved.OrgUnitName, Is.EqualTo("SafetyOps Industries"));
        });
    }

    [Test]
    public void Validation_SubmitCompletelyEmptyForm()
    {
        TrainingPage.OpenCreateClass(Driver);
        Driver.Click(TrainingPage.Button("Create"));
        Driver.ExpectTexts(TrainingPage.FrameAlertLines, "Course ID is required.", "Class Date is required.", "Specific location is required");
        ExpectNoShellSave();
    }

    [Test]
    public void Validation_CourseTitleIsRequired()
    {
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.PickToday(Driver);
        Driver.Fill(TrainingPage.Location, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));
        Driver.ExpectText(TrainingPage.FrameAlert, "Course ID is required.");
        ExpectNoShellSave();
    }

    [Test]
    public void Validation_DateIsRequired()
    {
        TrainingPage.OpenCreateClass(Driver);
        Driver.Fill(TrainingPage.Location, UniqueLocation);
        TrainingPage.SelectCourseViaPopup(Driver, TrainingPage.ElectricalLowVoltage);
        Driver.Click(TrainingPage.Button("Create"));
        Driver.ExpectText(TrainingPage.FrameAlert, "Class Date is required.");
        ExpectNoShellSave();
    }

    [Test]
    public void EdgeCase_SpecialCharactersInLocationField()
    {
        var location = $"Bldg 110 / Room 200 & Annex <B> \"North\" {Token}";
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, location);
        Driver.Click(TrainingPage.Button("Create"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with create");
        TrainingPage.EnterFrame(Driver);
        Driver.ExpectCount(TrainingPage.FrameAlert, 0);

        TrainingPage.Shell(Driver);
        var id = TrainingPage.SaveNewClass(Driver);
        Driver.ExpectVisible(X.Text($"Location: {location}"));
        Assert.That(Api.GetClass(id).Location, Is.EqualTo(location));
    }

    [Test]
    public void EdgeCase_OversizedLocationInputBoundaryCheck()
    {
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        var longLocation = Token + new string('A', 500 - Token.Length);
        Driver.Fill(TrainingPage.Location, longLocation);
        // The field has no maxlength; the API is the guard (200 characters).
        Driver.ExpectValue(TrainingPage.Location, longLocation);

        Driver.Click(TrainingPage.Button("Create"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with create");
        Driver.Click(TrainingPage.ShellSaveButton);

        Driver.ExpectText(TrainingPage.ShellAlert, "The field Location must be a string with a maximum length of 200.");
        Driver.WaitForUrl("/training$");
        Assert.That(SavedClasses(), Is.Empty);
    }

    [Test]
    public void EdgeCase_FutureDateIsRejectedOrFlagged()
    {
        TrainingPage.OpenCreateClass(Driver);
        Driver.Fill(TrainingPage.Location, UniqueLocation);
        TrainingPage.SelectCourseViaPopup(Driver, TrainingPage.ElectricalLowVoltage);
        Driver.Fill(TrainingPage.ClassDate, "12/31/2099");
        Driver.Click(TrainingPage.Button("Create"));
        Driver.ExpectText(TrainingPage.FrameAlert, "Future dates are not allowed.");
        ExpectNoShellSave();
    }

    [Test]
    public void EdgeCase_NonsenseDateStringIsRejected()
    {
        TrainingPage.OpenCreateClass(Driver);
        Driver.Fill(TrainingPage.Location, UniqueLocation);
        TrainingPage.SelectCourseViaPopup(Driver, TrainingPage.ElectricalLowVoltage);
        Driver.Fill(TrainingPage.ClassDate, "99/99/9999");
        Driver.Click(TrainingPage.Button("Create"));
        Driver.ExpectText(TrainingPage.FrameAlert, "Invalid date. Please enter a valid date.");
        ExpectNoShellSave();
    }

    [Test]
    public void EdgeCase_ClosingCoursePickerPopupLeavesCourseValueEmpty()
    {
        TrainingPage.OpenCreateClass(Driver);
        var opener = Popups.OpenFrom(Driver, TrainingPage.CoursePickerButton);
        Driver.ExpectVisible(X.Heading("Select Course"));
        Popups.Close(Driver, opener);
        TrainingPage.EnterFrame(Driver);
        Driver.ExpectValue(TrainingPage.CourseTitle, "");
    }

    [Test]
    public void EdgeCase_DoubleClickCreateDoesNotProduceDuplicateClass()
    {
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.DoubleClick(TrainingPage.Button("Create"));
        TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with create");
        TrainingPage.SaveNewClass(Driver);

        Assert.That(SavedClasses(), Has.Count.EqualTo(1));
    }

    [Test]
    public void DuplicateDialog_AllThreeOptionsArePresent()
    {
        ArrangeExistingClassToday();
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));

        Driver.ExpectVisible(X.Text("A class with this course and date already exists. How would you like to proceed?"));
        Driver.ExpectVisible(TrainingPage.Button("Continue with create"));
        Driver.ExpectVisible(TrainingPage.Button("Go to Existing"));
        Driver.ExpectVisible(TrainingPage.Button("Start Over"));
        ExpectNoShellSave();
    }

    [Test]
    public void DuplicateDialog_ContinueWithCreateProceedsToSave()
    {
        ArrangeExistingClassToday();
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));

        Assert.That(TrainingPage.DismissDuplicateWarningIfPresent(Driver, "Continue with create"), Is.True);
        TrainingPage.SaveNewClass(Driver);
        Driver.ExpectVisible(TrainingPage.ShellStatus("Class saved successfully"));
        Assert.That(SavedClasses(), Has.Count.EqualTo(2));
    }

    [Test]
    public void DuplicateDialog_GoToExistingOpensTheExistingClass()
    {
        ArrangeExistingClassToday();
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));
        Driver.Click(TrainingPage.Button("Go to Existing"));

        // The frame posts trainingGoToExisting; the shell navigates to that class.
        TrainingPage.Shell(Driver);
        Driver.WaitForUrl(@"/training/classes/\d+$");
        var existing = Api.GetClass(TrainingPage.ClassIdFromUrl(Driver.Url));
        Assert.Multiple(() =>
        {
            Assert.That(existing.CourseId, Is.EqualTo("ELV-001"));
            Assert.That(existing.ClassDate, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
        });
        Assert.That(SavedClasses(), Has.Count.EqualTo(1), "Go to Existing must not create a class");
    }

    [Test]
    public void DuplicateDialog_StartOverResetsTheForm()
    {
        ArrangeExistingClassToday();
        TrainingPage.OpenCreateClass(Driver);
        TrainingPage.FillValidClass(Driver, UniqueLocation);
        Driver.Click(TrainingPage.Button("Create"));
        Driver.Click(TrainingPage.Button("Start Over"));

        Driver.ExpectValue(TrainingPage.Location, "");
        Driver.ExpectValue(TrainingPage.CourseTitle, "");
        Driver.ExpectValue(TrainingPage.ClassDate, "");
        Driver.ExpectHidden(TrainingPage.Button("Start Over"));
        ExpectNoShellSave();
    }
}
