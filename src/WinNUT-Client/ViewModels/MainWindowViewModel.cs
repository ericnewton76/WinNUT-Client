using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

/// <summary>
/// Summary information for a UPS shown in the sidebar.
/// </summary>
public partial class UpsSummary : ObservableObject
{
	public Guid Id { get; set; }

	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _host = string.Empty;

	[ObservableProperty]
	private bool _isOnline;

	[ObservableProperty]
	private bool _isOnBattery;

	[ObservableProperty]
	private bool _isConnected;

	[ObservableProperty]
	private double _batteryCharge;

	[ObservableProperty]
	private IBrush _statusColor = Brushes.Gray;

	[ObservableProperty]
	private string _statusText = "Unknown";

	[ObservableProperty]
	private bool _isSelected;

	public string DisplayName => string.IsNullOrEmpty(Name) ? Host : Name;
}

public partial class MainWindowViewModel : ViewModelBase
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private UpsNetworkService? _upsNetwork;
	private readonly System.Timers.Timer _freshnessTimer;
	private DateTime _lastUpdateTime;

	// Multi-UPS sidebar
	[ObservableProperty]
	private ObservableCollection<UpsSummary> _upsList = new();

	[ObservableProperty]
	private UpsSummary? _selectedUps;

	[ObservableProperty]
	private bool _showSidebar;

	/// <summary>
	/// Recommended window width based on sidebar visibility.
	/// </summary>
	public double RecommendedWidth => ShowSidebar ? 750 : 550;

	partial void OnShowSidebarChanged(bool value)
	{
		OnPropertyChanged(nameof(RecommendedWidth));
	}

	[ObservableProperty]
	private string _connectionStatus = "Not Connected";

	[ObservableProperty]
	private bool _isConnected;

	[ObservableProperty]
	private string _upsManufacturer = string.Empty;

	[ObservableProperty]
	private string _upsModel = string.Empty;

	[ObservableProperty]
	private string _upsSerial = string.Empty;

	[ObservableProperty]
	private string _upsStatus = string.Empty;

	[ObservableProperty]
	private string _upsStatusDisplay = "Unknown";

	[ObservableProperty]
	private IBrush _statusBadgeColor = Brushes.Gray;

	[ObservableProperty]
	private string _batteryIconPath = "/Assets/1057.ico";  // Default: Battery0 + OnLine

	[ObservableProperty]
	private double _batteryCharge;

	[ObservableProperty]
	private double _batteryVoltage;

	[ObservableProperty]
	private double _batteryRuntimeMinutes;

	[ObservableProperty]
	private string _batteryRuntimeDisplay = "--";

	[ObservableProperty]
	private double _inputVoltage;

	[ObservableProperty]
	private double _outputVoltage;

	[ObservableProperty]
	private double _inputFrequency;

	[ObservableProperty]
	private double _load;

	[ObservableProperty]
	private double _outputPower;

	[ObservableProperty]
	private int _retryCount;

	[ObservableProperty]
	private IBrush _dataFreshnessColor = Brushes.Gray;

	[ObservableProperty]
	private string _dataFreshnessTooltip = "No data";

	public MainWindowViewModel()
	{
		_upsNetwork = App.UpsNetwork;

		// Timer to update freshness indicator
		_freshnessTimer = new System.Timers.Timer(1000);
		_freshnessTimer.Elapsed += (_, _) => UpdateFreshnessIndicator();
		_freshnessTimer.AutoReset = true;

		// Subscribe to UPS manager events (works for all UPS devices)
		var manager = App.UpsManager;
		if (manager != null)
		{
			manager.UpsConnected += OnUpsManagerConnected;
			manager.UpsDisconnected += OnUpsManagerDisconnected;
			manager.UpsDataUpdated += OnUpsManagerDataUpdated;
			manager.UpsConnectionLost += OnUpsManagerConnectionLost;
			manager.ShutdownConditionMet += OnUpsShutdownConditionMet;
			manager.ConfigsReloaded += OnConfigsReloaded;

			// Initialize sidebar with configured UPS devices
			InitializeSidebar();
		}

		// Also subscribe to first service directly for backward compatibility
		if (_upsNetwork != null)
		{
			_upsNetwork.RetryAttempt += OnUpsRetryAttempt;
			_upsNetwork.ShutdownCancelled += OnUpsShutdownCancelled;
		}
	}

	private void OnConfigsReloaded(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(RefreshSidebar);
	}

	private void RefreshSidebar()
	{
		var manager = App.UpsManager;
		if (manager == null) return;

		// Remember selected UPS ID
		var selectedId = SelectedUps?.Id;

		// Clear and rebuild
		UpsList.Clear();
		
		foreach (var config in manager.Configs)
		{
			var service = manager.GetService(config.Id);
			var summary = new UpsSummary
			{
				Id = config.Id,
				Name = config.DisplayName,
				Host = config.Address,
				IsConnected = service?.IsConnected ?? false,
				StatusColor = service?.IsConnected == true ? Brushes.Green : Brushes.Gray,
				StatusText = service?.IsConnected == true ? "Connected" : "Not Connected"
			};
			
			// Update with current data if connected
			if (service?.IsConnected == true && service.CurrentData != null)
			{
				var data = service.CurrentData;
				summary.BatteryCharge = data.BatteryCharge;
				summary.IsOnline = data.IsOnline;
				summary.IsOnBattery = data.IsOnBattery;
				
				if (data.IsOnBattery)
				{
					summary.StatusColor = data.IsLowBattery ? Brushes.Red : Brushes.Orange;
					summary.StatusText = data.IsLowBattery ? "Low Battery" : "On Battery";
				}
				else
				{
					summary.StatusColor = Brushes.Green;
					summary.StatusText = "Online";
				}
			}
			
			UpsList.Add(summary);
		}

		// Restore selection or select first
		UpsSummary? toSelect = null;
		if (selectedId != null)
		{
			toSelect = UpsList.FirstOrDefault(u => u.Id == selectedId);
		}
		toSelect ??= UpsList.FirstOrDefault();
		
		if (toSelect != null)
		{
			toSelect.IsSelected = true;
			SelectedUps = toSelect;
			_upsNetwork = manager.GetService(toSelect.Id);
		}
		else
		{
			SelectedUps = null;
			_upsNetwork = null;
		}

		// Show sidebar if more than one UPS
		ShowSidebar = UpsList.Count > 1;
	}

	private void InitializeSidebar()
	{
		var manager = App.UpsManager;
		if (manager == null) return;

		foreach (var config in manager.Configs)
		{
			var summary = new UpsSummary
			{
				Id = config.Id,
				Name = config.DisplayName,
				Host = $"{config.Host}:{config.Port}",
				IsConnected = false,
				StatusColor = Brushes.Gray,
				StatusText = "Not Connected"
			};
			UpsList.Add(summary);
		}

		// Select first UPS if any
		if (UpsList.Count > 0)
		{
			UpsList[0].IsSelected = true;
			SelectedUps = UpsList[0];
			_upsNetwork = manager.GetService(UpsList[0].Id);
		}

		// Show sidebar if more than one UPS
		ShowSidebar = UpsList.Count > 1;
	}

	private void OnUpsManagerConnected(object? sender, UpsEventArgs e)
	{
		// Update _upsNetwork reference to the connected service
		_upsNetwork = e.Service;
		Dispatcher.UIThread.Post(() =>
		{
			OnUpsConnected(sender, EventArgs.Empty);
		});
	}

	private void OnUpsManagerDisconnected(object? sender, UpsEventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			OnUpsDisconnected(sender, EventArgs.Empty);
		});
	}

	private void OnUpsManagerDataUpdated(object? sender, UpsEventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			OnUpsDataUpdated(sender, EventArgs.Empty);
		});
	}

	private void OnUpsManagerConnectionLost(object? sender, UpsEventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			OnUpsConnectionLost(sender, EventArgs.Empty);
		});
	}

	private void OnUpsShutdownConditionMet(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			OnUpsShutdownCondition(sender, EventArgs.Empty);
		});
	}

	[RelayCommand]
	private async Task ConnectAsync()
	{
		if (IsConnected)
			return;

		ConnectionStatus = "Connecting...";
		try
		{
			await App.UpsManager.ConnectAllAsync();
		}
		catch (Exception ex)
		{
			ConnectionStatus = $"Connection failed: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task DisconnectAsync()
	{
		if (!IsConnected)
			return;

		await App.UpsManager.DisconnectAllAsync();
	}

	/// <summary>
	/// Selects a UPS from the sidebar.
	/// </summary>
	public void SelectUps(UpsSummary ups)
	{
		// Deselect all
		foreach (var u in UpsList)
		{
			u.IsSelected = false;
		}

		// Select the clicked one
		ups.IsSelected = true;
		SelectedUps = ups;

		// Switch the active UPS service reference
		var service = App.UpsManager.GetService(ups.Id);
		if (service != null)
		{
			_upsNetwork = service;
			// Update display with this UPS's data
			if (service.IsConnected)
			{
				OnUpsDataUpdated(this, EventArgs.Empty);
			}
		}
	}

	[RelayCommand]
	private async Task ConnectUpsAsync(UpsSummary? ups)
	{
		if (ups == null) return;
		
		try
		{
			await App.UpsManager.ConnectAsync(ups.Id);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to connect UPS {Name}", ups.DisplayName);
		}
	}

	[RelayCommand]
	private async Task DisconnectUpsAsync(UpsSummary? ups)
	{
		if (ups == null) return;

		try
		{
			await App.UpsManager.DisconnectAsync(ups.Id);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to disconnect UPS {Name}", ups.DisplayName);
		}
	}

	[RelayCommand]
	private void Exit()
	{
		if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			lifetime.Shutdown();
		}
	}

	[RelayCommand]
	private async Task ShowPreferencesAsync()
	{
		var prefsWindow = new Views.PreferencesWindow();
		if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			await prefsWindow.ShowDialog(lifetime.MainWindow!);

			// After preferences saved, reload settings to UPS manager
			if (prefsWindow.DataContext is PreferencesViewModel vm && vm.DialogResult == true)
			{
				// Settings already saved - manager will pick up changes on next connection
				// If connected, would need to reconnect to apply connection changes
			}
		}
	}

	[RelayCommand]
	private async Task ShowUpsVariablesAsync()
	{
		var varsWindow = new Views.UpsVariablesWindow();
		if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			await varsWindow.ShowDialog(lifetime.MainWindow!);
		}
	}

	[RelayCommand]
	private async Task ShowAboutAsync()
	{
		var aboutWindow = new Views.AboutWindow();
		if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			await aboutWindow.ShowDialog(lifetime.MainWindow!);
		}
	}

	[RelayCommand]
	private void ViewLogFile()
	{
		var logPath = LoggingSetup.LogFilePath;
		if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
		{
			Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
		}
	}

	[RelayCommand]
	private void OpenGitHub()
	{
		Process.Start(new ProcessStartInfo(WinNutGlobals.GitHubUrl) { UseShellExecute = true });
	}

	private void OnUpsConnected(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			IsConnected = App.UpsManager?.AnyConnected ?? false;
			var host = _upsNetwork?.Host ?? "unknown";
			var port = _upsNetwork?.Port ?? 0;
			ConnectionStatus = $"Connected to {host}:{port}";
			RetryCount = 0;
			_freshnessTimer.Start();

			// Add/update UPS in sidebar list
			UpdateUpsSidebar();
		});
	}

	private void UpdateUpsSidebar()
	{
		// Update sidebar from all UPS configs
		var manager = App.UpsManager;
		if (manager == null) return;

		foreach (var config in manager.Configs)
		{
			var service = manager.GetService(config.Id);
			var existing = UpsList.FirstOrDefault(u => u.Id == config.Id);
			
			if (existing == null)
			{
				existing = new UpsSummary
				{
					Id = config.Id,
					Name = config.DisplayName,
					Host = $"{config.Host}:{config.Port}"
				};
				UpsList.Add(existing);
			}

			// Update from service data if connected
			if (service != null && service.IsConnected)
			{
				existing.IsConnected = true;
				existing.IsOnline = service.CurrentData.IsOnline;
				existing.IsOnBattery = service.CurrentData.IsOnBattery;
				existing.BatteryCharge = service.CurrentData.BatteryCharge;
				existing.StatusColor = existing.IsOnline ? Brushes.Green : (existing.IsOnBattery ? Brushes.Orange : Brushes.Gray);
				existing.StatusText = existing.IsOnline ? "Online" : (existing.IsOnBattery ? "On Battery" : "Unknown");
			}
			else
			{
				existing.IsConnected = false;
				existing.StatusColor = Brushes.Gray;
				existing.StatusText = "Disconnected";
			}
		}

		// Select first UPS if none selected
		if (SelectedUps == null && UpsList.Count > 0)
		{
			var first = UpsList.First();
			first.IsSelected = true;
			SelectedUps = first;
			_upsNetwork = manager.GetService(first.Id);
		}

		// Show sidebar if more than one UPS
		ShowSidebar = UpsList.Count > 1;
	}

	private void OnUpsDisconnected(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			IsConnected = App.UpsManager?.AnyConnected ?? false;
			
			if (!IsConnected)
			{
				ConnectionStatus = "Disconnected";
				_freshnessTimer.Stop();
				DataFreshnessColor = Brushes.Gray;
				DataFreshnessTooltip = "Not connected";
				ClearUpsData();
			}
			
			UpdateUpsSidebar();
		});
	}

	private void OnUpsConnectionLost(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			IsConnected = App.UpsManager?.AnyConnected ?? false;
			ConnectionStatus = "Connection lost - reconnecting...";
			DataFreshnessColor = Brushes.Red;
			DataFreshnessTooltip = "Connection lost";
			UpdateUpsSidebar();
		});

		App.Notifications?.NotifyDisconnected();
	}

	private void OnUpsDataUpdated(object? sender, EventArgs e)
	{
		if (_upsNetwork == null) return;
		var data = _upsNetwork.CurrentData;

		Dispatcher.UIThread.Post(() =>
		{
			UpsManufacturer = data.Manufacturer;
			UpsModel = data.Model;
			UpsSerial = data.Serial;
			UpsStatus = data.Status;
			BatteryCharge = data.BatteryCharge;
			BatteryVoltage = data.BatteryVoltage;
			BatteryRuntimeMinutes = data.BatteryRuntimeSeconds / 60.0;
			InputVoltage = data.InputVoltage;
			OutputVoltage = data.OutputVoltage;
			InputFrequency = data.InputFrequency;
			Load = data.Load;
			OutputPower = data.OutputPower;

			// Update display properties
			UpdateStatusDisplay(data);
			UpdateBatteryRuntimeDisplay();

			// Update sidebar
			UpdateUpsSidebar();

			_lastUpdateTime = DateTime.Now;
			UpdateFreshnessIndicator();
		});
	}

	private void UpdateStatusDisplay(UpsData data)
	{
		if (data.IsOnline)
		{
			UpsStatusDisplay = data.IsCharging ? "Online (Charging)" : "Online";
			StatusBadgeColor = Brushes.Green;
		}
		else if (data.IsOnBattery)
		{
			UpsStatusDisplay = data.IsLowBattery ? "On Battery (Low!)" : "On Battery";
			StatusBadgeColor = data.IsLowBattery ? Brushes.Red : Brushes.Orange;
		}
		else if (data.IsForcedShutdown)
		{
			UpsStatusDisplay = "Forced Shutdown";
			StatusBadgeColor = Brushes.Red;
		}
		else
		{
			UpsStatusDisplay = data.Status;
			StatusBadgeColor = Brushes.Gray;
		}

		// Update battery icon
		BatteryIconPath = GetBatteryIconPath(data);
	}

	private static string GetBatteryIconPath(UpsData data)
	{
		// Calculate icon index using AppIconIndex bit flags
		int iconIndex = (int)AppIconIndex.Offset;

		// Battery level
		if (data.BatteryCharge >= 87.5)
			iconIndex |= (int)AppIconIndex.Battery100;
		else if (data.BatteryCharge >= 62.5)
			iconIndex |= (int)AppIconIndex.Battery75;
		else if (data.BatteryCharge >= 37.5)
			iconIndex |= (int)AppIconIndex.Battery50;
		else if (data.BatteryCharge >= 12.5)
			iconIndex |= (int)AppIconIndex.Battery25;
		else
			iconIndex |= (int)AppIconIndex.Battery0;

		// Online/Offline status
		if (data.IsOnline)
			iconIndex |= (int)AppIconIndex.OnLine;

		return $"/Assets/{iconIndex}.ico";
	}

	private void UpdateBatteryRuntimeDisplay()
	{
		if (BatteryRuntimeMinutes < 1)
		{
			BatteryRuntimeDisplay = $"{BatteryRuntimeMinutes * 60:F0} sec";
		}
		else if (BatteryRuntimeMinutes < 60)
		{
			BatteryRuntimeDisplay = $"{BatteryRuntimeMinutes:F0} min";
		}
		else
		{
			var hours = (int)(BatteryRuntimeMinutes / 60);
			var mins = (int)(BatteryRuntimeMinutes % 60);
			BatteryRuntimeDisplay = $"{hours}h {mins}m";
		}
	}

	private void UpdateFreshnessIndicator()
	{
		if (!IsConnected || _upsNetwork == null)
			return;

		var age = DateTime.Now - _lastUpdateTime;
		var pollingInterval = _upsNetwork.PollingIntervalMs / 1000.0;

		// Green: within 2x polling interval, Yellow: within 4x, Red: stale
		if (age.TotalSeconds < pollingInterval * 2)
		{
			DataFreshnessColor = Brushes.LimeGreen;
		}
		else if (age.TotalSeconds < pollingInterval * 4)
		{
			DataFreshnessColor = Brushes.Gold;
		}
		else
		{
			DataFreshnessColor = Brushes.OrangeRed;
		}

		DataFreshnessTooltip = $"Last updated: {_lastUpdateTime:HH:mm:ss} ({age.TotalSeconds:F0}s ago)";
	}

	private void OnUpsRetryAttempt(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (_upsNetwork == null) return;
			RetryCount = _upsNetwork.RetryCount;
			ConnectionStatus = $"Reconnecting... ({RetryCount}/{_upsNetwork.MaxRetries})";
		});
	}

	private void OnUpsShutdownCondition(object? sender, EventArgs e)
	{
		Log.Warn("Shutdown condition triggered");

		Dispatcher.UIThread.Post(async () =>
		{
			var settings = App.Settings.Settings.Power;

			if (settings.ImmediateStopAction)
			{
				// Immediate shutdown without dialog
				PerformShutdown(settings.TypeOfStop);
				return;
			}

			// Show countdown dialog
			var shutdownWindow = new Views.ShutdownWindow(settings.DelayToShutdownSeconds);

			if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
			{
				await shutdownWindow.ShowDialog(lifetime.MainWindow!);

				if (shutdownWindow.ShutdownConfirmed)
				{
					PerformShutdown(settings.TypeOfStop);
				}
				else
				{
					Log.Info("Shutdown cancelled by user");
				}
			}
		});
	}

	private void PerformShutdown(ShutdownType shutdownType)
	{
		Log.Warn($"Performing system {shutdownType}");

		App.Notifications?.SendToast("WinNUT-Client", $"System {shutdownType} initiated");

		try
		{
			var command = shutdownType switch
			{
				ShutdownType.Shutdown => "shutdown /s /t 0",
				ShutdownType.Hibernate => "shutdown /h",
				ShutdownType.Suspend => "rundll32.exe powrprof.dll,SetSuspendState 0,1,0",
				_ => "shutdown /s /t 0"
			};

			Process.Start(new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = $"/c {command}",
				UseShellExecute = false,
				CreateNoWindow = true
			});
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to execute shutdown: {ex.Message}");
		}
	}

	private void OnUpsShutdownCancelled(object? sender, EventArgs e)
	{
		Log.Info("Shutdown cancelled - power restored");
	}

	private void ClearUpsData()
	{
		UpsManufacturer = string.Empty;
		UpsModel = string.Empty;
		UpsStatus = string.Empty;
		BatteryCharge = 0;
		BatteryVoltage = 0;
		BatteryRuntimeMinutes = 0;
		InputVoltage = 0;
		OutputVoltage = 0;
		InputFrequency = 0;
		Load = 0;
		OutputPower = 0;
	}
}
