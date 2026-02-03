using Avalonia;
using Avalonia.Controls;
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

	private TrayIcon? _trayIcon;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

		// Get tray icon references after XAML load
		var trayIcons = TrayIcon.GetIcons(this);
		_trayIcon = trayIcons?.FirstOrDefault();
		if (_trayIcon != null)
		{
			_trayIcon.Clicked += TrayIcon_Clicked;
		}
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

			// Subscribe to UPS events for tray updates
			UpsNetwork.Connected += OnUpsConnectedForTray;
			UpsNetwork.Disconnected += OnUpsDisconnectedForTray;
			UpsNetwork.DataUpdated += OnUpsDataUpdatedForTray;

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

	private void TrayIcon_Clicked(object? sender, EventArgs e)
	{
		ShowMainWindow();
	}

	private void TrayIcon_ShowWindow(object? sender, EventArgs e)
	{
		ShowMainWindow();
	}

	private async void TrayIcon_Connect(object? sender, EventArgs e)
	{
		if (!UpsNetwork.IsConnected)
		{
			try
			{
				await UpsNetwork.ConnectAsync();
			}
			catch (Exception ex)
			{
				LoggingService.Error($"Connection failed: {ex.Message}");
			}
		}
	}

	private async void TrayIcon_Disconnect(object? sender, EventArgs e)
	{
		if (UpsNetwork.IsConnected)
		{
			await UpsNetwork.DisconnectAsync();
		}
	}

	private void TrayIcon_Preferences(object? sender, EventArgs e)
	{
		ShowMainWindow();
		// The MainWindow will handle showing preferences
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
			desktop.MainWindow?.DataContext is MainWindowViewModel vm)
		{
			vm.ShowPreferencesCommand.Execute(null);
		}
	}

	private void TrayIcon_Exit(object? sender, EventArgs e)
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}

	private void ShowMainWindow()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var window = desktop.MainWindow;
			if (window != null)
			{
				window.Show();
				window.WindowState = WindowState.Normal;
				window.Activate();
			}
		}
	}

	private void OnUpsConnectedForTray(object? sender, EventArgs e)
	{
		UpdateTrayStatus();
	}

	private void OnUpsDisconnectedForTray(object? sender, EventArgs e)
	{
		UpdateTrayStatus();
	}

	private void OnUpsDataUpdatedForTray(object? sender, EventArgs e)
	{
		UpdateTrayStatus();
	}

	private void UpdateTrayStatus()
	{
		if (_trayIcon == null) return;

		if (UpsNetwork.IsConnected)
		{
			var charge = UpsNetwork.CurrentData.BatteryCharge;
			var status = UpsNetwork.CurrentData.Status;
			_trayIcon.ToolTipText = $"WinNUT-Client - {status} ({charge:F0}%)";
		}
		else
		{
			_trayIcon.ToolTipText = "WinNUT-Client - Not Connected";
		}
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
