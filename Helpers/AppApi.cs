using System.Net;
using System.Text;
using System.Text.Json;
using SafetyOpsTestsSelenium.Config;

namespace SafetyOpsTestsSelenium.Helpers;

public record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record Person(
    int Id, string FirstName, string LastName, string MiddleName, string Gender, string Department,
    string EmployeeCategory, string Subscription, string EmployeeNumber, int OrgUnitId, string OrgUnitName);

public record PersonOption(int Id, string Name);

public record TrainingClass(int Id, string CourseTitle, string CourseId, string ClassDate, string Location, int OrgUnitId, string OrgUnitName);

public record Appointment(int Id, string Date, int PersonId, string PersonName, List<AppointmentStressor> Stressors, int OrgUnitId, string OrgUnitName);

public record AppointmentStressor(string StressorId, string StressorName, string ExamType);

public record Incident(
    int Id, string OccurredAt, string Location, string Category, string Severity, string Description,
    int ReportedById, string ReportedByName, string Status, int OrgUnitId, string OrgUnitName);

public record RoleAssignment(int Id, int UserId, string UserName, string DisplayName, int OrgUnitId, string OrgUnitName, string Role);

/// <summary>
/// A signed-in session on the app's REST API, used for test setup, verification, and cleanup.
/// It keeps the auth cookie itself: .NET's CookieContainer won't send the app's Secure cookie
/// over http://localhost.
/// </summary>
public sealed class AppApi : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private string? _cookie;

    private AppApi()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator, // dev certificate
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(ConfigLoader.Settings.BaseUrl) };
    }

    public static AppApi SignIn(Credentials user)
    {
        var api = new AppApi();
        using var response = api.Send(HttpMethod.Post, "/api/auth/login", new { username = user.Username, password = user.Password });
        if (!response.IsSuccessStatusCode)
        {
            api.Dispose();
            throw new InvalidOperationException($"API sign-in as '{user.Username}' failed: {(int)response.StatusCode} {Read(response)}");
        }
        api._cookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        return api;
    }

    public void Dispose() => _http.Dispose();

    public HttpStatusCode Status(HttpMethod method, string url, object? body = null)
    {
        using var response = Send(method, url, body);
        return response.StatusCode;
    }

    public T Get<T>(string url) => ReadJson<T>(HttpMethod.Get, url, null);

    public T Post<T>(string url, object body) => ReadJson<T>(HttpMethod.Post, url, body);

    public void Delete(string url)
    {
        using var response = Send(HttpMethod.Delete, url, null);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException($"DELETE {url} failed: {(int)response.StatusCode} {Read(response)}");
    }

    private T ReadJson<T>(HttpMethod method, string url, object? body)
    {
        using var response = Send(method, url, body);
        var text = Read(response);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"{method} {url} failed: {(int)response.StatusCode} {text}");
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    private HttpResponseMessage Send(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (_cookie is not null) request.Headers.Add("Cookie", _cookie);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        return _http.Send(request);
    }

    private static string Read(HttpResponseMessage response)
    {
        using var reader = new StreamReader(response.Content.ReadAsStream());
        return reader.ReadToEnd();
    }

    private static string Search(string search) => Uri.EscapeDataString(search);

    // Personnel

    public Person CreatePerson(string firstName, string lastName, int? orgUnitId = null) =>
        Post<Person>("/api/personnel", new
        {
            firstName, lastName, middleName = "", gender = "", department = "Safety",
            employeeCategory = "Full Time", subscription = "Basic", employeeNumber = "1234567", orgUnitId,
        });

    public List<Person> SearchPeople(string search) => Get<Paged<Person>>($"/api/personnel?pageSize=100&search={Search(search)}").Items;

    public Person GetPerson(int id) => Get<Person>($"/api/personnel/{id}");

    public void DeletePerson(int id) => Delete($"/api/personnel/{id}");

    // Training

    /// <summary>Creates a class; <paramref name="classDate"/> defaults to today.</summary>
    public TrainingClass CreateClass(string location, DateOnly? classDate = null, string courseId = "ELV-001", int? orgUnitId = null) =>
        Post<TrainingClass>("/api/training/classes", new
        {
            courseId, classDate = (classDate ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"), location, orgUnitId,
        });

    public List<TrainingClass> SearchClasses(string search) => Get<Paged<TrainingClass>>($"/api/training/classes?pageSize=100&search={Search(search)}").Items;

    public TrainingClass GetClass(int id) => Get<TrainingClass>($"/api/training/classes/{id}");

    public void DeleteClass(int id) => Delete($"/api/training/classes/{id}");

    // Medical surveillance

    public Appointment CreateAppointment(int personId, DateOnly date, params AppointmentStressor[] stressors) =>
        Post<Appointment>("/api/medical-surveillance/appointments", new
        {
            date = date.ToString("yyyy-MM-dd"), personId,
            stressors = stressors.Select(s => new { stressorId = s.StressorId, examType = s.ExamType }),
        });

    public List<Appointment> SearchAppointments(string search) =>
        Get<Paged<Appointment>>($"/api/medical-surveillance/appointments?pageSize=100&search={Search(search)}").Items;

    public Appointment GetAppointment(int id) => Get<Appointment>($"/api/medical-surveillance/appointments/{id}");

    public void DeleteAppointment(int id) => Delete($"/api/medical-surveillance/appointments/{id}");

    // Incident reports

    /// <summary>Creates an incident reported by the first person matching <paramref name="reporter"/>, yesterday at 09:30.</summary>
    public Incident CreateIncident(string location, string status = "Open", string category = "NearMiss",
        string severity = "Low", string reporter = "John Smith", int? orgUnitId = null)
    {
        var reportedBy = Get<List<PersonOption>>($"/api/personnel/lookup?search={Search(reporter)}").First();
        return Post<Incident>("/api/incidents", new
        {
            occurredAt = DateTime.Today.AddDays(-1).AddHours(9.5).ToString("yyyy-MM-ddTHH:mm"),
            location, category, severity, description = $"Test incident at {location}", reportedById = reportedBy.Id, status, orgUnitId,
        });
    }

    public List<Incident> SearchIncidents(string search) => Get<Paged<Incident>>($"/api/incidents?pageSize=100&search={Search(search)}").Items;

    public Incident GetIncident(int id) => Get<Incident>($"/api/incidents/{id}");

    public void DeleteIncident(int id) => Delete($"/api/incidents/{id}");

    // Access

    public List<RoleAssignment> ListRoleAssignments() => Get<List<RoleAssignment>>("/api/access/assignments");

    public void RevokeRole(int assignmentId) => Delete($"/api/access/assignments/{assignmentId}");
}
