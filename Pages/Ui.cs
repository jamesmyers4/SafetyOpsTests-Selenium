using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SafetyOpsTestsSelenium.Config;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>
/// Waiting actions and retrying assertions. Selenium doesn't auto-wait, so every interaction here
/// waits for its element first, and every Expect* polls until it holds or fails the test after
/// <see cref="Timeout"/> with what it last saw.
/// </summary>
public static class Ui
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static T Until<T>(this IWebDriver driver, Func<IWebDriver, T> condition, string what, TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(driver, timeout ?? Timeout) { Message = $"Timed out waiting for {what}" };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait.Until(condition);
    }

    /// <summary>The first visible element matching <paramref name="by"/>, once there is one.</summary>
    public static IWebElement Find(this IWebDriver driver, By by) =>
        driver.Until(d => d.FindElements(by).FirstOrDefault(e => e.Displayed), $"{by} to be visible")!;

    /// <summary>Clicks the first visible, enabled match, retrying while it's re-rendered or covered.</summary>
    public static void Click(this IWebDriver driver, By by) =>
        driver.Until(d =>
        {
            var element = d.FindElements(by).FirstOrDefault(e => e.Displayed && e.Enabled);
            if (element is null) return false;
            try { element.Click(); return true; }
            catch (ElementClickInterceptedException) { return false; }
        }, $"{by} to be clickable");

    public static void DoubleClick(this IWebDriver driver, By by) =>
        new Actions(driver).DoubleClick(driver.Find(by)).Perform();

    /// <summary>Replaces the field's text by typing, so React sees real input events (also when clearing).</summary>
    public static void Fill(this IWebDriver driver, By by, string text)
    {
        var element = driver.Find(by);
        element.SendKeys(Keys.Control + "a");
        element.SendKeys(Keys.Delete);
        if (text.Length > 0) element.SendKeys(text);
    }

    /// <summary>
    /// Sets an input's value the way React expects (native setter + input event). For inputs that
    /// can't be typed into reliably, such as datetime-local, whose segments depend on the locale.
    /// </summary>
    public static void SetValue(this IWebDriver driver, By by, string value) =>
        ((IJavaScriptExecutor)driver).ExecuteScript("""
            const [el, value] = arguments;
            Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, value);
            el.dispatchEvent(new Event('input', { bubbles: true }));
            """, driver.Find(by), value);

    public static void Press(this IWebDriver driver, By by, string key) => driver.Find(by).SendKeys(key);

    public static void SelectByText(this IWebDriver driver, By by, string text) => new SelectElement(driver.Find(by)).SelectByText(text);

    public static void SelectByValue(this IWebDriver driver, By by, string value) => new SelectElement(driver.Find(by)).SelectByValue(value);

    public static string Value(this IWebElement element) => element.GetDomProperty("value") ?? "";

    public static string Value(this IWebDriver driver, By by) => driver.Find(by).Value();

    /// <summary>Trimmed visible texts of every match.</summary>
    public static List<string> Texts(this ISearchContext context, By by) => context.FindElements(by).Select(e => e.Text.Trim()).ToList();

    /// <summary>Opens a path of the app under test, e.g. "/home".</summary>
    public static void Open(this IWebDriver driver, string path) => driver.Navigate().GoToUrl(ConfigLoader.Settings.BaseUrl + path);

    public static void WaitForUrl(this IWebDriver driver, string pattern) =>
        driver.Expect(() => driver.Url, url => Regex.IsMatch(url, pattern), $"URL matching /{pattern}/");

    // Retrying assertions

    /// <summary>Polls <paramref name="read"/> until <paramref name="ok"/> holds, or fails the test.</summary>
    public static void Expect<T>(this IWebDriver driver, Func<T> read, Func<T, bool> ok, string expectation, TimeSpan? timeout = null)
    {
        var last = default(T);
        try
        {
            driver.Until(_ => ok(last = read()), expectation, timeout);
        }
        catch (WebDriverTimeoutException)
        {
            Assert.Fail($"Expected {expectation}, but got {Describe(last)}");
        }
    }

    public static void ExpectVisible(this IWebDriver driver, By by) =>
        driver.Expect(() => driver.FindElements(by).Any(e => e.Displayed), v => v, $"{by} to be visible");

    public static void ExpectHidden(this IWebDriver driver, By by) =>
        driver.Expect(() => driver.FindElements(by).Any(e => e.Displayed), v => !v, $"{by} to be hidden");

    public static void ExpectCount(this IWebDriver driver, By by, int count) =>
        driver.Expect(() => driver.FindElements(by).Count, n => n == count, $"{count} × {by}");

    /// <summary>The first match's visible text equals <paramref name="text"/> (whitespace-normalized).</summary>
    public static void ExpectText(this IWebDriver driver, By by, string text) =>
        driver.Expect(() => Normalize(driver.FindElements(by).FirstOrDefault()?.Text), t => t == text, $"{by} to have text \"{text}\"");

    public static void ExpectContainsText(this IWebDriver driver, By by, string text) =>
        driver.Expect(() => Normalize(driver.FindElements(by).FirstOrDefault()?.Text), t => t?.Contains(text) == true, $"{by} to contain \"{text}\"");

    /// <summary>Every match's text, in order, equals <paramref name="texts"/>.</summary>
    public static void ExpectTexts(this IWebDriver driver, By by, params string[] texts) =>
        driver.Expect(() => driver.FindElements(by).Select(e => Normalize(e.Text)).ToList(), t => t.SequenceEqual(texts),
            $"{by} to have texts [{string.Join(", ", texts)}]");

    public static void ExpectValue(this IWebDriver driver, By by, string value) =>
        driver.Expect(() => driver.FindElements(by).FirstOrDefault()?.Value(), v => v == value, $"{by} to have value \"{value}\"");

    public static void ExpectNotValue(this IWebDriver driver, By by, string value) =>
        driver.Expect(() => driver.FindElements(by).FirstOrDefault()?.Value(), v => v is not null && v != value,
            $"{by} to have a value other than \"{value}\"");

    public static void ExpectEnabled(this IWebDriver driver, By by, bool enabled = true) =>
        driver.Expect(() => driver.FindElements(by).FirstOrDefault()?.Enabled, e => e == enabled,
            $"{by} to be {(enabled ? "enabled" : "disabled")}");

    private static string? Normalize(string? text) => text is null ? null : Regex.Replace(text, @"\s+", " ").Trim();

    private static string Describe<T>(T value) => value switch
    {
        null => "nothing",
        string s => $"\"{s}\"",
        System.Collections.IEnumerable e => "[" + string.Join(", ", e.Cast<object?>()) + "]",
        _ => value.ToString() ?? "",
    };
}
