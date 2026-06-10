using System;
using System.Threading.Tasks;
using MobileApp.Models;
using OneOf;
using TrueLayer.Payments.Model;

namespace MobileApp.Fakes;

public class FakePaymentService(
    FakeTrueLayerClient tlClient,
    FakeRedirectManager redirectManager) : IPaymentService
{
    public Task<PaymentModel?> MakePayment(string providerId, string beneficiaryName, string beneficiaryIban, string paymentReference,
        string currency, decimal amount, OneOf<SchemeSelection.InstantOnly, SchemeSelection.InstantPreferred, SchemeSelection.UserSelected>? paymentScheme)
    {
        var payment = tlClient.Payments.CreatePayment(null!);
        var paymentModel = new PaymentModel
        {
            Id = payment.Id.ToString(),
            Status = "Pending",
            ProviderId = providerId,
            BeneficiaryName = beneficiaryName,
            BeneficiaryIban = beneficiaryIban,
            Reference = paymentReference,
            Scheme = "SEPA",
            Amount = (long)(amount * 100), // Convert to minor currency units
            Currency = currency,
            ResourceToken = "ResourceToken"
        };
        return Task.FromResult(paymentModel)!;
    }

    public void NavigateToPaymentRedirectUri(PaymentModel payment)
    {
        redirectManager.NavigateToRedirectUri(new Uri("https://example.com/redirect?payment_id=" + payment.Id));
    }

    public Task<string?> GetPaymentStatus(string paymentId)
    {
        return Task.FromResult("Completed")!;
    }
}
