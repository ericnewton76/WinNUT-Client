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

	public int Version { get; set; } = 2;

	public ConnectionSettings Connection { get; set; } = new();
	public CalibrationSettings Calibration { get; set; } = new();
	public AppearanceSettings Appearance { get; set; } = new();
	public LoggingSettings Logging { get; set; } = new();
	public PowerSettings Power { get; set; } = new();
	public UpdateSettings Update { get; set; } = new();
}

/// <summary>
/// Configuration for a single UPS connection.
/// </summary>
public class UpsConnectionConfig
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string DisplayName { get; set; } = string.Empty;
	
	/// <summary>
	/// UPS address in NUT standard format: upsname@host:port (port defaults to 3493)
	/// </summary>
	public string Address { get; set; } = string.Empty;
	
	public string? Login { get; set; }
	public string? EncryptedPassword { get; set; }
	public int PollingIntervalSeconds { get; set; } = 5;
	public bool AutoReconnect { get; set; } = true;
	public bool AutoConnectOnStartup { get; set; } = true;
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Parses the UPS name from the Address.
	/// </summary>
	[JsonIgnore]
	public string UpsName
	{
		get
		{
			var atIndex = Address.IndexOf('@');
			return atIndex > 0 ? Address[..atIndex] : string.Empty;
		}
	}

	/// <summary>
	/// Parses the host from the Address.
	/// </summary>
	[JsonIgnore]
	public string Host
	{
		get
		{
			var atIndex = Address.IndexOf('@');
			if (atIndex < 0) return string.Empty;
			
			var hostPart = Address[(atIndex + 1)..];
			var colonIndex = hostPart.LastIndexOf(':');
			
			// Check if there's a port (must be numeric after colon)
			if (colonIndex > 0 && int.TryParse(hostPart[(colonIndex + 1)..], out _))
			{
				return hostPart[..colonIndex];
			}
			return hostPart;
		}
	}

	/// <summary>
	/// Parses the port from the Address (defaults to 3493).
	/// </summary>
	[JsonIgnore]
	public int Port
	{
		get
		{
			var atIndex = Address.IndexOf('@');
			if (atIndex < 0) return 3493;
			
			var hostPart = Address[(atIndex + 1)..];
			var colonIndex = hostPart.LastIndexOf(':');
			
			if (colonIndex > 0 && int.TryParse(hostPart[(colonIndex + 1)..], out var port))
			{
				return port;
			}
			return 3493;
		}
	}

	/// <summary>
	/// Creates a display-friendly name for this UPS.
	/// </summary>
	[JsonIgnore]
	public string EffectiveDisplayName => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Address;

	/// <summary>
	/// Creates an Address from components.
	/// </summary>
	public static string FormatAddress(string upsName, string host, int port = 3493)
	{
		return port == 3493 ? $"{upsName}@{host}" : $"{upsName}@{host}:{port}";
	}
}

/// <summary>
/// Determines how multiple UPS devices affect shutdown behavior.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MultiUpsShutdownMode
{
	/// <summary>Shutdown if any UPS reaches critical state.</summary>
	AnyUpsCritical = 0,
	/// <summary>Only the primary UPS triggers shutdown.</summary>
	PrimaryOnly = 1,
	/// <summary>Shutdown only when all UPS devices are critical.</summary>
	AllUpsCritical = 2
}

public class ConnectionSettings
{
	// Legacy single-UPS settings (for migration from v1)
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ServerAddress { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Port { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? UpsName { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? PollingIntervalSeconds { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Login { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? EncryptedPassword { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? AutoReconnect { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? AutoConnectOnStartup { get; set; }

	// Multi-UPS settings
	public List<UpsConnectionConfig> Devices { get; set; } = new();
	public MultiUpsShutdownMode ShutdownMode { get; set; } = MultiUpsShutdownMode.AnyUpsCritical;
	public Guid? PrimaryUpsId { get; set; }
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
