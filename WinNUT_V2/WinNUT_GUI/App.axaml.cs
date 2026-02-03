using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using WinNUT_Client.Services;
using WinNUT_Client.ViewModels;
using WinNUT_Client.Views;

namespace WinNUT_Client;

public partial class App : Application
{
	public static SettingsService Settings { get; private set; } = null!;
	public static UpsNetworkService UpsNetwork { get; private set; } = null!;
	public static NotificationService? Notifications { get; private set; }

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			// Initialize globals
			WinNutGlobals.Initialize();

			// Parse command line for custom config path
			var configPath = SettingsService.ParseConfigPathFromArgs(desktop.Args ?? Array.Empty<string>());
			Settings = configPath != null ? new SettingsService(configPath) : new SettingsService();
			Settings.Load();

			// Initialize logging
			LoggingService.Initialize(
				Settings.Settings.Logging.EnableFileLogging,
				Settings.Settings.Logging.LogLevel);

			// Initialize UPS network service
			UpsNetwork = new UpsNetworkService();
			ApplySettingsToUpsNetwork();

			// Initialize notifications if supported
			if (NotificationService.IsSupported)
			{
				Notifications = new NotificationService();
			}

			// Avoid duplicate validations from both Avalonia and the CommunityToolkit.
			DisableAvaloniaDataAnnotationValidation();

			desktop.MainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(),
			};

			desktop.ShutdownRequested += OnShutdownRequested;
		}

		base.OnFrameworkInitializationCompleted();
	}

	public static void ApplySettingsToUpsNetwork()
	{
		var conn = Settings.Settings.Connection;
		var power = Settings.Settings.Power;

		UpsNetwork.Host = conn.ServerAddress;
		UpsNetwork.Port = conn.Port;
		UpsNetwork.UpsName = conn.UpsName;
		UpsNetwork.PollingIntervalMs = conn.PollingIntervalSeconds * 1000;
		UpsNetwork.Login = conn.Login;
		UpsNetwork.AutoReconnect = conn.AutoReconnect;
		UpsNetwork.BatteryLimitPercent = power.ShutdownLimitBatteryCharge;
		UpsNetwork.BackupLimitSeconds = power.ShutdownLimitUpsRemainTimeSeconds;
		UpsNetwork.FollowFsd = power.FollowFsd;
		UpsNetwork.DefaultFrequencyHz = Settings.Settings.Calibration.FrequencySupply == FrequencyType.Hz50 ? 50 : 60;

		// Decrypt password if present
		if (!string.IsNullOrEmpty(conn.EncryptedPassword))
		{
			using var crypto = new CryptographyService();
			try
			{
				UpsNetwork.Password = crypto.Decrypt(conn.EncryptedPassword);
			}
			catch
			{
				// Password decryption failed, leave empty
				UpsNetwork.Password = null;
			}
		}
	}

	private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		// Cleanup
		UpsNetwork?.Dispose();
		LoggingService.Shutdown();
	}

	private void DisableAvaloniaDataAnnotationValidation()
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
