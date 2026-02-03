using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

/// <summary>
/// ViewModel for a single UPS device in the preferences list.
/// </summary>
public partial class UpsDeviceViewModel : ObservableObject
{
	public Guid Id { get; set; } = Guid.NewGuid();

	[ObservableProperty]
	private string _displayName = string.Empty;

	/// <summary>
	/// UPS address in NUT format: upsname@host:port (port defaults to 3493)
	/// </summary>
	[ObservableProperty]
	private string _address = string.Empty;

	[ObservableProperty]
	private int _pollingInterval = 5;

	[ObservableProperty]
	private string _login = string.Empty;

	[ObservableProperty]
	private string _password = string.Empty;

	[ObservableProperty]
	private bool _autoReconnect = true;

	[ObservableProperty]
	private bool _autoConnectOnStartup = true;

	[ObservableProperty]
	private bool _enabled = true;

	[ObservableProperty]
	private bool _isPrimary;

	public string HostDisplay => Address;
}

public partial class PreferencesViewModel : ViewModelBase
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	// UPS Devices
	[ObservableProperty]
	private ObservableCollection<UpsDeviceViewModel> _upsDevices = new();

	[ObservableProperty]
	private UpsDeviceViewModel? _selectedUpsDevice;

	// Multi-UPS shutdown mode
	[ObservableProperty]
	private bool _shutdownModeAny = true;

	[ObservableProperty]
	private bool _shutdownModePrimary;

	[ObservableProperty]
	private bool _shutdownModeAll;

	// Appearance settings
	[ObservableProperty]
	private bool _minimizeToTray;

	[ObservableProperty]
	private bool _minimizeOnStart;

	[ObservableProperty]
	private bool _closeToTray;

	[ObservableProperty]
	private bool _startWithWindows;

	// Logging settings
	[ObservableProperty]
	private bool _enableFileLogging;

	[ObservableProperty]
	private int _logLevelIndex;

	// Power settings
	[ObservableProperty]
	private int _shutdownBatteryLimit = 30;

	[ObservableProperty]
	private int _shutdownRuntimeLimit = 120;

	[ObservableProperty]
	private bool _immediateStopAction;

	[ObservableProperty]
	private bool _followFsd;

	[ObservableProperty]
	private int _shutdownTypeIndex;

	[ObservableProperty]
	private int _delayToShutdown = 15;

	// Calibration settings
	[ObservableProperty]
	private int _minInputVoltage = 210;

	[ObservableProperty]
	private int _maxInputVoltage = 270;

	[ObservableProperty]
	private int _frequencyIndex;

	[ObservableProperty]
	private int _minOutputVoltage = 210;

	[ObservableProperty]
	private int _maxOutputVoltage = 250;

	[ObservableProperty]
	private int _minLoad;

	[ObservableProperty]
	private int _maxLoad = 100;

	public bool? DialogResult { get; private set; }

	public event EventHandler? CloseRequested;

	public PreferencesViewModel()
	{
		LoadSettings();
	}

	private void LoadSettings()
	{
		var settings = App.Settings.Settings;

		// Load UPS Devices
		foreach (var config in settings.Connection.Devices)
		{
			var deviceVm = new UpsDeviceViewModel
			{
				Id = config.Id,
				DisplayName = config.DisplayName,
				Address = config.Address,
				PollingInterval = config.PollingIntervalSeconds,
				Login = config.Login ?? string.Empty,
				AutoReconnect = config.AutoReconnect,
				AutoConnectOnStartup = config.AutoConnectOnStartup,
				Enabled = config.Enabled,
				IsPrimary = config.Id == settings.Connection.PrimaryUpsId
			};

			// Decrypt password
			if (!string.IsNullOrEmpty(config.EncryptedPassword))
			{
				try
				{
					deviceVm.Password = App.Crypto.Decrypt(config.EncryptedPassword);
				}
				catch
				{
					deviceVm.Password = string.Empty;
				}
			}

			UpsDevices.Add(deviceVm);
		}

		// Select first device if any
		if (UpsDevices.Count > 0)
			SelectedUpsDevice = UpsDevices[0];

		// Multi-UPS shutdown mode
		ShutdownModeAny = settings.Connection.ShutdownMode == MultiUpsShutdownMode.AnyUpsCritical;
		ShutdownModePrimary = settings.Connection.ShutdownMode == MultiUpsShutdownMode.PrimaryOnly;
		ShutdownModeAll = settings.Connection.ShutdownMode == MultiUpsShutdownMode.AllUpsCritical;

		// Appearance
		MinimizeToTray = settings.Appearance.MinimizeToTray;
		MinimizeOnStart = settings.Appearance.MinimizeOnStart;
		CloseToTray = settings.Appearance.CloseToTray;
		StartWithWindows = IsStartupEnabled();

		// Logging
		EnableFileLogging = settings.Logging.EnableFileLogging;
		LogLevelIndex = (int)settings.Logging.LogLevel;

		// Power
		ShutdownBatteryLimit = settings.Power.ShutdownLimitBatteryCharge;
		ShutdownRuntimeLimit = settings.Power.ShutdownLimitUpsRemainTimeSeconds;
		ImmediateStopAction = settings.Power.ImmediateStopAction;
		FollowFsd = settings.Power.FollowFsd;
		ShutdownTypeIndex = (int)settings.Power.TypeOfStop;
		DelayToShutdown = settings.Power.DelayToShutdownSeconds;

		// Calibration
		MinInputVoltage = settings.Calibration.MinInputVoltage;
		MaxInputVoltage = settings.Calibration.MaxInputVoltage;
		FrequencyIndex = (int)settings.Calibration.FrequencySupply;
		MinOutputVoltage = settings.Calibration.MinOutputVoltage;
		MaxOutputVoltage = settings.Calibration.MaxOutputVoltage;
		MinLoad = settings.Calibration.MinUpsLoad;
		MaxLoad = settings.Calibration.MaxUpsLoad;
	}

	[RelayCommand]
	private void Save()
	{
		var settings = App.Settings.Settings;

		// Save all UPS devices
		settings.Connection.Devices.Clear();
		Guid? primaryId = null;

		foreach (var deviceVm in UpsDevices)
		{
			var config = new UpsConnectionConfig
			{
				Id = deviceVm.Id,
				DisplayName = deviceVm.DisplayName,
				Address = deviceVm.Address,
				PollingIntervalSeconds = deviceVm.PollingInterval,
				Login = string.IsNullOrEmpty(deviceVm.Login) ? null : deviceVm.Login,
				AutoReconnect = deviceVm.AutoReconnect,
				AutoConnectOnStartup = deviceVm.AutoConnectOnStartup,
				Enabled = deviceVm.Enabled
			};

			// Encrypt password
			if (!string.IsNullOrEmpty(deviceVm.Password))
			{
				config.EncryptedPassword = App.Crypto.Encrypt(deviceVm.Password);
			}

			settings.Connection.Devices.Add(config);

			if (deviceVm.IsPrimary)
				primaryId = deviceVm.Id;
		}

		settings.Connection.PrimaryUpsId = primaryId;

		// Multi-UPS shutdown mode
		if (ShutdownModeAny)
			settings.Connection.ShutdownMode = MultiUpsShutdownMode.AnyUpsCritical;
		else if (ShutdownModePrimary)
			settings.Connection.ShutdownMode = MultiUpsShutdownMode.PrimaryOnly;
		else if (ShutdownModeAll)
			settings.Connection.ShutdownMode = MultiUpsShutdownMode.AllUpsCritical;

		// Appearance
		settings.Appearance.MinimizeToTray = MinimizeToTray;
		settings.Appearance.MinimizeOnStart = MinimizeOnStart;
		settings.Appearance.CloseToTray = CloseToTray;
		settings.Appearance.StartWithWindows = StartWithWindows;

		// Logging
		settings.Logging.EnableFileLogging = EnableFileLogging;
		settings.Logging.LogLevel = (Services.LogLevel)LogLevelIndex;

		// Power
		settings.Power.ShutdownLimitBatteryCharge = ShutdownBatteryLimit;
		settings.Power.ShutdownLimitUpsRemainTimeSeconds = ShutdownRuntimeLimit;
		settings.Power.ImmediateStopAction = ImmediateStopAction;
		settings.Power.FollowFsd = FollowFsd;
		settings.Power.TypeOfStop = (ShutdownType)ShutdownTypeIndex;
		settings.Power.DelayToShutdownSeconds = DelayToShutdown;

		// Calibration
		settings.Calibration.MinInputVoltage = MinInputVoltage;
		settings.Calibration.MaxInputVoltage = MaxInputVoltage;
		settings.Calibration.FrequencySupply = (FrequencyType)FrequencyIndex;
		settings.Calibration.MinOutputVoltage = MinOutputVoltage;
		settings.Calibration.MaxOutputVoltage = MaxOutputVoltage;
		settings.Calibration.MinUpsLoad = MinLoad;
		settings.Calibration.MaxUpsLoad = MaxLoad;

		// Save to file
		App.Settings.Save();

		// Reload UPS manager with new settings
		App.UpsManager.LoadFromSettings(settings);

		// Update logging
		LoggingSetup.SetLogLevel(settings.Logging.LogLevel);
		LoggingSetup.SetFileLoggingEnabled(settings.Logging.EnableFileLogging);

		// Update Windows startup
		UpdateWindowsStartup(StartWithWindows);

		DialogResult = true;
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	private static void UpdateWindowsStartup(bool enable)
	{
		if (!OperatingSystem.IsWindows())
			return;

		try
		{
			const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
			const string valueName = "WinNUT-Client";

			using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, writable: true);
			if (key == null) return;

			if (enable)
			{
				var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
				key.SetValue(valueName, $"\"{exePath}\"");
			}
			else
			{
				key.DeleteValue(valueName, throwOnMissingValue: false);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to update Windows startup: {ex.Message}");
		}
	}

	private static bool IsStartupEnabled()
	{
		if (!OperatingSystem.IsWindows())
			return false;

		try
		{
			const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
			const string valueName = "WinNUT-Client";

			using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName);
			return key?.GetValue(valueName) != null;
		}
		catch
		{
			return false;
		}
	}

	[RelayCommand]
	private void AddUps()
	{
		var newDevice = new UpsDeviceViewModel
		{
			DisplayName = $"UPS {UpsDevices.Count + 1}",
			Address = "ups@localhost",
			PollingInterval = 5,
			AutoReconnect = true,
			AutoConnectOnStartup = true,
			Enabled = true
		};

		UpsDevices.Add(newDevice);
		SelectedUpsDevice = newDevice;

		// If this is the first device, make it primary
		if (UpsDevices.Count == 1)
		{
			newDevice.IsPrimary = true;
		}
	}

	[RelayCommand]
	private void RemoveUps()
	{
		if (SelectedUpsDevice == null) return;

		var wasPrimary = SelectedUpsDevice.IsPrimary;
		var index = UpsDevices.IndexOf(SelectedUpsDevice);
		UpsDevices.Remove(SelectedUpsDevice);

		// Select another device
		if (UpsDevices.Count > 0)
		{
			SelectedUpsDevice = UpsDevices[Math.Min(index, UpsDevices.Count - 1)];

			// Transfer primary to first device if removed was primary
			if (wasPrimary)
			{
				UpsDevices[0].IsPrimary = true;
			}
		}
		else
		{
			SelectedUpsDevice = null;
		}
	}

	[RelayCommand]
	private void SetPrimaryUps()
	{
		if (SelectedUpsDevice == null) return;

		// Clear all primary flags
		foreach (var device in UpsDevices)
		{
			device.IsPrimary = false;
		}

		// Set selected as primary
		SelectedUpsDevice.IsPrimary = true;
	}

	[RelayCommand]
	private void Cancel()
	{
		DialogResult = false;
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
