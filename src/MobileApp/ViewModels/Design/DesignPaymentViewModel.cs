using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using MobileApp.Fakes;
using MobileApp.Models;

namespace MobileApp.ViewModels.Design;

public class DesignPaymentViewModel : PaymentViewModel
{
    public DesignPaymentViewModel() : base(
        new FakeAuthTokenStorage(),
        new FakePaymentService(new FakeTrueLayerClient(), new FakeRedirectManager()),
        WeakReferenceMessenger.Default,
        NullLogger<PaymentViewModel>.Instance)
    {
        RequestedPayments.Add(new PaymentModel
        {
            Id = Guid.NewGuid().ToString(),
            Status = "Authorization Required Very Long Status",
            ProviderId = "provider-1",
            BeneficiaryName = "John Doe",
            BeneficiaryIban = "GB29NWBK60161331926819",
            Reference = "Payment for services",
            Scheme = "SEPA",
            Amount = 10000, // 100.00 in minor currency units
            Currency = "EUR",
            ResourceToken = "resource-token-12345"
        });

        RequestedPayments.Add(new PaymentModel
        {
            Id = Guid.NewGuid().ToString(),
            Status = "Completed",
            ProviderId = "provider-2",
            BeneficiaryName = "Jane Smith",
            BeneficiaryIban = "GB29NWBK60161331926820",
            Reference = "Invoice payment",
            Scheme = "BACS",
            Amount = 5000, // 50.00 in minor currency units
            Currency = "GBP",
            ResourceToken = "resource-token-67890"
        });

        Beneficiaries.Add(new BeneficiaryModel("Alice Johnson", "GB29NWBK60161331926821", "Alice's Account"));
        Beneficiaries.Add(new BeneficiaryModel("Bob Brown", "GB29NWBK60161331926822", "Bob's Account"));
    }
}
