using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

public partial class PreferencesViewModel : ViewModelBase
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	// Connection settings
	[ObservableProperty]
	private string _serverAddress = string.Empty;

	[ObservableProperty]
	private int _port = 3493;

	[ObservableProperty]
	private string _upsName = string.Empty;

	[ObservableProperty]
	private int _pollingInterval = 5;

	[ObservableProperty]
	private string _login = string.Empty;

	[ObservableProperty]
	private string _password = string.Empty;

	[ObservableProperty]
	private bool _autoReconnect;

	[ObservableProperty]
	private bool _autoConnectOnStartup;

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

		// Connection
		ServerAddress = settings.Connection.ServerAddress;
		Port = settings.Connection.Port;
		UpsName = settings.Connection.UpsName;
		PollingInterval = settings.Connection.PollingIntervalSeconds;
		Login = settings.Connection.Login ?? string.Empty;
		AutoReconnect = settings.Connection.AutoReconnect;
		AutoConnectOnStartup = settings.Connection.AutoConnectOnStartup;

		// Decrypt password
		if (!string.IsNullOrEmpty(settings.Connection.EncryptedPassword))
		{
			using var crypto = new CryptographyService();
			try
			{
				Password = crypto.Decrypt(settings.Connection.EncryptedPassword);
			}
			catch
			{
				Password = string.Empty;
			}
		}

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

		// Connection
		settings.Connection.ServerAddress = ServerAddress;
		settings.Connection.Port = Port;
		settings.Connection.UpsName = UpsName;
		settings.Connection.PollingIntervalSeconds = PollingInterval;
		settings.Connection.Login = string.IsNullOrEmpty(Login) ? null : Login;
		settings.Connection.AutoReconnect = AutoReconnect;
		settings.Connection.AutoConnectOnStartup = AutoConnectOnStartup;

		// Encrypt password
		if (!string.IsNullOrEmpty(Password))
		{
			using var crypto = new CryptographyService();
			settings.Connection.EncryptedPassword = crypto.Encrypt(Password);
		}
		else
		{
			settings.Connection.EncryptedPassword = null;
		}

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
	private void Cancel()
	{
		DialogResult = false;
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
