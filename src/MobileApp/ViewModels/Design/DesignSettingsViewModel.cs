using CommunityToolkit.Mvvm.Messaging;
using MobileApp.Fakes;

namespace MobileApp.ViewModels.Design;

public class DesignSettingsViewModel() : SettingsViewModel(new FakeAuthTokenStorage(), WeakReferenceMessenger.Default);
