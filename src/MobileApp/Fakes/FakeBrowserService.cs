using System;

namespace MobileApp.Fakes;

public class FakeBrowserService : IBrowserService
{
    public void OpenUrl(string url)
    {
        // This is a no-op for design-time purposes
        Console.WriteLine($"Design-time: Would open URL: {url}");
    }
}
