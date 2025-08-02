using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MobileApp.Models;
using AndroidContent = Android.Content;

namespace MobileApp.Android;

[Activity(
    Label = "TrueMobile",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)
]
[IntentFilter(
    [AndroidContent.Intent.ActionView],
    Categories =
    [
        AndroidContent.Intent.CategoryDefault,
        AndroidContent.Intent.CategoryBrowsable
    ],
    DataScheme = "mysecureapp",
    DataHost = "oauth2redirect",
    DataPathPrefix = "",
    AutoVerify = true
)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnNewIntent(AndroidContent.Intent? intent)
    {
        base.OnNewIntent(intent);

        HandleIntent(intent);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Intent?.Data is not null)
        {
            HandleIntent(Intent);
        }
    }

    private void HandleIntent(AndroidContent.Intent? intent)
    {
        // TODO: Find a way to use Logger<T> here
        Console.WriteLine("Handle Deep Link Intent");
        if (intent is null)
        {
            Console.WriteLine("Received null intent in OnNewIntent");
            return;
        }

        var uri = intent.DataString;
        if (string.IsNullOrEmpty(uri))
        {
            Console.WriteLine("Received null or empty URI in OnNewIntent");
            return;
        }

        var parsed = global::Android.Net.Uri.Parse(uri);
        if (parsed?.Host != "oauth2redirect")
        {
            Console.WriteLine($"Received unexpected host in OnNewIntent: {parsed?.Host}");
            return;
        }

        var queryParams = new Dictionary<string, string>();
        if (parsed.QueryParameterNames != null)
            foreach (var param in parsed.QueryParameterNames)
            {
                var value = parsed.GetQueryParameter(param);
                if (value is null) continue;
                queryParams[param] = value;
            }

        Console.WriteLine("Sending success message");
        var messenger = App.Instance.Services.GetRequiredService<IMessenger>();
        messenger.Send(new CallbackReceivedMessage(new CallbackReceivedEventArgs(queryParams)));
    }
}

public class AndroidApp : App
{
    private readonly string[] _manifestResourceNames = Assembly
        .GetExecutingAssembly()
        .GetManifestResourceNames();

    protected override string ReadResourceFile(string resourceName)
    {
        using var stream = ReadResourceStream(resourceName);
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    private Stream ReadResourceStream(string resourceName)
    {
        var appsettingsResName = _manifestResourceNames.FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
        if (appsettingsResName is null)
        {
            throw new FileNotFoundException($" The configuration file '{resourceName}' was not found and is not optional.");
        }
        var resourceStream = GetType().GetTypeInfo().Assembly.GetManifestResourceStream(appsettingsResName);
        ArgumentNullException.ThrowIfNull(resourceStream);
        return resourceStream;
    }

    protected override void RegisterPlatformServices(IServiceCollection services)
    {
        services
            .AddSingleton<IBrowserService, AndroidBrowserService>()
            .AddSingleton<IRedirectManager, AndroidRedirectManager>();
    }

    protected override void PlatformConfiguration(ConfigurationBuilder builder)
    {
        builder.AddJsonStream(ReadResourceStream("appsettings.json"));
    }
}
