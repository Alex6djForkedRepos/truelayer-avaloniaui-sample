using System;

namespace MobileApp;

public interface IRedirectManager
{
    string RedirectUri { get; }

    void NavigateToRedirectUri(Uri uri);
    void OnRedirectSuccess(object? sender, CallbackReceivedEventArgs args);
}
