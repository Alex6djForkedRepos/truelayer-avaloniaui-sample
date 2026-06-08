using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    IInsetsManager? InsetsManager { get; set; }
    IInputPane? InputPane { get; set; }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null) return;

        InsetsManager = toplevel.InsetsManager;
        InputPane = toplevel.InputPane;

        if (InsetsManager is not null)
        {
            // InsetsManager.DisplayEdgeToEdgePreference = true;
            // InsetsManager.IsSystemBarVisible = true;
        }

        if (InputPane is not null)
            InputPane.StateChanged += InputPane_StateChanged;

        // Initial input to let the view resize according to the safe area
        InputPane_StateChanged(this, new InputPaneStateEventArgs(InputPaneState.Closed, new Rect(0, 0, 0, 0), new Rect(0, 0, 0, 0)));
    }

    // Script to avoid the keyboard overlapping the content
    // https://github.com/AvaloniaUI/Avalonia/issues/13319#issuecomment-2653355229
    private void InputPane_StateChanged(object? sender, InputPaneStateEventArgs e)
    {
        if (DataContext is not MainViewModel model || InputPane is null || InsetsManager is null) return;

        var safeArea = InsetsManager.SafeAreaPadding;
        var occludedArea = InputPane.OccludedRect;

        // we don't want the bottom bar to be displayed while the keyboard is open.
        // So we remove the bottom bar height from the safe area when the keyboard is open.
        var bottomHeight = BottomBar is { Bounds.Height: > 0 } && e.NewState == InputPaneState.Open
            ? BottomBar.Bounds.Height
            : 0;

        model.SafeArea = new Thickness(safeArea.Left, safeArea.Top, safeArea.Right, occludedArea.Height - bottomHeight);
    }
}
