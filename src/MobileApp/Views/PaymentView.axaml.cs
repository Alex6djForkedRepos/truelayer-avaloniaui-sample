using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MobileApp.Views;

public partial class PaymentView : UserControl
{
    public PaymentView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Make the BeneficiaryButton a square
        if (BeneficiaryButton?.Bounds is not null)
        {
            BeneficiaryButton.Width = BeneficiaryButton.Bounds.Height;
        }

        // Make the beneficiary dialog width 70% of the screen width
        BeneficiaryDialogGrid.Width = Bounds.Width * 0.7;
    }
}
