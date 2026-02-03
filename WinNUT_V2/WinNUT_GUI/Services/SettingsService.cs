// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Text.Json;
using NLog;

namespace WinNUT_Client.Services;

/// <summary>
/// Manages application settings persistence using JSON files.
/// </summary>
public class SettingsService
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly string _settingsFilePath;
	private AppSettings _settings;

	public AppSettings Settings => _settings;
	public string SettingsFilePath => _settingsFilePath;

	/// <summary>
	/// Creates a new SettingsService with the default settings file location.
	/// Default: %APPDATA%/WinNUT-Client/settings.json
	/// </summary>
	public SettingsService() : this(GetDefaultSettingsPath())
	{
	}

	/// <summary>
	/// Creates a new SettingsService with a custom settings file path.
	/// </summary>
	/// <param name="settingsFilePath">Full path to the settings JSON file.</param>
	public SettingsService(string settingsFilePath)
	{
		_settingsFilePath = settingsFilePath;
		_settings = new AppSettings();
	}

	/// <summary>
	/// Gets the default settings file path.
	/// </summary>
	public static string GetDefaultSettingsPath()
	{
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return Path.Combine(appDataPath, "WinNUT-Client", "settings.json");
	}

	/// <summary>
	/// Loads settings from the JSON file. Creates default settings if file doesn't exist.
	/// </summary>
	public async Task<AppSettings> LoadAsync()
	{
		try
		{
			if (File.Exists(_settingsFilePath))
			{
				var json = await File.ReadAllTextAsync(_settingsFilePath);
				_settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
				Log.Info($"Settings loaded from {_settingsFilePath}");
			}
			else
			{
				_settings = new AppSettings();
				await SaveAsync(); // Create default settings file
				Log.Info($"Created default settings at {_settingsFilePath}");
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to load settings, using defaults");
			_settings = new AppSettings();
		}

		return _settings;
	}

	/// <summary>
	/// Loads settings synchronously. Use LoadAsync when possible.
	/// </summary>
	public AppSettings Load()
	{
		try
		{
			if (File.Exists(_settingsFilePath))
			{
				var json = File.ReadAllText(_settingsFilePath);
				_settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
			}
			else
			{
				_settings = new AppSettings();
				Save();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to load settings, using defaults");
			_settings = new AppSettings();
		}

		return _settings;
	}

	/// <summary>
	/// Saves current settings to the JSON file.
	/// </summary>
	public async Task SaveAsync()
	{
		try
		{
			var directory = Path.GetDirectoryName(_settingsFilePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var json = JsonSerializer.Serialize(_settings, JsonOptions);
			await File.WriteAllTextAsync(_settingsFilePath, json);
			Log.Debug($"Settings saved to {_settingsFilePath}");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save settings");
			throw;
		}
	}

	/// <summary>
	/// Saves settings synchronously. Use SaveAsync when possible.
	/// </summary>
	public void Save()
	{
		try
		{
			var directory = Path.GetDirectoryName(_settingsFilePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var json = JsonSerializer.Serialize(_settings, JsonOptions);
			File.WriteAllText(_settingsFilePath, json);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save settings");
			throw;
		}
	}

	/// <summary>
	/// Imports settings from a legacy INI file (WinNUT 1.x format).
	/// </summary>
	public async Task<bool> ImportFromIniAsync(string iniFilePath)
	{
		try
		{
			var parser = new IniFileParser();
			var iniData = await parser.LoadAsync(iniFilePath);

			MapIniToSettings(iniData);
			await SaveAsync();

			Log.Info($"Imported settings from legacy INI: {iniFilePath}");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(ex, $"Failed to import INI file: {iniFilePath}");
			return false;
		}
	}

	private void MapIniToSettings(IniFileParser.IniData iniData)
	{
		// Connection settings
		if (iniData.TryGetValue("Server", "Server address", out var serverAddress))
			_settings.Connection.ServerAddress = serverAddress;
		if (iniData.TryGetInt("Server", "Port", out var port))
			_settings.Connection.Port = port;
		if (iniData.TryGetValue("Server", "UPS name", out var upsName))
			_settings.Connection.UpsName = upsName;
		if (iniData.TryGetInt("Server", "Delay", out var delay))
			_settings.Connection.PollingIntervalSeconds = delay;
		if (iniData.TryGetBool("Server", "AutoReconnect", out var autoReconnect))
			_settings.Connection.AutoReconnect = autoReconnect;

		// Calibration settings
		if (iniData.TryGetInt("Calibration", "Min Input Voltage", out var minInputV))
			_settings.Calibration.MinInputVoltage = minInputV;
		if (iniData.TryGetInt("Calibration", "Max Input Voltage", out var maxInputV))
			_settings.Calibration.MaxInputVoltage = maxInputV;
		if (iniData.TryGetInt("Calibration", "Frequency Supply", out var freq))
			_settings.Calibration.FrequencySupply = freq == 50 ? FrequencyType.Hz50 : FrequencyType.Hz60;
		if (iniData.TryGetInt("Calibration", "Min Input Frequency", out var minFreq))
			_settings.Calibration.MinInputFrequency = minFreq;
		if (iniData.TryGetInt("Calibration", "Max Input Frequency", out var maxFreq))
			_settings.Calibration.MaxInputFrequency = maxFreq;
		if (iniData.TryGetInt("Calibration", "Min Output Voltage", out var minOutputV))
			_settings.Calibration.MinOutputVoltage = minOutputV;
		if (iniData.TryGetInt("Calibration", "Max Output Voltage", out var maxOutputV))
			_settings.Calibration.MaxOutputVoltage = maxOutputV;
		if (iniData.TryGetInt("Calibration", "Min UPS Load", out var minLoad))
			_settings.Calibration.MinUpsLoad = minLoad;
		if (iniData.TryGetInt("Calibration", "Max UPS Load", out var maxLoad))
			_settings.Calibration.MaxUpsLoad = maxLoad;
		if (iniData.TryGetInt("Calibration", "Min Batt Voltage", out var minBattV))
			_settings.Calibration.MinBatteryVoltage = minBattV;
		if (iniData.TryGetInt("Calibration", "Max Batt Voltage", out var maxBattV))
			_settings.Calibration.MaxBatteryVoltage = maxBattV;

		// Appearance settings
		if (iniData.TryGetBool("Misc", "Minimize to tray", out var minToTray))
			_settings.Appearance.MinimizeToTray = minToTray;
		if (iniData.TryGetBool("Misc", "Minimize on Start", out var minOnStart))
			_settings.Appearance.MinimizeOnStart = minOnStart;
		if (iniData.TryGetBool("Misc", "Close to tray", out var closeToTray))
			_settings.Appearance.CloseToTray = closeToTray;
		if (iniData.TryGetBool("Misc", "Start with Windows", out var startWithWin))
			_settings.Appearance.StartWithWindows = startWithWin;

		// Logging settings
		if (iniData.TryGetBool("Logging", "Use Log File", out var useLogFile))
			_settings.Logging.EnableFileLogging = useLogFile;
		if (iniData.TryGetInt("Logging", "Log Level", out var logLevel))
			_settings.Logging.LogLevel = logLevel switch
			{
				1 => LogLevel.Notice,
				2 => LogLevel.Warning,
				4 => LogLevel.Error,
				8 => LogLevel.Debug,
				_ => LogLevel.Notice
			};

		// Power settings
		if (iniData.TryGetInt("Power", "Shutdown Limit Battery Charge", out var shutdownBatt))
			_settings.Power.ShutdownLimitBatteryCharge = shutdownBatt;
		if (iniData.TryGetInt("Power", "Shutdown Limit UPS Remain Time", out var shutdownTime))
			_settings.Power.ShutdownLimitUpsRemainTimeSeconds = shutdownTime;
		if (iniData.TryGetBool("Power", "Immediate stop action", out var immediateStop))
			_settings.Power.ImmediateStopAction = immediateStop;
		if (iniData.TryGetInt("Power", "Type Of Stop", out var typeOfStop))
			_settings.Power.TypeOfStop = typeOfStop switch
			{
				17 => ShutdownType.Shutdown,
				32 => ShutdownType.Hibernate,
				64 => ShutdownType.Suspend,
				_ => ShutdownType.Shutdown
			};
		if (iniData.TryGetInt("Power", "Delay To Shutdown", out var delayShutdown))
			_settings.Power.DelayToShutdownSeconds = delayShutdown;
		if (iniData.TryGetBool("Power", "Allow Extended Shutdown Delay", out var allowExtended))
			_settings.Power.AllowExtendedShutdownDelay = allowExtended;
		if (iniData.TryGetInt("Power", "Extended Shutdown Delay", out var extendedDelay))
			_settings.Power.ExtendedShutdownDelaySeconds = extendedDelay;

		// Update settings
		if (iniData.TryGetBool("Update", "Verify Update", out var checkUpdates))
			_settings.Update.CheckForUpdates = checkUpdates;
		if (iniData.TryGetBool("Update", "Verify Update At Start", out var checkAtStart))
			_settings.Update.CheckForUpdatesAtStart = checkAtStart;
		if (iniData.TryGetInt("Update", "Delay Between Each Verification", out var checkInterval))
			_settings.Update.CheckInterval = (checkInterval - 1) switch
			{
				0 => UpdateCheckInterval.Daily,
				1 => UpdateCheckInterval.Weekly,
				2 => UpdateCheckInterval.Monthly,
				_ => UpdateCheckInterval.Weekly
			};
		if (iniData.TryGetInt("Update", "Stable Or Dev Branch", out var branch))
			_settings.Update.Branch = (branch - 1) == 0 ? UpdateBranch.Stable : UpdateBranch.Development;
	}

	/// <summary>
	/// Parses command line arguments for settings file path.
	/// Supports: --config "path/to/settings.json" or -c "path/to/settings.json"
	/// </summary>
	public static string? ParseConfigPathFromArgs(string[] args)
	{
		for (int i = 0; i < args.Length - 1; i++)
		{
			if (args[i] == "--config" || args[i] == "-c")
			{
				return args[i + 1];
			}
		}
		return null;
	}
}
