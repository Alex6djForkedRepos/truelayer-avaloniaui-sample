using System;
using System.Collections.Generic;
using OneOf;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MobileApp.Models;
using TrueLayer;
using TrueLayer.Payments.Model;
using TrueLayer.PaymentsProviders.Model;

namespace MobileApp;

public interface IPaymentService
{
    Task<PaymentModel?> MakePayment(
        string providerId,
        string beneficiaryName,
        string beneficiaryIban,
        string paymentReference,
        string currency,
        decimal amount,
        OneOf<SchemeSelection.InstantOnly, SchemeSelection.InstantPreferred, SchemeSelection.UserSelected>?
            paymentScheme);

    void NavigateToPaymentRedirectUri(PaymentModel payment);

    Task<string?> GetPaymentStatus(string paymentId);
}

public class PaymentService(
    ITrueLayerClient tlClient,
    IRedirectManager redirectManager,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentModel?> MakePayment(
        string providerId,
        string beneficiaryName,
        string beneficiaryIban,
        string paymentReference,
        string currency,
        decimal amount,
        OneOf<SchemeSelection.InstantOnly, SchemeSelection.InstantPreferred, SchemeSelection.UserSelected>? paymentScheme)
    {
        var paymentRequest = new CreatePaymentRequest(
            amountInMinor: amount.ToMinorCurrencyUnit(2),
            currency: currency,
            paymentMethod: new CreatePaymentMethod.BankTransfer(
                new CreateProviderSelection.UserSelected
                {
                    Filter = new ProviderFilter
                    {
                        ProviderIds = providerId == "ALL" ? null : [providerId],
                        ReleaseChannel = "private_beta",
                    },
                    SchemeSelection = paymentScheme,
                },
                new Beneficiary.ExternalAccount(
                    beneficiaryName,
                    paymentReference,
                    new AccountIdentifier.Iban(beneficiaryIban)
                )
            ),
            // TODO: Make email configurable
            user: new PaymentUserRequest(name: beneficiaryIban, email: "user@example.com"),
            hostedPage: new HostedPageRequest(new Uri(redirectManager.RedirectUri))
        );

        var apiResponse = await tlClient.Payments.CreatePayment(
            paymentRequest,
            idempotencyKey: Guid.NewGuid().ToString()
        );

        if (!apiResponse.IsSuccessful)
        {
            logger.LogWarning("Error creating payment: {StatusCode} - {ExtractErrors} - {TraceId}",
                apiResponse.StatusCode,
                Helpers.ExtractErrors(apiResponse.Problem?.Errors),
                apiResponse.TraceId);
            return null;
        }

        if (apiResponse.Data.TryPickT0(out var authorizationRequired, out _))
        {
            logger.LogInformation("Payment requires authorization: {authorizationRequiredId} - {authorizationRequiredStatus}", authorizationRequired.Id, authorizationRequired.Status);

            return new PaymentModel
            {
                Id = authorizationRequired.Id,
                Status = authorizationRequired.Status,
                ProviderId = providerId,
                BeneficiaryName = beneficiaryName,
                BeneficiaryIban = beneficiaryIban,
                Reference = paymentReference,
                Scheme = paymentScheme.ToString() ?? "Unknown",
                Amount = amount.ToMinorCurrencyUnit(2),
                Currency = currency,
                ResourceToken = authorizationRequired.ResourceToken,
                HostedPageUri = authorizationRequired.HostedPage?.Uri,
            };
        }

        if (apiResponse.Data.TryPickT1(out var authorized, out _))
        {
            logger.LogError("Unexpected 'Authorized' payment response: {authorizedId} - {authorizedStatus}", authorized.Id, authorized.Status);
        }
        if (apiResponse.Data.TryPickT2(out var failed, out _))
        {
            logger.LogError("Unexpected 'Failed' payment response: {failedId} - {failedFailureStage} - {failedFailureReason}",
                failed.Id, failed.FailureStage, failed.FailureReason);
        }
        if (apiResponse.Data.TryPickT3(out _, out _))
        {
            logger.LogError("Unexpected 'Authorizing' payment response.");
        }

        return null;
    }

    public void NavigateToPaymentRedirectUri(PaymentModel payment)
    {
        ArgumentException.ThrowIfNullOrEmpty(payment.ResourceToken);

        if (payment.HostedPageUri is null)
        {
            logger.LogError("Hosted payment page URI is null for payment ID: {PaymentId}", payment.Id);
            return;
        }

        logger.LogInformation("Hosted Payment Page URL: {HppUrl}", payment.HostedPageUri.AbsoluteUri);
        redirectManager.NavigateToRedirectUri(payment.HostedPageUri);
    }

    public async Task<string?> GetPaymentStatus(string paymentId)
    {
        var response = await tlClient.Payments.GetPayment(paymentId);
        if (!response.IsSuccessful)
        {
            logger.LogError("Error fetching payment status: {StatusCode} - {Errors} - {TraceId}", response.StatusCode, Helpers.ExtractErrors(response.Problem?.Errors), response.TraceId);
            return null;
        }

        var newStatus = string.Empty;

        if (response.Data.TryPickT0(out var authRequired, out _))
        {
            logger.LogInformation("Payment requires authorization: {Id} - {Status}", authRequired.Id, authRequired.Status);
            newStatus = authRequired.Status;
        }
        else if (response.Data.TryPickT1(out var authorizing, out _))
        {
            logger.LogInformation("Payment is authorizing: {Id} - {Status}", authorizing.Id, authorizing.Status);
            newStatus = authorizing.Status;
        }
        else if (response.Data.TryPickT2(out var authorized, out _))
        {
            logger.LogInformation("Payment is authorized: {Id} - {Status}", authorized.Id, authorized.Status);
            newStatus = authorized.Status;
        }
        else if (response.Data.TryPickT3(out var executed, out _))
        {
            logger.LogInformation("Payment is executed: {Id} - {Status}", executed.Id, executed.Status);
            newStatus = executed.Status;
        }
        else if (response.Data.TryPickT4(out var settled, out _))
        {
            logger.LogInformation("Payment is settled: {Id} - {Status}", settled.Id, settled.Status);
            newStatus = settled.Status;
        }
        else if (response.Data.TryPickT5(out var failed, out _))
        {
            logger.LogError("Payment failed: {Id} - {FailureStage} - {FailureReason}", failed.Id, failed.FailureStage, failed.FailureReason);
            newStatus = "Failed";
        }
        else if (response.Data.TryPickT6(out var attemptFailed, out _))
        {
            logger.LogError("Payment attempt failed: {Id} - {FailureStage} - {FailureReason}", attemptFailed.Id, attemptFailed.FailureStage, attemptFailed.FailureReason);
            newStatus = "Failed";
        }

        return newStatus;
    }

    // ReSharper disable once UnusedMember.Local
    private async Task FetchProviders()
    {
        var config = new AuthorizationFlowConfiguration(Redirect: new Dictionary<string, string>());
        var flow = new AuthorizationFlow(config);
        var request = new SearchPaymentsProvidersRequest(flow, ["IT", "GB"], ["EUR", "GBP"], "private_beta", ["retail"]);
        var response = await tlClient.PaymentsProviders.SearchPaymentsProviders(request);

        // if (response.IsSuccessful)
        // {
        //     await storage.Store("providers.json", response.Data);
        // }
    }
}
