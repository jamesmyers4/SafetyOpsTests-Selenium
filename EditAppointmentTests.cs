using OpenQA.Selenium;
using SafetyOpsTestsSelenium.Helpers;
using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

/// <summary>
/// Each test edits its own appointment: a person and an appointment (30 days ago, one stressor)
/// are created through the API with a unique token in the person's name.
/// Tests find the record by that token instead of relying on seed data.
/// </summary>
[TestFixture]
public class EditAppointmentTests : AppTest
{
    private static readonly By Frame = MedicalSurveillancePage.EditFrame;

    private Appointment _appointment = null!;

    private string Token => Data.Token;

    private static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    [SetUp]
    public void CreateAppointmentToEdit()
    {
        var person = Data.Person("Medical", $"Patient {Token}");
        _appointment = Data.Appointment(person.Id,
            DateOnly.FromDateTime(DateTime.Today).AddDays(-30), new AppointmentStressor("STR-001", "", "Initial"));
    }

    /// <summary>Leaves the driver in the edit frame.</summary>
    private void OpenAppointmentForEditing(string? search = null)
    {
        MedicalSurveillancePage.OpenSearch(Driver);
        MedicalSurveillancePage.Search(Driver, search ?? Token);
        MedicalSurveillancePage.OpenOnlyResult(Driver);
    }

    /// <summary>From the edit frame. Leaves the driver in the frame.</summary>
    private void UpdateAndExpectSaved()
    {
        Driver.Click(MedicalSurveillancePage.Button("Update"));
        // The frame confirms, and posts appointmentUpdated so the page shows the same message.
        Driver.ExpectVisible(X.Status(MedicalSurveillancePage.UpdatedMessage));
        Driver.ExpectCount(MedicalSurveillancePage.FrameAlert, 0);
        Driver.SwitchTo().DefaultContent();
        Driver.ExpectVisible(X.Status(MedicalSurveillancePage.UpdatedMessage));
        Popups.EnterFrame(Driver, Frame);
    }

    private Appointment Saved() => Api.GetAppointment(_appointment.Id);

    private static Dictionary<string, string> ExamTypes(Appointment a) => a.Stressors.ToDictionary(s => s.StressorId, s => s.ExamType);

    [Test]
    public void MedicalSurveillanceEditModuleLoadsAndSearchLinkIsVisible()
    {
        MedicalSurveillancePage.Open(Driver);
        Driver.ExpectVisible(X.Link("Edit / Search"));
        MedicalSurveillancePage.OpenSearch(Driver);
    }

    [Test]
    public void HappyPath_SearchAndEditAppointmentDate()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        UpdateAndExpectSaved();
        Assert.That(Saved().Date, Is.EqualTo(Today));
    }

    [Test]
    public void HappyPath_SearchAndEditPersonEvaluated()
    {
        OpenAppointmentForEditing();
        var person = MedicalSurveillancePage.PickRandomPersonEvaluated(Driver, Frame, except: _appointment.PersonName);
        UpdateAndExpectSaved();
        Assert.That(Saved().PersonName, Is.EqualTo(person));
    }

    [Test]
    public void HappyPath_SearchAndEditExamTypes()
    {
        OpenAppointmentForEditing();
        var examTypes = MedicalSurveillancePage.SelectExamTypesForAllStressors(Driver);
        UpdateAndExpectSaved();
        Assert.That(ExamTypes(Saved()), Is.EquivalentTo(examTypes));
    }

    [Test]
    public void Validation_SearchWithNoResultsReturnsMessage()
    {
        MedicalSurveillancePage.OpenSearch(Driver);
        MedicalSurveillancePage.Search(Driver, "NONEXISTENT_RECORD_XYZ_12345");
        Driver.ExpectVisible(X.Text("No results found. No appointments match your search."));
        Driver.ExpectCount(MedicalSurveillancePage.SearchResultRows, 0);
    }

    [Test]
    public void Validation_SearchWithPartialTermReturnsMultipleResults()
    {
        // "Smith" is part of every seeded appointment's person (John, Jane, and Robert Smith).
        MedicalSurveillancePage.OpenSearch(Driver);
        MedicalSurveillancePage.Search(Driver, "Smith");
        Driver.Expect(() => Driver.FindElements(MedicalSurveillancePage.SearchResultRows).Count, n => n > 1, "more than one result row");
        foreach (var person in Driver.Texts(By.CssSelector("table tbody tr td:nth-child(2)")))
            Assert.That(person, Does.Contain("Smith"));
    }

    [Test]
    public void SearchForTheRemovedAppointmentKeywordReturnsNothing()
    {
        // The old app returned every record for "appointment(s)"; the rebuilt search doesn't.
        MedicalSurveillancePage.OpenSearch(Driver);
        MedicalSurveillancePage.Search(Driver, "appointments");
        Driver.ExpectVisible(X.Text("No results found. No appointments match your search."));
    }

    [Test]
    public void EdgeCase_UpdateOnlyDateField()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        UpdateAndExpectSaved();
        Driver.ExpectValue(MedicalSurveillancePage.PersonEvaluated, _appointment.PersonName);
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(Today));
            Assert.That(saved.PersonId, Is.EqualTo(_appointment.PersonId));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(ExamTypes(_appointment)));
        });
    }

    [Test]
    public void EdgeCase_UpdateOnlyPersonEvaluatedField()
    {
        OpenAppointmentForEditing();
        var person = MedicalSurveillancePage.PickRandomPersonEvaluated(Driver, Frame, except: _appointment.PersonName);
        UpdateAndExpectSaved();
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(saved.Date, Is.EqualTo(_appointment.Date));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(ExamTypes(_appointment)));
        });
    }

    [Test]
    public void EdgeCase_UpdateOnlyExamTypesField()
    {
        OpenAppointmentForEditing();
        var examTypes = MedicalSurveillancePage.SelectExamTypesForAllStressors(Driver);
        UpdateAndExpectSaved();
        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(ExamTypes(saved), Is.EquivalentTo(examTypes));
            Assert.That(saved.Date, Is.EqualTo(_appointment.Date));
            Assert.That(saved.PersonId, Is.EqualTo(_appointment.PersonId));
        });
    }

    [Test]
    public void EdgeCase_AddWorkTaskDuringEdit()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.AddRandomWorkTask(Driver, Frame);
        var shown = Driver.Texts(By.XPath("//table[.//th[normalize-space()='Stressor ID']]/tbody/tr/td[1]"));
        UpdateAndExpectSaved();

        var saved = Saved();
        Assert.That(saved.Stressors.Select(s => s.StressorId), Is.EquivalentTo(shown));
        Assert.That(saved.Stressors.Single(s => s.StressorId == "STR-001").ExamType, Is.EqualTo("Initial"));
    }

    [Test]
    public void EdgeCase_DoubleClickUpdateDoesNotCreateDuplicateSaves()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        Driver.DoubleClick(MedicalSurveillancePage.Button("Update"));
        Driver.SwitchTo().DefaultContent();
        Driver.ExpectVisible(X.Status(MedicalSurveillancePage.UpdatedMessage));
        Popups.EnterFrame(Driver, Frame);
        Driver.ExpectCount(MedicalSurveillancePage.FrameAlert, 0);

        var matches = Api.SearchAppointments(Token);
        Assert.That(matches.Select(a => a.Id), Is.EqualTo(new[] { _appointment.Id }));
        Assert.That(matches[0].Date, Is.EqualTo(Today));
    }

    [Test]
    public void CancelEdit_ChangesAreDiscarded()
    {
        OpenAppointmentForEditing();
        var originalDate = Driver.Value(MedicalSurveillancePage.AppointmentDate);
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        Driver.Click(MedicalSurveillancePage.Button("Cancel"));
        // Cancel posts appointmentEditCancelled and the page closes the frame.
        Driver.SwitchTo().DefaultContent();
        Driver.ExpectCount(Frame, 0);

        MedicalSurveillancePage.Search(Driver, Token);
        MedicalSurveillancePage.OpenOnlyResult(Driver);
        Driver.ExpectValue(MedicalSurveillancePage.AppointmentDate, originalDate);
        Assert.That(Saved(), Is.EqualTo(_appointment).Using<Appointment>(SameRecord));
    }

    [Test]
    public void MultipleFieldUpdate_DatePersonAndExamTypes()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        var person = MedicalSurveillancePage.PickRandomPersonEvaluated(Driver, Frame, except: _appointment.PersonName);
        var examTypes = MedicalSurveillancePage.SelectExamTypesForAllStressors(Driver);
        UpdateAndExpectSaved();

        var saved = Saved();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(Today));
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(ExamTypes(saved), Is.EquivalentTo(examTypes));
        });
    }

    [Test]
    public void VerifyRecordReflectsChangesAfterEdit()
    {
        OpenAppointmentForEditing();
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        UpdateAndExpectSaved();

        // Search by the record's id (an exact match) and reopen it from scratch.
        MedicalSurveillancePage.OpenSearch(Driver);
        MedicalSurveillancePage.Search(Driver, _appointment.Id.ToString());
        Driver.ExpectText(By.CssSelector("table tbody tr td"), _appointment.Id.ToString());
        MedicalSurveillancePage.OpenOnlyResult(Driver);
        Driver.ExpectValue(MedicalSurveillancePage.AppointmentDate, DateTime.Today.ToString("MM/dd/yyyy"));
    }

    /// <summary>Records compare by value, but their stressor lists are reference types.</summary>
    private static bool SameRecord(Appointment a, Appointment b) =>
        a with { Stressors = null! } == b with { Stressors = null! } && a.Stressors.SequenceEqual(b.Stressors);
}
