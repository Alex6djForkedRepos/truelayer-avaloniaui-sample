using System;

namespace MobileApp.Fakes;

public class FakeRedirectManager : IRedirectManager
{
    public string RedirectUri => "https://fake-redirect-uri.com/callback";

    public void NavigateToRedirectUri(Uri uri)
    {
        Console.WriteLine("Navigating to redirect URI: " + uri.AbsoluteUri);
    }

    public void OnRedirectSuccess(object? sender, CallbackReceivedEventArgs args)
    {
        Console.WriteLine("Redirect successful");
    }
}
