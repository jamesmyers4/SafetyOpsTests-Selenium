using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>
/// XPath locators for what Playwright calls role, label, and text locators. Names match exactly
/// after whitespace normalization; <see cref="Text"/> matches a substring, like GetByText.
/// </summary>
public static class X
{
    /// <summary>An XPath 1.0 string literal for any text, including text with both quote kinds.</summary>
    public static string Literal(string s)
    {
        if (!s.Contains('\'')) return $"'{s}'";
        if (!s.Contains('"')) return $"\"{s}\"";
        return "concat(" + string.Join(", \"'\", ", s.Split('\'').Select(p => $"'{p}'")) + ")";
    }

    public static By Button(string name) => By.XPath($"//button[normalize-space()={Literal(name)}]");

    public static By Link(string name) => By.XPath($"//a[normalize-space()={Literal(name)}]");

    public static By Heading(string name) =>
        By.XPath($"//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][normalize-space()={Literal(name)}]");

    /// <summary>A form control by its aria-label or its &lt;label for&gt;.</summary>
    public static By Label(string label) =>
        By.XPath($"//*[@aria-label={Literal(label)}] | //*[@id=//label[normalize-space()={Literal(label)}]/@for]");

    public static By Role(string role) => By.XPath($"//*[@role={Literal(role)}]");

    public static readonly By Alert = Role("alert");

    /// <summary>A role="status" region containing <paramref name="text"/>.</summary>
    public static By Status(string text) => By.XPath($"//*[@role='status'][contains(normalize-space(), {Literal(text)})]");

    /// <summary>The innermost elements whose text contains <paramref name="text"/>.</summary>
    public static By Text(string text) =>
        By.XPath($"//body//*[contains(normalize-space(), {Literal(text)})][not(*[contains(normalize-space(), {Literal(text)})])]");

    /// <summary>Rows of the page's table body.</summary>
    public static readonly By TableRows = By.CssSelector("table tbody tr");
}
