using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MobileApp.Models;
using TrueLayer.Payments.Model;

namespace MobileApp.ViewModels;

public partial class PaymentViewModel : ObservableValidator
{
    private readonly IAuthTokenStorage _storage;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentViewModel> _logger;

    [SuppressMessage("ReSharper", "MemberCanBeProtected.Global", Justification = "Used by DI")]
    public PaymentViewModel(IAuthTokenStorage storage, IPaymentService paymentService, IMessenger messenger, ILogger<PaymentViewModel> logger)
    {
        _storage = storage;
        _paymentService = paymentService;
        _logger = logger;

        _ = LoadBeneficiaries();

        Errors.CollectionChanged += (_, _) =>
        {
            HasErrorsInner = Errors.Count > 0;
        };

        ErrorsChanged += (_, _) =>
        {
            Errors.Clear();
            var errors = GetErrors().ToList();
            if (errors.Count == 0) return;
            foreach (var error in errors)
            {
                if (string.IsNullOrWhiteSpace(error.ErrorMessage)) continue;
                Errors.Add(error.ErrorMessage);
            }
        };

        RequestedPayments.CollectionChanged += OnRequestedPaymentsCollectionChanged;

        messenger.Register<PaymentViewModel, CallbackReceivedMessage>(this, (__, message) =>
        {
            if (!message.Args.QueryParams.TryGetValue("payment_id", out var paymentId))
            {
                _logger.LogWarning("No payment_id found in the redirect query parameters.");
                Errors.Add("No payment_id found in the redirect query parameters.");
                return;
            }

            _ = GetPaymentStatus(paymentId);
        });

        messenger.Register<PaymentViewModel, DataProviderAddedMessage>(this, (_, message) =>
        {
            PaymentProviders.Add(new PaymentProvider(message.DisplayName, message.ProviderId));
        });

        var tokens = _storage.LoadTokens();
        if (tokens is { Length: >0 })
        {
            PaymentProviders.AddRange(tokens.Select(t => new PaymentProvider(t.ProviderId, t.ProviderId)));
        }

        PaymentProviders.Add(new PaymentProvider("ALL", "All Providers"));
        SelectedPaymentProvider = PaymentProviders.FirstOrDefault();
    }

    [ObservableProperty] private bool _hasErrorsInner;

    public ObservableCollection<string> Errors { get; } = [];

    // Payment Providers
    public ObservableCollection<PaymentProvider> PaymentProviders { get; } = [];

    // Payment Schemes
    public ObservableCollection<string> PaymentSchemes { get; } =
    [
        "SEPA",
        "Instant SEPA"
    ];

    // Currencies
    public ObservableCollection<string> Currencies { get; } =
    [
        "EUR",
        "GBP"
    ];

    [ObservableProperty]
    private PaymentProvider? _selectedPaymentProvider;

    [ObservableProperty]
    private string? _selectedPaymentScheme = "Instant SEPA";

    [ObservableProperty]
    private string? _selectedCurrency = "EUR";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, 999999.99, ErrorMessage = "Amount must be between 0.01 and 999,999.99")]
    private decimal _amount = 0.01m;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Beneficiary name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Beneficiary name must be between 2 and 100 characters")]
    private string _beneficiaryName = "Test Name";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "IBAN is required")]
    [RegularExpression(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$", ErrorMessage = "Invalid IBAN format")]
    private string _beneficiaryIban = "IT0012341234567123456789";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Payment reference is required")]
    [StringLength(18, MinimumLength = 1, ErrorMessage = "Payment reference must be between 1 and 18 characters")]
    private string _paymentReference = "Test Reference";

    [RelayCommand]
    private async Task SubmitPayment()
    {
        if (SelectedPaymentProvider == null)
        {
            Console.WriteLine("Error: Please select a payment provider");
            return;
        }

        if (string.IsNullOrEmpty(SelectedPaymentScheme))
        {
            Console.WriteLine("Error: Please select a payment scheme");
            return;
        }

        if (string.IsNullOrEmpty(SelectedCurrency))
        {
            Console.WriteLine("Error: Please select a currency");
            return;
        }

        if (HasErrors)
        {
            Console.WriteLine("Error: Please fix validation errors before submitting");
            return;
        }

        var payment = await _paymentService.MakePayment(
            SelectedPaymentProvider.Id, BeneficiaryName, BeneficiaryIban, PaymentReference, SelectedCurrency, Amount,
            (SelectedPaymentScheme == "Instant SEPA" ? new SchemeSelection.InstantPreferred() : null)!);

        if (payment is null)
        {
            Errors.Add("Failed to create payment. Please check your inputs and try again.");
            return;
        }

        RequestedPayments.Add(payment);

        // TODO: this also opens a browser window, better decouple this
        _paymentService.NavigateToPaymentRedirectUri(payment);
    }

    private async Task GetPaymentStatus(string paymentId)
    {
        var status = await _paymentService.GetPaymentStatus(paymentId);
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        // Find and update the payment in the requested payments collection
        var requestedPayment = RequestedPayments.FirstOrDefault(p => p.Id == paymentId);
        if (requestedPayment is null)
        {
            return;
        }

        var index = RequestedPayments.IndexOf(requestedPayment);
        if (index >= 0)
        {
            RequestedPayments[index] = requestedPayment with { Status = status };
        }
    }

    public ObservableCollection<PaymentModel> RequestedPayments { get; } = [];
    private void OnRequestedPaymentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasPayments = RequestedPayments.Count > 0;
    }

    [ObservableProperty]
    private bool _hasPayments;

    [RelayCommand]
    private async Task RefreshPaymentStatus(PaymentModel payment)
    {
        await GetPaymentStatus(payment.Id);
    }

    [ObservableProperty] private bool _isDialogOpen;

    [RelayCommand]
    private void OpenDialog()
    {
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    public ObservableCollection<BeneficiaryModel> Beneficiaries { get; } = [];

    private async Task LoadBeneficiaries()
    {
        var beneficiaries = await _storage.Load<List<BeneficiaryModel>>("beneficiaries.json");
        if (beneficiaries != null)
        {
            Beneficiaries.AddRange(beneficiaries);
        }
    }

    [ObservableProperty] private string _newBeneficiaryName = string.Empty;
    [ObservableProperty] private string _newBeneficiaryIban = string.Empty;
    [ObservableProperty] private string _newBeneficiaryAlias = string.Empty;

    [RelayCommand]
    private async Task AddBeneficiary()
    {
        if (string.IsNullOrWhiteSpace(NewBeneficiaryName) || string.IsNullOrWhiteSpace(NewBeneficiaryIban))
        {
            _logger.LogWarning("Beneficiary name and IBAN are required.");
            return;
        }

        var newBeneficiary = new BeneficiaryModel(NewBeneficiaryName, NewBeneficiaryIban, string.IsNullOrWhiteSpace(NewBeneficiaryAlias) ? "NO-ALIAS" : NewBeneficiaryAlias);
        Beneficiaries.Add(newBeneficiary);

        // Save to storage
        var beneficiariesList = Beneficiaries.ToArray();
        await _storage.Store("beneficiaries.json", beneficiariesList);

        // Clear input fields
        NewBeneficiaryName = string.Empty;
        NewBeneficiaryIban = string.Empty;
        NewBeneficiaryAlias = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteBeneficiary(BeneficiaryModel? beneficiary)
    {
        if (beneficiary is null) return;

        Beneficiaries.Remove(beneficiary);
        await _storage.Store("beneficiaries.json", Beneficiaries.ToArray());
    }

    [ObservableProperty] private BeneficiaryModel? _selectedBeneficiary;
    partial void OnSelectedBeneficiaryChanged(BeneficiaryModel? value)
    {
        if (value is null) return;

        BeneficiaryName = value.Name;
        BeneficiaryIban = value.Iban;
        IsDialogOpen = false;
    }
}
