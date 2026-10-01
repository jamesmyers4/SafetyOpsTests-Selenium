namespace SafetyOpsTestsSelenium.Helpers;

/// <summary>
/// One test's data. Creates records through the API and remembers them, and gives the test a
/// unique <see cref="Token"/> to put in anything it creates through the UI. <see cref="CleanUp"/>
/// deletes both, dependents first, so every test leaves the database as it found it.
/// </summary>
public sealed class TestDataScope(AppApi api)
{
    private readonly List<Action> _deletes = [];

    /// <summary>Unique per test; put it in a searchable field (name, location) of UI-created records.</summary>
    public string Token { get; } = TestData.UniqueToken();

    public Person Person(string firstName, string lastName, int? orgUnitId = null)
    {
        var person = api.CreatePerson(firstName, lastName, orgUnitId);
        _deletes.Add(() => api.DeletePerson(person.Id));
        return person;
    }

    public TrainingClass Class(string location, DateOnly? classDate = null)
    {
        var cls = api.CreateClass(location, classDate);
        _deletes.Add(() => api.DeleteClass(cls.Id));
        return cls;
    }

    public Appointment Appointment(int personId, DateOnly date, params AppointmentStressor[] stressors)
    {
        var appointment = api.CreateAppointment(personId, date, stressors);
        TrackAppointment(appointment.Id);
        return appointment;
    }

    public Incident Incident(string location, string status = "Open", string category = "NearMiss", int? orgUnitId = null)
    {
        var incident = api.CreateIncident(location, status, category, orgUnitId: orgUnitId);
        _deletes.Add(() => api.DeleteIncident(incident.Id));
        return incident;
    }

    /// <summary>For an appointment created through the UI for someone without the token in their name.</summary>
    public void TrackAppointment(int id) => _deletes.Add(() => api.DeleteAppointment(id));

    /// <summary>Registers any other undo step (e.g. revoking a role granted through the UI).</summary>
    public void OnCleanUp(Action<AppApi> undo) => _deletes.Add(() => undo(api));

    /// <summary>
    /// Deletes records carrying <see cref="Token"/> (whoever created them), then tracked records in
    /// reverse creation order. Appointments and incidents go before the people they reference.
    /// Already-deleted records are fine (404s are ignored).
    /// </summary>
    public void CleanUp()
    {
        foreach (var a in api.SearchAppointments(Token)) api.DeleteAppointment(a.Id);
        foreach (var i in api.SearchIncidents(Token)) api.DeleteIncident(i.Id);
        foreach (var c in api.SearchClasses(Token)) api.DeleteClass(c.Id);

        for (var i = _deletes.Count - 1; i >= 0; i--) _deletes[i]();
        _deletes.Clear();

        foreach (var p in api.SearchPeople(Token)) api.DeletePerson(p.Id);
    }
}
