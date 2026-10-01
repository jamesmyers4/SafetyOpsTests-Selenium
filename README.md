# SafetyOpsTests-Selenium

[![E2E](https://github.com/jamesmyers4/SafetyOpsTests-Selenium/actions/workflows/e2e.yml/badge.svg)](https://github.com/jamesmyers4/SafetyOpsTests-Selenium/actions/workflows/e2e.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

An end-to-end test suite in C# (NUnit + Selenium WebDriver) for [SafetyOpsApp](https://github.com/jamesmyers4/SafetyOpsApp), a workplace-safety records system with role-based access control across an org hierarchy. It is the Selenium twin of [SafetyOpsTests-Playwright](https://github.com/jamesmyers4/SafetyOpsTests-Playwright): the same 108 tests, the same assertions, written with Selenium's stateful driver instead of Playwright's locators.

The app's UI is built to be hard to automate: forms live in iframe shells whose **Save** button sits on the parent page, pickers open in **popup windows**, one picker is an **iframe nested inside an iframe**, and frames talk to their parents through origin-checked **`postMessage`**. The suite runs headless in CI against the app started from a pinned commit in Docker, and every test creates and deletes its own data.

## What this demonstrates

- **Frames, windows, and dialogs with a stateful driver.** Selenium is "in" one window and one frame at a time, so every page-object method says where it leaves the driver (in the form frame or on the top-level page). Picker popups post their choice back and close themselves mid-click; `Popups.ClickAndExpectClose` treats that as the expected outcome and switches back to the opener. `window.confirm` dialogs are accepted or dismissed explicitly, with their message asserted.
- **Waiting done right.** No implicit waits, and tests never sleep. `Pages/Ui.cs` provides waiting actions (click retries while an element is re-rendered or covered) and retrying `Expect*` assertions that fail with what they last saw, e.g. `Expected //*[@role='alert'] to have text "…", but got "…"`.
- **Multi-user RBAC tests.** Viewer, Manager, and Admin sessions side by side. An Admin grants a role in one Chrome, and the grantee's already-open second Chrome picks it up on refresh. Also covered: hidden UI actions per role, the API returning 403/404, and scoped org-unit pickers.
- **Isolated, self-cleaning test data.** Tests sign in by calling the login API from inside the browser, so Chrome stores the HttpOnly cookie for pages, frames, and popups. Each test also gets its own Admin API session to arrange and verify data, and a per-test `TestDataScope` deletes everything it created, dependents first, pass or fail. An assembly-level check compares record counts before and after the run and fails the run if anything leaked.
- **Assertions with an oracle.** Randomized and adversarial names (SQL/XSS probes, diacritics, RTL, 255-character strings) are still used. The expected outcome is derived from the input (required field, 100-character limit, or success), then checked in the UI and against what the API actually saved.
- **Debuggable failures.** Failed tests keep a screenshot and the page source, and CI uploads them with the TRX and the app's container logs.

## Run it in 60 seconds

Prerequisites: .NET 10 SDK, Docker, Google Chrome. Selenium Manager downloads a matching ChromeDriver on first run.

```bash
# 1. Start the app with fresh demo data on http://localhost:8080
git clone https://github.com/jamesmyers4/SafetyOpsApp
cd SafetyOpsApp && docker compose up -d --build && cd ..

# 2. Run the suite
git clone https://github.com/jamesmyers4/SafetyOpsTests-Selenium
cd SafetyOpsTests-Selenium
dotnet test
```

`docker compose down -v` (in the app folder) resets the app to fresh demo data. The suite also passes repeatedly against the same database.

With PowerShell 7 and the app cloned next to this repo, `pwsh scripts/e2e.ps1` does all of it: resets and starts the app, waits for `/healthz`, builds, runs the tests, and stops the app (`-Filter`, `-KeepApp`, `-AppPath` are optional).

To watch it run, set `TestSettings__Headless=false`.

## Coverage

| Fixture | Area | Tests |
| ------- | ---- | ----: |
| `AuthTests` | Sign-in guard and `returnUrl` (pages and iframe pages), bad credentials, open-redirect protection, sign-out, API 401s | 14 |
| `NavigationTests` | UI sign-in, Modules menu | 6 |
| `AddUserTests` | Add User form (randomized/adversarial names), server validation, delete with `window.confirm` | 4 |
| `EditUserTests` | Search, edit every field, validation, cancel, double-submit | 15 |
| `AccessLevelsTests` | Org tree per role, read-only Viewer, Manager's scoped pickers, Admin grant → grantee → revoke | 10 |
| `CreateClassTests` | Create-class frame, course popup, duplicate dialog (continue / go to existing / start over), date rules, shell Save | 15 |
| `EditClassTests` | Search/edit frame, validation, 200-char limit surfaced by the shell, cancel, double-submit | 13 |
| `CreateAppointmentTests` | Create frame: calendar, person popup, nested work-task frame, exam types; required fields; delete | 3 |
| `EditAppointmentTests` | Search, edit date/person/exam types/work tasks, `postMessage` confirm and cancel, search by id | 15 |
| `IncidentTests` | Create, validation, future-time rejection, search, status/category filters, edit, delete, Viewer scope | 13 |
| **Total** | | **108** |

## Project layout

```
AppTest.cs            Base fixture: Chrome per test, API sign-in, per-test Admin API session, test data, failure artifacts
AssemblySetup.cs      Record-count leak check around the whole run
Config/               TestSettings (BaseUrl, Username, Password, Headless) from appsettings.json + env vars
Helpers/
  Browser.cs          Starts Chrome; signs in/out and calls the API from inside the browser
  AppApi.cs           Typed wrapper over the app's REST API (arrange, verify, clean up)
  TestDataScope.cs    Per-test token, tracked creates, dependency-ordered cleanup
  DemoUsers.cs        Seeded admin / manager / viewer accounts
  OrgUnits.cs         The app's fixed org tree
  RandomName.cs       Plain and adversarial name generator
Pages/
  X.cs                Role, label, and text locators as XPath
  Ui.cs               Waiting actions and retrying Expect* assertions
  Popups.cs           Frames, picker windows, window.confirm
  LoginPage, Navigation, PersonnelPage, AccessLevelsPage, TrainingPage, MedicalSurveillancePage, IncidentsPage
*Tests.cs             Test fixtures (see Coverage)
scripts/e2e.ps1       Local gate: fresh app in Docker → build → test → stop
.github/workflows/    E2E workflow: SafetyOpsApp (pinned) in Docker → dotnet test → artifacts
```

## Configuration

`appsettings.json` points at the Docker setup and the seeded demo Admin (public demo credentials, not secrets):

```json
{
  "TestSettings": {
    "BaseUrl": "http://localhost:8080",
    "Username": "admin",
    "Password": "admin",
    "Headless": true
  }
}
```

Environment variables override it: `TestSettings__BaseUrl` (e.g. `https://localhost:14418` for the app's dev profile; certificate errors are ignored), `TestSettings__Username`, `TestSettings__Password`, `TestSettings__Headless`.

## Selenium vs. Playwright: same tests, different tools

| Need | Playwright twin | This suite |
| ---- | --------------- | ---------- |
| Waiting | Auto-waiting locators and `Expect` | `Ui` waiting actions and retrying `Expect*` built on `WebDriverWait` |
| Role/label/text locators | `GetByRole`, `GetByLabel`, `GetByText` | XPath builders in `Pages/X.cs` (`X.Button`, `X.Label`, `X.Text`, …) |
| Frames | Stateless `FrameLocator`, nestable | `SwitchTo().Frame()`; `Popups.EnterFrame` re-enters from the top for nested frames |
| Popups | `RunAndWaitForPopupAsync` | Diff `WindowHandles`, switch, then switch back when the popup closes itself |
| Dialogs | `Page.Dialog` event | `UnhandledPromptBehavior.Ignore` + `SwitchTo().Alert()` |
| Sign-in cookie | `APIRequestContext` shares the context's cookie jar | `fetch('/api/auth/login')` run inside Chrome, so the browser stores the HttpOnly cookie |
| React-controlled inputs | `FillAsync` | Select-all + type; a native value setter for `datetime-local` |
| Failure evidence | Trace + screenshot | Screenshot + page source |

## Found while testing

The suite pins down current behavior. Where that behavior looks wrong, the test says so in a comment, and the issue is reported to the app rather than hidden:

- Edit Class: clearing the Course Title box doesn't clear the course. The save reports success and keeps the old course, with no "Course ID is required." message.
- Add User shows raw server messages for missing names; Edit User validates on the client.
- Person names aren't trimmed on save.

## Tech stack

C# / .NET 10 · NUnit 5 · Selenium WebDriver 4.49 (Chrome, Selenium Manager) · Microsoft.Extensions.Configuration · GitHub Actions + Docker Compose

## Related

- [SafetyOpsApp](https://github.com/jamesmyers4/SafetyOpsApp): the system under test (ASP.NET Core 10 API + React).
- [SafetyOpsTests-Playwright](https://github.com/jamesmyers4/SafetyOpsTests-Playwright): the same suite in Playwright for .NET.

## License

[MIT](LICENSE) © James Myers
