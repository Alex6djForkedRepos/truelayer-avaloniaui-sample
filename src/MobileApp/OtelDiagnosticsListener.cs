using System;
using System.Diagnostics.Tracing;

namespace MobileApp;

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
