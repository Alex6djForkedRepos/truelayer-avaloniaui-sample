using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogHostAvalonia;
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class SettingsView : UserControl
{
    private static readonly FilePickerFileType JsonFilter =
        new("JSON backup") { Patterns = ["*.json"] };

    public SettingsView()
    {
        InitializeComponent();
        ExportButton.Click += ExportButton_Click;
        ImportButton.Click += ImportButton_Click;
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext!;

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Settings",
            SuggestedFileName = "truelayer-backup.json",
            FileTypeChoices = [JsonFilter]
        });
        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        await ViewModel.ExportSettingsAsync(stream);
    }

    private async void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogSession? session = null;
        var result = await DialogHost.Show(
            BuildConfirmPanel(
                onCancel: () => session?.Close(null),
                onConfirm: () => session?.Close("confirmed")),
            "SettingsDialogHost",
            (DialogOpenedEventHandler)((_, args) => session = args.Session));

        if (result?.ToString() != "confirmed") return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Settings",
            AllowMultiple = false,
            FileTypeFilter = [JsonFilter]
        });
        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        await ViewModel.ImportSettingsAsync(stream);
    }

    private static StackPanel BuildConfirmPanel(
        System.Action onCancel, System.Action onConfirm)
    {
        var lavender = new DynamicResourceExtension("Lavender");
        var charcoal = new DynamicResourceExtension("Charcoal");

        var cancelButton = new Button { Content = "Cancel" };
        cancelButton.Click += (_, _) => onCancel();
        cancelButton[!BackgroundProperty] = lavender;
        cancelButton[!ForegroundProperty] = charcoal;

        var confirmButton = new Button { Content = "Import" };
        confirmButton.Click += (_, _) => onConfirm();
        confirmButton[!BackgroundProperty] = lavender;
        confirmButton[!ForegroundProperty] = charcoal;

        return new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "This will replace all your accounts and beneficiaries.\nAre you sure?",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, confirmButton }
                }
            }
        };
    }
}
