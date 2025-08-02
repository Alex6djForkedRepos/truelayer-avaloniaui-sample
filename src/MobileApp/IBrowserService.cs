namespace MobileApp;

public interface IBrowserService
{
    /// <summary>
    /// Opens the specified URL in the default browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    void OpenUrl(string url);
}
