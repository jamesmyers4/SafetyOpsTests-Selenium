using OpenQA.Selenium;

namespace SafetyOpsTestsSelenium.Pages;

/// <summary>
/// Frames and popup windows. Selenium's context is stateful: the driver is "in" one window and one
/// frame at a time, so these helpers always say where they leave it.
/// </summary>
public static class Popups
{
    /// <summary>Switches to the top-level page, then into each frame of <paramref name="path"/> in turn.</summary>
    public static void EnterFrame(IWebDriver driver, params By[] path)
    {
        driver.SwitchTo().DefaultContent();
        foreach (var frame in path)
            driver.SwitchTo().Frame(driver.Find(frame));
    }

    /// <summary>
    /// Clicks <paramref name="trigger"/> (in the current context) and switches to the window it opens.
    /// Returns the opener's handle for <see cref="ClickAndExpectClose"/> or <see cref="Close"/>.
    /// </summary>
    public static string OpenFrom(IWebDriver driver, By trigger)
    {
        var opener = driver.CurrentWindowHandle;
        var before = driver.WindowHandles.ToHashSet();
        driver.Click(trigger);
        var popup = driver.Until(d => d.WindowHandles.FirstOrDefault(h => !before.Contains(h)), "a popup window to open");
        driver.SwitchTo().Window(popup!);
        return opener;
    }

    /// <summary>
    /// Clicks something that makes the current popup post its choice to the opener and close
    /// itself, then switches back to the opener's top-level page. The popup may close before the
    /// click returns; that is the expected outcome, so the error is only rethrown if it's still open.
    /// </summary>
    public static void ClickAndExpectClose(IWebDriver driver, By target, string opener)
    {
        var popup = driver.CurrentWindowHandle;
        var element = driver.Find(target);
        try
        {
            element.Click();
        }
        catch (WebDriverException) when (!driver.WindowHandles.Contains(popup))
        {
        }
        driver.Until(d => !d.WindowHandles.Contains(popup), "the popup to close itself");
        driver.SwitchTo().Window(opener);
    }

    /// <summary>Closes the current popup the way a user would and switches back to the opener.</summary>
    public static void Close(IWebDriver driver, string opener)
    {
        driver.Close();
        driver.SwitchTo().Window(opener);
    }

    /// <summary>
    /// Accepts or dismisses the window.confirm dialog that <paramref name="trigger"/> opens and returns its message.
    /// </summary>
    public static string AnswerConfirm(IWebDriver driver, By trigger, bool accept)
    {
        driver.Click(trigger);
        var dialog = driver.Until(d => { try { return (IAlert?)d.SwitchTo().Alert(); } catch (NoAlertPresentException) { return null; } }, "a confirm dialog");
        var message = dialog!.Text ?? "";
        if (accept) dialog.Accept(); else dialog.Dismiss();
        return message;
    }
}
