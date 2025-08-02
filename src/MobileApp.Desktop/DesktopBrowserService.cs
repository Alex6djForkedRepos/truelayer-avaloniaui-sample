namespace MobileApp.Desktop;

public class DesktopBrowserService : IBrowserService
{
    public void OpenUrl(string url)
    {
        // Open the URL in the default browser
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
