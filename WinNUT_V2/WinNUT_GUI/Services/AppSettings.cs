// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Text.Json.Serialization;

namespace WinNUT_Client.Services;

/// <summary>
/// Application settings model. Serialized to/from JSON.
/// </summary>
public class AppSettings
{
	[JsonPropertyName("$schema")]
	public string Schema { get; set; } = "https://raw.githubusercontent.com/ericnewton76/WinNUT-Client/main/schemas/settings.schema.json";

	public int Version { get; set; } = 1;

	public ConnectionSettings Connection { get; set; } = new();
	public CalibrationSettings Calibration { get; set; } = new();
	public AppearanceSettings Appearance { get; set; } = new();
	public LoggingSettings Logging { get; set; } = new();
	public PowerSettings Power { get; set; } = new();
	public UpdateSettings Update { get; set; } = new();
}

public class ConnectionSettings
{
	public string ServerAddress { get; set; } = "localhost";
	public int Port { get; set; } = 3493;
	public string UpsName { get; set; } = "ups";
	public int PollingIntervalSeconds { get; set; } = 5;
	public string? Login { get; set; }

	/// <summary>
	/// Encrypted password. Use CryptographyService to encrypt/decrypt.
	/// </summary>
	public string? EncryptedPassword { get; set; }

	public bool AutoReconnect { get; set; } = false;
}

public class CalibrationSettings
{
	public int MinInputVoltage { get; set; } = 210;
	public int MaxInputVoltage { get; set; } = 270;

	public FrequencyType FrequencySupply { get; set; } = FrequencyType.Hz50;
	public int MinInputFrequency { get; set; } = 40;
	public int MaxInputFrequency { get; set; } = 60;

	public int MinOutputVoltage { get; set; } = 210;
	public int MaxOutputVoltage { get; set; } = 250;

	public int MinUpsLoad { get; set; } = 0;
	public int MaxUpsLoad { get; set; } = 100;

	public int MinBatteryVoltage { get; set; } = 6;
	public int MaxBatteryVoltage { get; set; } = 18;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrequencyType
{
	Hz50 = 0,
	Hz60 = 1
}

public class AppearanceSettings
{
	public bool MinimizeToTray { get; set; } = false;
	public bool MinimizeOnStart { get; set; } = false;
	public bool CloseToTray { get; set; } = false;
	public bool StartWithWindows { get; set; } = false;
}

public class LoggingSettings
{
	public bool EnableFileLogging { get; set; } = false;
	public LogLevel LogLevel { get; set; } = LogLevel.Notice;
}

public class PowerSettings
{
	public int ShutdownLimitBatteryCharge { get; set; } = 30;
	public int ShutdownLimitUpsRemainTimeSeconds { get; set; } = 120;
	public bool ImmediateStopAction { get; set; } = false;
	public bool FollowFsd { get; set; } = false;
	public ShutdownType TypeOfStop { get; set; } = ShutdownType.Shutdown;
	public int DelayToShutdownSeconds { get; set; } = 15;
	public bool AllowExtendedShutdownDelay { get; set; } = false;
	public int ExtendedShutdownDelaySeconds { get; set; } = 15;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShutdownType
{
	Shutdown = 0,
	Hibernate = 1,
	Suspend = 2
}

public class UpdateSettings
{
	public bool CheckForUpdates { get; set; } = false;
	public bool CheckForUpdatesAtStart { get; set; } = false;
	public UpdateCheckInterval CheckInterval { get; set; } = UpdateCheckInterval.Weekly;
	public UpdateBranch Branch { get; set; } = UpdateBranch.Stable;
	public DateTime? LastCheckDate { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateCheckInterval
{
	Daily = 0,
	Weekly = 1,
	Monthly = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateBranch
{
	Stable = 0,
	Development = 1
}
