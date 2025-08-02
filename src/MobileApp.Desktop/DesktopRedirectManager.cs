using System;
using CommunityToolkit.Mvvm.Messaging;
using MobileApp.Models;

namespace MobileApp.Desktop;

public class DesktopRedirectManager(IBrowserService browser, IAuthManager authManager, IMessenger messenger) : IRedirectManager
{
    public string RedirectUri => "http://localhost:3000/callback";

    public void NavigateToRedirectUri(Uri uri)
    {
        authManager.CallbackReceived += OnRedirectSuccess;
        authManager.Start();

        browser.OpenUrl(uri.AbsoluteUri);
    }

    public void OnRedirectSuccess(object? sender, CallbackReceivedEventArgs args)
    {
        Console.WriteLine("Desktop Redirect successful!");
        messenger.Send(new CallbackReceivedMessage(args));
        authManager.Stop();
    }
}
