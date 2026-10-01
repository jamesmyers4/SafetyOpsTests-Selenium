using SafetyOpsTestsSelenium.Pages;

namespace SafetyOpsTestsSelenium.Tests;

[TestFixture]
public class CreateAppointmentTests : AppTest
{
    [Test]
    public void FillsAndSubmitsCreateMedicalSurveillanceForm()
    {
        var frame = MedicalSurveillancePage.CreateFrame;
        MedicalSurveillancePage.OpenCreate(Driver);
        MedicalSurveillancePage.PickTodayFromCalendar(Driver);
        var person = MedicalSurveillancePage.PickRandomPersonEvaluated(Driver, frame);
        MedicalSurveillancePage.AddRandomWorkTask(Driver, frame);
        var examTypes = MedicalSurveillancePage.SelectExamTypesForAllStressors(Driver);
        Driver.Click(MedicalSurveillancePage.Button("Create"));

        // The frame sends the whole page to the new appointment.
        Driver.SwitchTo().DefaultContent();
        Driver.WaitForUrl(@"/medical-surveillance/appointments/\d+$");
        var id = MedicalSurveillancePage.AppointmentIdFromUrl(Driver.Url);
        Data.TrackAppointment(id); // the randomly picked person doesn't carry the token
        Driver.ExpectVisible(X.Heading("Appointment Details"));
        Driver.ExpectVisible(X.Text($"Person Evaluated: {person}"));

        var saved = Api.GetAppointment(id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Date, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
            Assert.That(saved.PersonName, Is.EqualTo(person));
            Assert.That(saved.Stressors.ToDictionary(s => s.StressorId, s => s.ExamType), Is.EquivalentTo(examTypes));
        });
    }

    [Test]
    public void CreateRequiresDateAndPerson()
    {
        MedicalSurveillancePage.OpenCreate(Driver);
        Driver.Click(MedicalSurveillancePage.Button("Create"));
        Driver.ExpectTexts(MedicalSurveillancePage.FrameAlertLines, "Appointment Date is required.", "Person Evaluated is required.");
        Driver.SwitchTo().DefaultContent();
        Driver.WaitForUrl("/medical-surveillance/create$");
    }

    [Test]
    public void DeleteAppointmentFromDetailsPageAsksForConfirmation()
    {
        var person = Data.Person("Medical", $"Delete {Data.Token}");
        var appointment = Data.Appointment(person.Id, DateOnly.FromDateTime(DateTime.Today));

        Driver.Open($"/medical-surveillance/appointments/{appointment.Id}");
        Driver.Click(X.Button("Delete Appointment"));
        Driver.ExpectVisible(X.Text("Confirm deletion?"));
        Driver.Click(X.Button("Confirm"));

        Driver.ExpectVisible(X.Status("Appointment deleted successfully."));
        Assert.That(Api.SearchAppointments(Data.Token), Is.Empty);
    }
}
