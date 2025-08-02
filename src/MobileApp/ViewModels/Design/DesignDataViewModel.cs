using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using MobileApp.Fakes;

namespace MobileApp.ViewModels.Design;

public class DesignDataViewModel : DataViewModel
{
    public DesignDataViewModel() : base(
        new FakeTrueLayerClient(),
        new FakeOptionsFactory(),
        new FakeAuthTokenStorage(),
        WeakReferenceMessenger.Default,
        new FakeRedirectManager(),
        NullLogger<DataViewModel>.Instance)
    {
        Tokens = new List<OAuthToken>([new OAuthToken("Fake Bank", "fake-token", "Bearer", 1000, "https://fake-bank.com/logo.png")]);
        Errors.Add("This is a design-time error for testing purposes.");
    }
}
