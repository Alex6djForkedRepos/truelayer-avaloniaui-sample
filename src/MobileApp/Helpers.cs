using System.Collections.Generic;
using System.Linq;

namespace MobileApp;

public static class Helpers
{
    public static string ExtractErrors(Dictionary<string, string[]>? dict)
    {
        if (dict is null) return string.Empty;

        var errors = dict.SelectMany(kvp => kvp.Value.Select(error => $"{kvp.Key}: {error}"))
            .Aggregate((current, next) => $"{current}, {next}");

        return errors;
    }

    public static string GetCurrencySymbol(string currency)
    {
        return currency switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            _ => currency,
        };
    }
}
