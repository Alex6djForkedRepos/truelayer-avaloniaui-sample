using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MobileApp.ViewModels;
using MobileApp.Views;
using TrueLayer.Caching;

namespace MobileApp;

public abstract class App : Application
{
    [SuppressMessage("ReSharper", "PublicConstructorInAbstractClass", Justification = "Warning AVLN3001 Avalonia: XAML resource \"App.axaml\" won't be reachable via runtime loader")]
    public App() { }

    public static App Instance => (Current as App)!;
    public ServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // These methods will be implemented in platform-specific projects
    protected abstract void RegisterPlatformServices(IServiceCollection services);
    protected abstract void PlatformConfiguration(ConfigurationBuilder builder);
    protected abstract string ReadResourceFile(string resourceName);

    public override void OnFrameworkInitializationCompleted()
    {
        if (Design.IsDesignMode)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var configBuilder = new ConfigurationBuilder();

        PlatformConfiguration(configBuilder);

        var config = configBuilder.Build();

        var services = new ServiceCollection();
        services
            .AddLogging(builder => builder.AddConsole())
            .AddSingleton<MainViewModel>()
            .AddSingleton<PaymentViewModel>()
            .AddSingleton<DataViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
            .AddSingleton<IAuthTokenStorage, AuthTokenStorage>()
            .AddSingleton<IAuthManager, AuthManager>()
            .AddSingleton<IPaymentService, PaymentService>()
            .AddTrueLayer(config, options =>
                {
                    if (options.Payments?.SigningKey != null)
                    {
                        options.Payments.SigningKey.PrivateKey = ReadResourceFile("ec512-private-key.pem");
                    }
                },
                authTokenCachingStrategy: AuthTokenCachingStrategies.InMemory);

        RegisterPlatformServices(services);

        Services = services.BuildServiceProvider();

        var viewModel = Services.GetRequiredService<MainViewModel>();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel,
                };
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = viewModel,
                };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
