namespace MobileApp.Models;

public record PaymentProvider(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}
