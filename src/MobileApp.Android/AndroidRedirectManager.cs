using System;

namespace MobileApp.Android;

public class AndroidRedirectManager(IBrowserService browser) : IRedirectManager
{
    public string RedirectUri => "mysecureapp://oauth2redirect";

    public void NavigateToRedirectUri(Uri uri)
    {
        browser.OpenUrl(uri.AbsoluteUri);
    }

    public void OnRedirectSuccess(object? sender, CallbackReceivedEventArgs? args)
    {
        // directly handled by the intent in MainActivity.cs
        Console.WriteLine("Redirect successful!");
    }
}
