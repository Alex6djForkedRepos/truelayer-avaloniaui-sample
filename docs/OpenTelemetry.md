# OpenTelemetry in Avalonia (no IHost)

Avalonia apps don't use `IHost`, so the standard `services.AddHostedService` / automatic `TracerProvider` lifetime that ASP.NET Core and Worker Service apps get for free doesn't apply. This document describes the pattern used in this repo to work around that and produce distributed traces with Honeycomb as the backend.

---

## 1. Packages

All packages are centrally managed in `src/Directory.Packages.props`. Add them to the shared project (`MobileApp.csproj`) — not to any platform-specific project, so they're available across Desktop and Android.

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="OpenTelemetry.Exporter.Console"             Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting"            Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http"          Version="1.15.1" />
```

```xml
<!-- MobileApp.csproj -->
<PackageReference Include="OpenTelemetry.Exporter.Console" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
```

`OpenTelemetry.Extensions.Hosting` provides `AddOpenTelemetry()` and `WithTracing()` as extension methods on `IServiceCollection`. The others are self-explanatory.

---

## 2. ActivitySource

Declare one static `ActivitySource` on the `App` class so it's accessible from anywhere in the app without DI:

```csharp
// App.axaml.cs
public static readonly ActivitySource Source = new("TrueMobile");
```

The name `"TrueMobile"` is the source identifier. It must match what you pass to `AddSource(...)` in the tracer configuration (see §4).

---

## 3. Platform device attributes

The tracer resource builder accepts attributes that describe the device. Because Avalonia's `App` class is abstract, the platform projects override virtual properties:

```csharp
// App.axaml.cs — defaults (Desktop / fallback)
protected virtual string DeviceId   => Environment.MachineName;
protected virtual string DeviceName => Environment.MachineName;
protected virtual string DeviceType => Environment.OSVersion.ToString();
```

```csharp
// MainActivity.cs (Android) — overrides
protected override string DeviceId =>
    Android.Provider.Settings.Secure.GetString(
        Android.App.Application.Context.ContentResolver,
        Android.Provider.Settings.Secure.AndroidId) ?? base.DeviceId;

protected override string DeviceName => Build.Manufacturer + " " + Build.Model;
protected override string DeviceType => $"Android {Build.VERSION.Release} (SDK {Build.VERSION.SdkInt})";
```

Call the properties during `OnFrameworkInitializationCompleted`, before `BuildServiceProvider`, so platform overrides are active at configuration time.

---

## 4. Tracer configuration

Wire everything up in `OnFrameworkInitializationCompleted` inside `App.axaml.cs`, after the `IConfiguration` is built but before `BuildServiceProvider`:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddSource("TrueMobile")            // must match ActivitySource name
            .SetSampler(new AlwaysOnSampler())
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("true-mobile", serviceVersion: "0.1.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = "Development",
                    ["service.instance.id"]    = DeviceId,
                    ["device.model.name"]      = DeviceName,
                    ["device.type"]            = DeviceType,
                }))
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                    activity.DisplayName = $"{request.Method.Method} {request.RequestUri}";
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(config["Honeycomb:Endpoint"]
                    ?? throw new InvalidOperationException("Honeycomb:Endpoint is not configured"));
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Headers  = $"x-honeycomb-team={config["Honeycomb:ApiKey"]
                    ?? throw new InvalidOperationException("Honeycomb:ApiKey is not configured")}";
            })
#if DEBUG
            .AddConsoleExporter()
#endif
            ;
    });
```

**Key points:**
- Both `Honeycomb:Endpoint` and `Honeycomb:ApiKey` must have `?? throw` guards. A missing `ApiKey` without a guard produces `"x-honeycomb-team="`, which Honeycomb rejects with 401 — silently, since the OTLP exporter handles transport errors internally and never throws back to the app.
- `AddHttpClientInstrumentation` automatically wraps every `HttpClient` request in a child span. Setting `DisplayName` on enrich gives readable span names in Honeycomb instead of the default `HTTP GET`.

---

## 5. Forcing TracerProvider initialization (no IHost workaround)

In a hosted app, `IHost.StartAsync()` resolves and starts all hosted services, which triggers OTel initialization. Avalonia has no such mechanism. Resolve `TracerProvider` explicitly after `BuildServiceProvider`:

```csharp
Services = services.BuildServiceProvider();

// Forces the TracerProvider singleton to be constructed and the OTLP exporter
// to connect — equivalent to what IHost does automatically.
Services.GetRequiredService<TracerProvider>();
```

Place this line **before** creating any ViewModels or showing any UI, so traces are active from the first user interaction.

If the OTLP options lambda throws (e.g. missing config key), the exception propagates unhandled here and prevents the app from starting. This is intentional fail-fast behavior — a misconfigured telemetry backend should be loud, not silent.

---

## 6. Lifecycle disposal

`TracerProvider` is `IDisposable`. It must be disposed on app exit to flush the in-flight export queue (the OTLP exporter batches spans and sends them on a background thread; an unclean exit drops the last batch).

**Desktop:**
```csharp
case IClassicDesktopStyleApplicationLifetime desktop:
    desktop.MainWindow = new MainWindow { DataContext = viewModel };
    desktop.Exit += (_, _) => Services.Dispose();
    break;
```

**Android:**
```csharp
// MainActivity.cs
protected override void OnDestroy()
{
    base.OnDestroy();
    if (IsFinishing)
        App.Instance.Services.Dispose();
}
```

The `IsFinishing` check is important on Android. `OnDestroy` is called both for final destruction and for Activity recreation triggered by unhandled configuration changes (locale, font scale, keyboard, density — anything not listed in `ConfigurationChanges`). Without the guard, a configuration change disposes the singleton `ServiceProvider` while the new Activity is still starting up, causing `ObjectDisposedException` on any subsequent service resolution.

---

## 7. Debug diagnostics listener

OpenTelemetry uses `EventSource` internally to emit its own diagnostic events (export failures, dropped spans, connectivity errors). By default these are invisible. The `OtelDiagnosticsListener` hooks into them and prints to `Console` in debug builds:

```csharp
// OtelDiagnosticsListener.cs
internal sealed class OtelDiagnosticsListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name.StartsWith("OpenTelemetry", StringComparison.Ordinal))
            EnableEvents(source, EventLevel.Verbose);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        string msg;
        try
        {
            msg = e.Payload is { Count: > 0 }
                ? string.Format(e.Message ?? string.Empty, [.. e.Payload])
                : e.Message ?? e.EventName ?? "(no message)";
        }
        catch (FormatException)
        {
            msg = e.Message ?? e.EventName ?? "(no message)";
        }
        Console.WriteLine($"[OTel/{e.Level}] {msg}");
    }
}
```

Activate it near the top of `OnFrameworkInitializationCompleted`, before any OTel setup:

```csharp
#if DEBUG
_ = new OtelDiagnosticsListener();
#endif
```

The `_ = new` discard is intentional: `EventListener` registers itself in a CLR-internal static list, so the instance won't be garbage-collected. It lives for the process lifetime, which is what you want for a diagnostic listener.

The `try/catch FormatException` around `string.Format` is a defensive guard — if an OTel event source message template has fewer `{N}` placeholders than the payload item count (possible in future OTel versions or transitive dependencies), `string.Format` would throw unhandled inside the event callback.

---

## 8. Creating custom spans in ViewModels

Use `App.Source.StartActivity()` at the top of any operation you want to trace. Wrap in `using` so the span is automatically ended (and its duration recorded) when the method returns or throws:

```csharp
[RelayCommand]
private async Task ExchangeCode(string code)
{
    using var activity = App.Source.StartActivity();
    activity?.SetTag("av.operation", "exchange-code");

    // ... rest of the method
}
```

`StartActivity()` returns `null` when no listener is active (e.g. in tests, or before the `TracerProvider` is initialized), so always use the null-conditional `?.` when setting tags or calling any `Activity` methods.

Common tags to set:
- `av.operation` — a short name for the logical operation
- `av.provider_id` — which data provider is involved, if applicable
- Any OTel semantic convention attribute that fits (`http.status_code`, `error.type`, etc.)

---

## 9. Configuration

Add the Honeycomb connection details to `appsettings.json` (excluded from source control — add it to `.gitignore` and distribute via secrets management):

```json
{
  "Honeycomb": {
    "Endpoint": "https://api.honeycomb.io/v1/traces",
    "ApiKey": "<your-team-api-key>"
  }
}
```

Both keys are required. The app will throw `InvalidOperationException` at startup if either is missing — this is intentional so misconfigurations are caught immediately rather than producing silent telemetry loss.
