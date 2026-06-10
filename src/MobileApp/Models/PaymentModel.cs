using System;

namespace MobileApp.Models;

public record PaymentModel
{
    public required string Id { get; set; }
    public required string Status { get; set; }
    public required string ProviderId { get; set; }
    public required string BeneficiaryName { get; set; }
    public required string BeneficiaryIban { get; set; }
    public required string Reference { get; set; }
    public required string Scheme { get; set; }
    public required long Amount { get; set; }
    public required string Currency { get; set; }
    public string? ResourceToken { get; set; }
    public Uri? HostedPageUri { get; set; }

    // Display amount in decimal format (converting from minor currency units)
    public decimal DisplayAmount => Amount / 100.0m;
}
