# TrueMobile – Codebase Review & Improvement Suggestions

> Audience: solo developer / SE. Focus is on practical fixes, not theoretical cleanliness.

---

## 1. What the App Is

A cross-platform Avalonia UI developer tool (Desktop + Android) for hands-on testing of TrueLayer's Data and Payments APIs. Three tabs:

| Tab | Purpose |
|---|---|
| **Data** | OAuth-link bank accounts, view account balances |
| **Payments** | Create SEPA/Instant-SEPA bank transfers, manage saved beneficiaries, track statuses |
| **Settings** | Manage linked providers, export/import a JSON backup |

Stack: .NET 10, Avalonia, CommunityToolkit.Mvvm, OpenTelemetry → Honeycomb, TrueLayer .NET SDK (git submodule).

---

## 2. Code Quality Fixes

### 2.1 `Console.WriteLine` instead of the injected logger

`PaymentViewModel.SubmitPayment` logs errors with `Console.WriteLine`. `MainActivity.HandleIntent` does the same. Both already receive or can receive a logger — the guard messages should go through `_logger` so they show up in OTel/console correctly and can be filtered by level.

**Files:** `src/MobileApp/ViewModels/PaymentViewModel.cs:138–158`, `src/MobileApp.Android/MainActivity.cs:71–102`

---

### 2.2 Duplicate event subscription in `DesktopRedirectManager`

`NavigateToRedirectUri` does `authManager.CallbackReceived += OnRedirectSuccess` every call without ever unsubscribing. If the user triggers multiple OAuth flows without restarting, `OnRedirectSuccess` fires multiple times per redirect. Fix: unsubscribe at the top of `OnRedirectSuccess`, or use `-=` before `+=`.

**File:** `src/MobileApp.Desktop/DesktopRedirectManager.cs:12–13`

---

### 2.3 `AuthManager` handles only a single callback then stops

`DoStuff` starts listening, handles one request, then calls `_listener.Stop()`. A second "Add account" attempt silently does nothing. The listener should either restart or loop until explicitly stopped.

**File:** `src/MobileApp/IAuthManager.cs:44–79`

---

### 2.4 Redundant navigation state in `MainViewModel`

Nine observable properties (`DataButtonFontWeight`, `PaymentsButtonFontWeight`, `SettingsButtonFontWeight`, and three `*ButtonForeground`) are manually kept in sync across three near-identical commands. Replace with a single `ActivePage` enum and compute weight/foreground via a converter or a helper method called once. Eliminates ~70 lines of brittle mirroring.

**File:** `src/MobileApp/ViewModels/MainViewModel.cs`

---

### 2.5 `DataViewModel.Logos()` allocates a new `Bitmap` on every call

Each account load creates a fresh `Bitmap` from the asset stream for every account entry. Cache the bitmaps in a static `Dictionary<string, Bitmap>` (or `Lazy<Bitmap>`) — they never change at runtime.

**File:** `src/MobileApp/ViewModels/DataViewModel.cs:247–253`

---

### 2.6 `GetAccountsAsync` `CanExecute` doesn't react to `Tokens` changes

`HasAccessToken()` checks `Tokens.Count` but `Tokens` is a plain `List<T>`. The button's enabled state won't update reactively when tokens are added or removed. Either switch `Tokens` to an `ObservableCollection` (and re-raise `GetAccountsAsyncCommand.NotifyCanExecuteChanged()` in the collection's changed handler), or just remove the `CanExecute` guard since the method is only called internally after a successful auth.

**File:** `src/MobileApp/ViewModels/DataViewModel.cs:219`

---

### 2.7 Messenger handlers never unregistered

`DataViewModel` and `PaymentViewModel` register three handlers each in their constructors but implement no `IDisposable` / never call `messenger.Unregister`. With `WeakReferenceMessenger` this is low-risk (GC collects dead registrations), but it's worth making explicit — either implement `IDisposable` and call `messenger.UnregisterAll(this)`, or document why it's safe.

---

### 2.8 `FetchProviders` is dead code

`PaymentService.FetchProviders()` is private, never called, and suppressed via `ReSharper disable`. Delete it or promote it to the interface if the provider-selection feature is planned.

**File:** `src/MobileApp/IPaymentService.cs:181–193`

---

### 2.9 `PaymentModel` has mutable `required` properties on a `record`

Properties like `Id`, `Status`, etc. are declared with `{ get; set; }` on a `record`, which defeats value-based equality and makes the record look mutable. Either use `init` (for proper immutability) or switch to a plain `class`. Currently `RequestedPayments[index] = requestedPayment with { Status = status }` works, but this is fragile if properties are later set directly.

**File:** `src/MobileApp/Models/PaymentModel.cs`

---

### 2.10 Tokens stored unencrypted on disk

`settings.json` under `~/TrueLayerMobile/secrets/` contains refresh tokens as plaintext JSON. On desktop this is fine for a dev tool, but on Android the `Personal` folder is app-private storage anyway. Still, worth noting: the export also ships refresh tokens in plaintext. Consider using the OS credential store (`DPAPI` on Windows, `SecretService` on Linux, `Keychain` on Android) for at least the token file, even in a dev tool.

---

### 2.11 `ImportSettings` is not atomic

If `StoreTokens` succeeds but `Store("beneficiaries.json", ...)` fails, the app ends up in a half-imported state. The TODO comment acknowledges this. A simple fix: write to `*.tmp` files and rename at the end, or wrap in a try/catch that rolls back by restoring the original files.

**File:** `src/MobileApp/IAuthTokenStorage.cs:107–117`

---

### 2.12 `ExportSettings` captures short-lived access tokens

Access tokens are included in the backup. They'll be expired within minutes. Export only refresh tokens (and beneficiaries) unless there's a reason to include access tokens.

---

### 2.13 Hard-coded callback port 3000

Marked with `// TODO: make this configurable` but also hard-coded in `DesktopRedirectManager.RedirectUri`. Pulling it from `appsettings.json` would let you run two instances or avoid port conflicts.

**Files:** `src/MobileApp/IAuthManager.cs:32`, `src/MobileApp.Desktop/DesktopRedirectManager.cs:9`

---

### 2.14 Error auto-clear races under rapid errors

`ClearErrorAsync` delays 5 s then removes `Errors[0]`. If multiple errors arrive in quick succession, earlier clear tasks may clear errors added after them. A safer approach: give each error an ID and only clear the specific one that was scheduled for removal.

**File:** `src/MobileApp/ViewModels/DataViewModel.cs:283–288`

---

### 2.15 `CreateHostedPaymentPageLink` has side-effects beyond its name

`IPaymentService.CreateHostedPaymentPageLink` also opens a browser window. The name implies it only builds a URL. Either rename it (`OpenHostedPaymentPage`) or split creation from navigation. The `PaymentViewModel` even has a `// TODO: decouple this` comment for exactly this reason.

**File:** `src/MobileApp/ViewModels/PaymentViewModel.cs:173`

---

## 3. Architecture / Design

### 3.1 No test project despite having Fakes

`src/MobileApp/Fakes/` contains `FakeAuthManager`, `FakeAuthTokenStorage`, `FakePaymentService`, etc., which are clearly intended for tests. But there is no test project in the solution. Even a small xUnit/NUnit project with ViewModel unit tests (e.g., "submitting with invalid IBAN shows error", "removing account fires AccountRemovedMessage") would give fast feedback and document expected behavior.

---

### 3.2 `App.axaml.cs` throws on missing Honeycomb config at startup

```csharp
options.Endpoint = new Uri(config["Honeycomb:Endpoint"]
    ?? throw new InvalidOperationException("NULL Honeycomb:Endpoint configuration value"));
```

If you clone the repo with a blank `appsettings.json`, the app crashes immediately. A null-conditional + console fallback would make it easier to run offline:

```csharp
if (config["Honeycomb:Endpoint"] is { } endpoint)
    .AddOtlpExporter(...)
else
    .AddConsoleExporter();
```

---

### 3.3 `ObservableCollectionExtensions.AddRange` — confirm thread-safety

`AddRange` on an `ObservableCollection` fires `CollectionChanged` once per item by default in Avalonia. If this is called from non-UI threads (e.g., after `await` in `GetAccountsAsync`), it may trigger cross-thread binding updates. Ensure all `Balances.Add` / `AddRange` calls run on the UI thread (Avalonia's `Dispatcher.UIThread.Post` or `InvokeAsync`).

---

## 4. New Feature Ideas

### 4.1 Transaction history per account (high value)

The OAuth scopes already include `transactions`. Tapping an account card in the Data tab could drill down to show recent transactions (date, description, amount). This is the most useful addition for testing the Data API end-to-end.

---

### 4.2 Standing orders & direct debits display

Scopes include `standing_orders` and `direct_debits` but the Data tab only shows balances. A secondary section or expandable card per account showing these would complete the Data API coverage.

---

### 4.3 Payment status auto-poll

Instead of a manual "refresh" button per payment, poll every N seconds for payments in `authorization_required` or `authorizing` state, stopping when the status reaches a terminal state (`executed`, `settled`, `failed`). A `PeriodicTimer` in `PaymentViewModel` with a short interval (3–5 s) would do it cleanly.

---

### 4.4 Persist payment history to disk

`RequestedPayments` is lost on every app restart. Saving it to `payments.json` alongside `beneficiaries.json` (same pattern already used) would let you review past test runs.

---

### 4.5 Configurable user email for payments

There's a `// TODO: Make email configurable` in `PaymentService.MakePayment`. Adding an `Email` field to `SettingsViewModel` (persisted in `settings.json`) and wiring it through `IPaymentService.MakePayment` is a small but useful change — some providers send a payment notification email.

---

### 4.6 Environment switcher (sandbox ↔ production)

`appsettings.json` has `"UseSandbox": false` with separate URIs. A toggle in the Settings tab that swaps `UseSandbox` and re-registers TrueLayer services would be more convenient than editing the JSON file.

> Requires making the `TrueLayer` registration options-based at runtime instead of at DI build time.

---

### 4.7 Provider search / filter before payment

The dead `FetchProviders()` method and the `ProviderFilter.ReleaseChannel = "private_beta"` suggest provider discovery was planned. A simple provider list (country filter: IT / GB) loaded once and cached to `providers.json` would let you pick a specific provider without typing its ID.

---

### 4.8 Copy-to-clipboard buttons

Payment IDs, IBANs, and trace IDs appear in the UI but can't be copied without selecting text (awkward on mobile). Small clipboard-copy icon buttons next to these values would speed up debugging (pasting IDs into Honeycomb queries, etc.).

---

### 4.9 In-app HTTP request log

Since OTel is already capturing HTTP spans, a fourth "Logs" tab (or an expandable drawer) showing the last N HTTP calls (method, URL, status, duration) would be a fast feedback loop without opening Honeycomb. A simple ring-buffer of `ActivityEvent` captures from the `TrueMobile` `ActivitySource` would suffice.

---

### 4.10 Amount quick-select presets

A row of preset buttons (e.g., `0.01`, `1.00`, `10.00`, `100.00`) above the amount field would save typing during repetitive testing — especially on Android where the numeric keyboard is clunky.

---

### 4.11 Token expiry display in Settings

`OAuthToken.ExpiresIn` is stored but never shown. Displaying the expiry time next to each provider in the Settings tab (converted from seconds since last refresh to an absolute time) would let you know when a token is about to go stale without waiting for a 401.

> Requires storing the refresh timestamp alongside the token — `DateTimeOffset.UtcNow` at `AddTokenAsync` time.

---

### 4.12 macOS / iOS support

The desktop project targets `net10.0` (no OS suffix) which covers Linux and Windows. Adding a `net10.0-macos` variant with platform-specific Keychain storage and a native menu would be a natural extension given Avalonia's cross-platform capabilities. iOS would follow the Android pattern closely.

---

### 4.13 Beneficiary IBAN validation improvement

The current IBAN regex (`^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$`) validates format but not the mod-97 checksum. A proper IBAN validator (a few lines of standard algorithm) would catch typos before they reach the API and give a more specific error message.

---

### 4.14 Payment QR code for HPP URL

After a payment is created, generating a QR code for the Hosted Payment Page URL would let you quickly scan it on a different device to test the bank redirect flow without copy-pasting URLs. Several lightweight QR-generation NuGet packages work on .NET without native dependencies.

---

## 5. Quick-win Priority Order

| # | Item | Effort |
|---|---|---|
| 1 | Fix duplicate event subscription (§2.2) | < 5 min |
| 2 | Fix `AuthManager` single-use listener (§2.3) | ~30 min |
| 3 | Replace `Console.WriteLine` with logger (§2.1) | ~10 min |
| 4 | Cache bank logo bitmaps (§2.5) | ~10 min |
| 5 | Make Honeycomb config optional / fall back to console (§3.2) | ~15 min |
| 6 | Persist payment history (§4.4) | ~1 h |
| 7 | Transaction history drill-down (§4.1) | ~half day |
| 8 | Payment status auto-poll (§4.3) | ~1 h |
| 9 | Add a test project using existing Fakes (§3.1) | ~2 h |
| 10 | Environment switcher (§4.6) | ~2 h |
