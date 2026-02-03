using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using NLog;
using WinNUT_Client.Services;
using WinNUT_Client.ViewModels;
using WinNUT_Client.Views;

namespace WinNUT_Client;

public partial class App : Application
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	public static SettingsService Settings { get; private set; } = null!;
	public static CryptographyService Crypto { get; private set; } = null!;
	public static UpsConnectionManager UpsManager { get; private set; } = null!;
	public static NotificationService? Notifications { get; private set; }

	// Convenience property for single-UPS scenarios (returns first connected service)
	public static UpsNetworkService? UpsNetwork => UpsManager?.Configs.Count > 0 
		? UpsManager.GetService(UpsManager.Configs[0].Id) 
		: null;

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
			// Set up global exception handling
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

			// Initialize globals
			WinNutGlobals.Initialize();

			// Parse command line for custom config path
			var configPath = SettingsService.ParseConfigPathFromArgs(desktop.Args ?? Array.Empty<string>());
			Settings = configPath != null ? new SettingsService(configPath) : new SettingsService();
			Settings.Load();

			// Apply settings to logging (NLog.config handles initialization)
			LoggingSetup.SetFileLoggingEnabled(Settings.Settings.Logging.EnableFileLogging);
			LoggingSetup.SetLogLevel(Settings.Settings.Logging.LogLevel);

			// Initialize cryptography service
			Crypto = new CryptographyService();

			// Initialize UPS connection manager
			UpsManager = new UpsConnectionManager(Crypto);
			UpsManager.LoadFromSettings(Settings.Settings);

			// Save settings if migration occurred
			if (Settings.Settings.Version == 2 && Settings.Settings.Connection.Devices.Count > 0)
			{
				Settings.Save();
			}

			// Subscribe to UPS manager events for tray updates
			UpsManager.UpsConnected += OnUpsConnectedForTray;
			UpsManager.UpsDisconnected += OnUpsDisconnectedForTray;
			UpsManager.AggregateStatusChanged += OnUpsStatusChangedForTray;

			// Initialize notifications if supported
			if (NotificationService.IsSupported)
			{
				Notifications = new NotificationService();
			}

			// Avoid duplicate validations from both Avalonia and the CommunityToolkit.
			DisableAvaloniaDataAnnotationValidation();

			var mainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(),
			};
			desktop.MainWindow = mainWindow;

			// Start minimized if configured
			if (Settings.Settings.Appearance.MinimizeOnStart)
			{
				if (Settings.Settings.Appearance.MinimizeToTray)
				{
					// Don't show window at all, just tray icon
					mainWindow.ShowInTaskbar = false;
					mainWindow.WindowState = WindowState.Minimized;
					mainWindow.Hide();
				}
				else
				{
					mainWindow.WindowState = WindowState.Minimized;
				}
			}

			desktop.ShutdownRequested += OnShutdownRequested;

			// Auto-connect on startup
			_ = AutoConnectAsync();
		}

		base.OnFrameworkInitializationCompleted();
	}

	private async Task AutoConnectAsync()
	{
		// Small delay to let UI initialize
		await Task.Delay(500);

		// Check if any UPS is configured for auto-connect
		var autoConnectDevices = UpsManager.Configs.Where(c => c.Enabled && c.AutoConnectOnStartup).ToList();
		if (autoConnectDevices.Count == 0)
			return;

		// Update status via ViewModel
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
			desktop.MainWindow?.DataContext is MainWindowViewModel vm)
		{
			Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.ConnectionStatus = "Connecting...");
		}

		try
		{
			await UpsManager.ConnectAllAsync();
		}
		catch (Exception ex)
		{
			Log.Error($"Auto-connect failed: {ex.Message}");

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2 &&
				desktop2.MainWindow?.DataContext is MainWindowViewModel vm2)
			{
				Avalonia.Threading.Dispatcher.UIThread.Post(() => 
					vm2.ConnectionStatus = $"Connection failed: {ex.Message}");
			}
		}
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
		if (!UpsManager.AnyConnected)
		{
			// Update status via ViewModel
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
				desktop.MainWindow?.DataContext is MainWindowViewModel vm)
			{
				Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.ConnectionStatus = "Connecting...");
			}

			try
			{
				await UpsManager.ConnectAllAsync();
			}
			catch (Exception ex)
			{
				Log.Error($"Connection failed: {ex.Message}");

				if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2 &&
					desktop2.MainWindow?.DataContext is MainWindowViewModel vm2)
				{
					Avalonia.Threading.Dispatcher.UIThread.Post(() =>
						vm2.ConnectionStatus = $"Connection failed: {ex.Message}");
				}
			}
		}
	}

	private async void TrayIcon_Disconnect(object? sender, EventArgs e)
	{
		if (UpsManager.AnyConnected)
		{
			await UpsManager.DisconnectAllAsync();
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
				window.ShowInTaskbar = true;
				window.Show();
				window.WindowState = WindowState.Normal;
				window.Activate();
			}
		}
	}

	private void OnUpsConnectedForTray(object? sender, UpsEventArgs e)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(UpdateTrayStatus);
	}

	private void OnUpsDisconnectedForTray(object? sender, UpsEventArgs e)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(UpdateTrayStatus);
	}

	private void OnUpsStatusChangedForTray(object? sender, EventArgs e)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(UpdateTrayStatus);
	}

	private void UpdateTrayStatus()
	{
		if (_trayIcon == null) return;

		var status = UpsManager.GetAggregateStatus();

		if (status.ConnectedCount > 0)
		{
			var stateText = status.State switch
			{
				AggregateState.Online => "Online",
				AggregateState.OnBattery => "On Battery",
				AggregateState.Critical => "CRITICAL",
				_ => "Unknown"
			};

			if (status.TotalCount > 1)
			{
				_trayIcon.ToolTipText = $"WinNUT-Client - {stateText} ({status.ConnectedCount}/{status.TotalCount} UPS)";
			}
			else
			{
				// Single UPS - show more detail
				var service = UpsNetwork;
				if (service != null)
				{
					var charge = service.CurrentData.BatteryCharge;
					_trayIcon.ToolTipText = $"WinNUT-Client - {stateText} ({charge:F0}%)";
				}
				else
				{
					_trayIcon.ToolTipText = $"WinNUT-Client - {stateText}";
				}
			}
		}
		else
		{
			_trayIcon.ToolTipText = "WinNUT-Client - Not Connected";
		}
	}

	private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
	{
		// Cleanup
		UpsManager?.Dispose();
		Crypto?.Dispose();
		LoggingSetup.Shutdown();
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

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var exception = e.ExceptionObject as Exception;
		Log.Error($"Unhandled exception: {exception?.Message}");
		Log.Error(exception?.StackTrace ?? "No stack trace");

		// Try to save settings before crash
		try
		{
			Settings?.Save();
		}
		catch { }
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Log.Error($"Unobserved task exception: {e.Exception.Message}");
		Log.Error(e.Exception.StackTrace ?? "No stack trace");
		e.SetObserved(); // Prevent app crash
	}
}
