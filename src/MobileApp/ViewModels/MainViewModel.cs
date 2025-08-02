using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MobileApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PaymentViewModel _paymentViewModel;
    private readonly DataViewModel _dataViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public MainViewModel(PaymentViewModel paymentViewModel, DataViewModel dataViewModel, SettingsViewModel settingsViewModel)
    {
        _paymentViewModel = paymentViewModel;
        _dataViewModel = dataViewModel;
        _settingsViewModel = settingsViewModel;

        _currentPage = dataViewModel;

        DataButtonFontWeight = SelectedFontWeight;
        PaymentsButtonFontWeight = DefaultFontWeight;
        SettingsButtonFontWeight = DefaultFontWeight;

        DataButtonForeground = _selectedButtonForeground;
        PaymentsButtonForeground = _defaultButtonForeground;
        SettingsButtonForeground = _defaultButtonForeground;
    }

    [ObservableProperty]
    private string? _selectedListItem;
    partial void OnSelectedListItemChanged(string? value)
    {
        if (value is null) return;

        ObservableObject? vm = value switch
        {
            "Payments" => _paymentViewModel,
            "Data" => _dataViewModel,
            "Settings" => _settingsViewModel,
            _ => null,
        };

        if (vm is null) return;

        CurrentPage = vm;
        IsPaneOpen = false;
    }

    [ObservableProperty]
    private bool _isPaneOpen;

    [ObservableProperty]
    private ObservableObject _currentPage;

    private Thickness _safeArea = new(0);
    public Thickness SafeArea
    {
        get => _safeArea;
        set
        {
            _safeArea = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private const FontWeight SelectedFontWeight = FontWeight.Bold;
    private const FontWeight DefaultFontWeight = FontWeight.SemiBold;

    [ObservableProperty] private FontWeight _dataButtonFontWeight = DefaultFontWeight;
    [ObservableProperty] private FontWeight _paymentsButtonFontWeight = DefaultFontWeight;
    [ObservableProperty] private FontWeight _settingsButtonFontWeight = DefaultFontWeight;

    private readonly IBrush _selectedButtonForeground = Brushes.White;
    private readonly IBrush _defaultButtonForeground = Brushes.Black;

    [ObservableProperty] private IBrush _dataButtonForeground;
    [ObservableProperty] private IBrush _paymentsButtonForeground;
    [ObservableProperty] private IBrush _settingsButtonForeground;

    [RelayCommand]
    private void OpenDataPage()
    {
        SelectedListItem = "Data";

        DataButtonFontWeight = SelectedFontWeight;
        PaymentsButtonFontWeight = DefaultFontWeight;
        SettingsButtonFontWeight = DefaultFontWeight;

        DataButtonForeground = _selectedButtonForeground;
        PaymentsButtonForeground = _defaultButtonForeground;
        SettingsButtonForeground = _defaultButtonForeground;
    }

    [RelayCommand]
    private void OpenPaymentsPage()
    {
        SelectedListItem = "Payments";

        PaymentsButtonFontWeight = SelectedFontWeight;
        DataButtonFontWeight = DefaultFontWeight;
        SettingsButtonFontWeight = DefaultFontWeight;

        PaymentsButtonForeground = _selectedButtonForeground;
        DataButtonForeground = _defaultButtonForeground;
        SettingsButtonForeground = _defaultButtonForeground;
    }

    [RelayCommand]
    private void OpenSettingsPage()
    {
        SelectedListItem = "Settings";

        SettingsButtonFontWeight = SelectedFontWeight;
        DataButtonFontWeight = DefaultFontWeight;
        PaymentsButtonFontWeight = DefaultFontWeight;

        SettingsButtonForeground = _selectedButtonForeground;
        DataButtonForeground = _defaultButtonForeground;
        PaymentsButtonForeground = _defaultButtonForeground;
    }
}
