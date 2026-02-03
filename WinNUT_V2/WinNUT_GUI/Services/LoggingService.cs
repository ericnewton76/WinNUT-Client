// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using NLog;
using NLog.Config;
using NLog.Targets;

namespace WinNUT_Client.Services;

/// <summary>
/// Log levels matching the original WinNUT implementation.
/// </summary>
public enum LogLevel
{
    Notice = 0,
    Warning = 1,
    Error = 2,
    Debug = 3
}

/// <summary>
/// Provides logging services using NLog.
/// Logs to daily rotating files in the AppData folder.
/// </summary>
public class LoggingService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static string _logFilePath = string.Empty;
    
    public static string LogFilePath => _logFilePath;

    /// <summary>
    /// Initializes NLog configuration programmatically.
    /// </summary>
    /// <param name="enableFileLogging">Whether to write logs to file.</param>
    /// <param name="minLevel">Minimum log level to record.</param>
    public static void Initialize(bool enableFileLogging, LogLevel minLevel)
    {
        var config = new LoggingConfiguration();
        
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinNUT-Client");
        
        Directory.CreateDirectory(logDirectory);
        
        _logFilePath = Path.Combine(logDirectory, "WinNUT-Client.log");

        // File target with daily archiving
        if (enableFileLogging)
        {
            var fileTarget = new FileTarget("file")
            {
                FileName = _logFilePath,
                Layout = "${longdate} | ${level:uppercase=true:padding=-5} | ${logger} | ${message}${exception:format=tostring}",
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveFileName = Path.Combine(logDirectory, "WinNUT-Client.{#}.log"),
                ArchiveNumbering = ArchiveNumberingMode.Date,
                ArchiveDateFormat = "yyyy-MM-dd",
                MaxArchiveFiles = 30,
                ConcurrentWrites = true,
                KeepFileOpen = false
            };
            
            config.AddTarget(fileTarget);
            config.AddRule(MapLogLevel(minLevel), NLog.LogLevel.Fatal, fileTarget);
        }

        // Debug console target (only in debug builds)
#if DEBUG
        var debugTarget = new DebuggerTarget("debugger")
        {
            Layout = "${longdate} | ${level:uppercase=true} | ${logger} | ${message}"
        };
        config.AddTarget(debugTarget);
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, debugTarget);
#endif

        LogManager.Configuration = config;
    }

    /// <summary>
    /// Updates the minimum log level at runtime.
    /// </summary>
    public static void SetLogLevel(LogLevel level)
    {
        foreach (var rule in LogManager.Configuration.LoggingRules)
        {
            rule.SetLoggingLevels(MapLogLevel(level), NLog.LogLevel.Fatal);
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

    private static NLog.LogLevel MapLogLevel(LogLevel level) => level switch
    {
        LogLevel.Notice => NLog.LogLevel.Info,
        LogLevel.Warning => NLog.LogLevel.Warn,
        LogLevel.Error => NLog.LogLevel.Error,
        LogLevel.Debug => NLog.LogLevel.Debug,
        _ => NLog.LogLevel.Info
    };

    // Convenience methods for logging
    public static void Notice(string message) => Logger.Info(message);
    public static void Warning(string message) => Logger.Warn(message);
    public static void Error(string message) => Logger.Error(message);
    public static void Error(Exception ex, string message) => Logger.Error(ex, message);
    public static void Debug(string message) => Logger.Debug(message);

    /// <summary>
    /// Gets a logger for a specific type.
    /// </summary>
    public static Logger GetLogger<T>() => LogManager.GetLogger(typeof(T).Name);
    public static Logger GetLogger(string name) => LogManager.GetLogger(name);

    /// <summary>
    /// Shuts down NLog and flushes any pending log entries.
    /// </summary>
    public static void Shutdown()
    {
        LogManager.Shutdown();
    }
}
