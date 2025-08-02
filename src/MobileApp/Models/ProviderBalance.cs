using Avalonia.Media.Imaging;

namespace MobileApp.Models;

public record ProviderBalance(string Id, string Iban, string AvailableAmount, string CurrentAmount, string Overdraft, Bitmap IconSource);
