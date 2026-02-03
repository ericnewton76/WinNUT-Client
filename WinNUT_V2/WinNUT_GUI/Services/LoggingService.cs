// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using NLog;

namespace WinNUT_Client.Services;

/// <summary>
/// Log levels for settings configuration.
/// </summary>
public enum LogLevel
{
	Notice = 0,
	Warning = 1,
	Error = 2,
	Debug = 3
}

/// <summary>
/// Helper for NLog operations. Configuration is via NLog.config file.
/// Use NLog.LogManager.GetCurrentClassLogger() in each class for logging.
/// </summary>
public static class LoggingSetup
{
	public static string LogFilePath
	{
		get
		{
			var logDirectory = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"WinNUT-Client");
			return Path.Combine(logDirectory, "WinNUT-Client.log");
		}
	}

	/// <summary>
	/// Updates the minimum log level at runtime.
	/// </summary>
	public static void SetLogLevel(LogLevel level)
	{
		var nlogLevel = level switch
		{
			LogLevel.Notice => NLog.LogLevel.Info,
			LogLevel.Warning => NLog.LogLevel.Warn,
			LogLevel.Error => NLog.LogLevel.Error,
			LogLevel.Debug => NLog.LogLevel.Debug,
			_ => NLog.LogLevel.Info
		};

		foreach (var rule in LogManager.Configuration.LoggingRules)
		{
			rule.SetLoggingLevels(nlogLevel, NLog.LogLevel.Fatal);
		}
		LogManager.ReconfigExistingLoggers();
	}

	/// <summary>
	/// Enables or disables file logging at runtime.
	/// </summary>
	public static void SetFileLoggingEnabled(bool enabled)
	{
		var fileTarget = LogManager.Configuration?.FindTargetByName("file");
		if (fileTarget != null)
		{
			foreach (var rule in LogManager.Configuration!.LoggingRules.ToList())
			{
				if (rule.Targets.Contains(fileTarget))
				{
					rule.SetLoggingLevels(
						enabled ? NLog.LogLevel.Trace : NLog.LogLevel.Off,
						enabled ? NLog.LogLevel.Fatal : NLog.LogLevel.Off);
				}
			}
			LogManager.ReconfigExistingLoggers();
		}
	}

	/// <summary>
	/// Shuts down NLog and flushes any pending log entries.
	/// </summary>
	public static void Shutdown()
	{
		LogManager.Shutdown();
	}
}
