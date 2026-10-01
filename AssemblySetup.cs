using SafetyOpsTestsSelenium.Helpers;

/// <summary>Runs once for the whole assembly (no namespace, so it covers every fixture).</summary>
[SetUpFixture]
public class AssemblySetup
{
    private static readonly string[] CountedLists =
    [
        "/api/personnel", "/api/training/classes", "/api/medical-surveillance/appointments", "/api/incidents",
    ];

    private AppApi? _api;
    private Dictionary<string, int> _before = [];

    [OneTimeSetUp]
    public void StartRun()
    {
        _api = AppApi.SignIn(DemoUsers.Admin);
        _before = CountRecords(_api);
    }

    /// <summary>
    /// Every test cleans up after itself, so the run must leave the same number of records it found.
    /// A difference means some test leaked data (or deleted data it didn't create).
    /// </summary>
    [OneTimeTearDown]
    public void CheckNothingLeaked()
    {
        try
        {
            var after = CountRecords(_api!);
            var changed = _before.Where(kv => after[kv.Key] != kv.Value).Select(kv => $"{kv.Key}: {kv.Value} → {after[kv.Key]}").ToList();
            Assert.That(changed, Is.Empty, "The test run changed the number of records: " + string.Join("; ", changed));
        }
        finally
        {
            _api?.Dispose();
        }
    }

    private static Dictionary<string, int> CountRecords(AppApi api)
    {
        var counts = new Dictionary<string, int>();
        foreach (var url in CountedLists)
            counts[url] = api.Get<Paged<object>>($"{url}?pageSize=1").TotalCount;
        counts["/api/access/assignments"] = api.ListRoleAssignments().Count;
        return counts;
    }
}
