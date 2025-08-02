using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MobileApp.Models;

namespace MobileApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAuthTokenStorage _storage;
    private readonly IMessenger _messenger;

    public SettingsViewModel(IAuthTokenStorage storage, IMessenger messenger)
    {
        _storage = storage;
        _messenger = messenger;
        var tokens = storage.LoadTokens();
        if (tokens is null) return;
        Accounts.AddRange(tokens);
    }

    public ObservableCollection<OAuthToken> Accounts { get; } = [];

    [RelayCommand]
    private async Task RemoveAccount(string providerId)
    {
        var account = Accounts.FirstOrDefault(a => a.ProviderId == providerId);
        if (account is null) return;
        Accounts.Remove(account);
        await _storage.StoreTokens(Accounts.ToArray());
        _messenger.Send(new AccountRemovedMessage(providerId));
    }

    [RelayCommand]
    private void RefreshAccounts()
    {
        var tokens = _storage.LoadTokens();
        if (tokens is null) return;
        Accounts.Clear();
        Accounts.AddRange(tokens);
    }
}
