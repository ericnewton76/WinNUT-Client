// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Globalization;
using System.Net.Sockets;
using NLog;

namespace WinNUT_Client.Services;

/// <summary>
/// Represents a UPS variable with its key, value, and description.
/// </summary>
public record UpsVariable(string Key, string Value, string? Description);

/// <summary>
/// Contains current UPS data readings.
/// </summary>
public class UpsData
{
	public string Manufacturer { get; set; } = "Unknown";
	public string Model { get; set; } = "Unknown";
	public string Serial { get; set; } = "Unknown";
	public string Firmware { get; set; } = "Unknown";

	public double BatteryCharge { get; set; }
	public double BatteryVoltage { get; set; }
	public double BatteryRuntimeSeconds { get; set; }
	public double BatteryCapacity { get; set; }

	public double InputFrequency { get; set; }
	public double InputVoltage { get; set; }
	public double OutputVoltage { get; set; }
	public double Load { get; set; }
	public double OutputPower { get; set; }
	public double InputCurrent { get; set; }

	public string Status { get; set; } = string.Empty;
	public bool IsOnline => Status.Contains("OL");
	public bool IsOnBattery => Status.Contains("OB");
	public bool IsForcedShutdown => Status.Contains("FSD");
	public bool IsLowBattery => Status.Contains("LB");
	public bool IsCharging => Status.Contains("CHRG");
	public bool IsDischarging => Status.Contains("DISCHRG");
}

/// <summary>
/// NUT protocol response codes.
/// </summary>
public enum NutResponse
{
	Ok,
	Var,
	AccessDenied,
	UnknownUps,
	VarNotSupported,
	CmdNotSupported,
	InvalidArgument,
	InstCmdFailed,
	SetFailed,
	ReadOnly,
	TooLong,
	FeatureNotSupported,
	FeatureNotConfigured,
	AlreadySslMode,
	DriverNotConnected,
	DataStale,
	AlreadyLoggedIn,
	InvalidPassword,
	AlreadySetPassword,
	InvalidUsername,
	AlreadySetUsername,
	UsernameRequired,
	PasswordRequired,
	UnknownCommand,
	InvalidValue
}

/// <summary>
/// Service for communicating with a NUT (Network UPS Tools) server.
/// </summary>
public class UpsNetworkService : IDisposable
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();
	private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
	private const double CosPhi = 0.6;

	private TcpClient? _tcpClient;
	private NetworkStream? _networkStream;
	private StreamReader? _reader;
	private StreamWriter? _writer;

	private System.Timers.Timer? _pollingTimer;
	private System.Timers.Timer? _reconnectTimer;

	private bool _disposed;
	private bool _isReconnecting;
	private int _retryCount;
	private double _frequencyFallback;
	
	// Semaphore to synchronize access to the network stream
	private readonly SemaphoreSlim _commandLock = new(1, 1);

	// Connection settings
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; } = 3493;
	public string UpsName { get; set; } = string.Empty;
	public int PollingIntervalMs { get; set; } = 5000;
	public string? Login { get; set; }
	public string? Password { get; set; }
	public bool AutoReconnect { get; set; }
	public int MaxRetries { get; set; } = 30;

	// Power thresholds
	public int BatteryLimitPercent { get; set; } = 30;
	public int BackupLimitSeconds { get; set; } = 120;
	public bool FollowFsd { get; set; }
	public int DefaultFrequencyHz { get; set; } = 50;

	// Current state
	public bool IsConnected { get; private set; }
	public UpsData CurrentData { get; } = new();
	public int RetryCount => _retryCount;

	// Events
	public event EventHandler? Connected;
	public event EventHandler? Disconnected;
	public event EventHandler? ConnectionLost;
	public event EventHandler? DataUpdated;
	public event EventHandler? RetryAttempt;
#pragma warning disable CS0067 // Event is never used (reserved for future use)
	public event EventHandler? UnknownUps;
	public event EventHandler? InvalidCredentials;
#pragma warning restore CS0067
	public event EventHandler? ShutdownCondition;
	public event EventHandler? ShutdownCancelled;

	private bool _shutdownInProgress;

	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(Host) || Port == 0 || string.IsNullOrEmpty(UpsName))
		{
			throw new InvalidOperationException("Host, Port, and UpsName must be configured before connecting.");
		}

		try
		{
			Log.Info("Connecting to NUT server at {Host}:{Port}, UPS: {UpsName}", Host, Port, UpsName);

			_tcpClient = new TcpClient();
			await _tcpClient.ConnectAsync(Host, Port, cancellationToken);

			_networkStream = _tcpClient.GetStream();
			_reader = new StreamReader(_networkStream);
			_writer = new StreamWriter(_networkStream) { AutoFlush = true };

			// Authenticate if credentials provided
			if (!string.IsNullOrEmpty(Login) && !string.IsNullOrEmpty(Password))
			{
				if (!await AuthenticateAsync(cancellationToken))
				{
					throw new UnauthorizedAccessException("Authentication failed.");
				}
			}

			IsConnected = true;

			// Get UPS product info (with lock)
			await _commandLock.WaitAsync(cancellationToken);
			try
			{
				await GetUpsProductInfoAsync(cancellationToken);
			}
			finally
			{
				_commandLock.Release();
			}

			// Start polling timer
			StartPollingTimer();

			_isReconnecting = false;
			_retryCount = 0;

			Log.Info("Connected to NUT server at {Host}:{Port}, UPS: {UpsName}", Host, Port, UpsName);
			Connected?.Invoke(this, EventArgs.Empty);

			// Get initial data (locks internally)
			await RetrieveUpsDataAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to connect to NUT server");
			await HandleConnectionErrorAsync(ex);
			throw;
		}
	}

	private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
	{
		try
		{
			Log.Debug("Authenticating with username: {Login}", Login);

			// Send username
			await SendCommandAsync($"USERNAME {Login}", cancellationToken);
			var response = await ReadResponseAsync(cancellationToken);
			var result = ParseResponse(response);

			if (result != NutResponse.Ok)
			{
				if (result == NutResponse.InvalidUsername)
				{
					InvalidCredentials?.Invoke(this, EventArgs.Empty);
				}
				Log.Error("Username authentication failed: {Response}", response);
				return false;
			}

			// Send password
			await SendCommandAsync($"PASSWORD {Password}", cancellationToken);
			response = await ReadResponseAsync(cancellationToken);
			result = ParseResponse(response);

			if (result != NutResponse.Ok)
			{
				if (result == NutResponse.InvalidPassword)
				{
					InvalidCredentials?.Invoke(this, EventArgs.Empty);
				}
				Log.Error("Password authentication failed: {Response}", response);
				return false;
			}

			Log.Debug("Authentication successful");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Authentication error");
			return false;
		}
	}

	public async Task DisconnectAsync(bool force = false)
	{
		Log.Debug("Disconnecting from NUT server (force={Force})", force);

		StopTimers();

		IsConnected = false;

		try
		{
			_writer?.Close();
			_reader?.Close();
			_networkStream?.Close();
			_tcpClient?.Close();
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Error during disconnect cleanup");
		}

		_writer = null;
		_reader = null;
		_networkStream = null;
		_tcpClient = null;

		// Reset UPS info
		CurrentData.Manufacturer = "Unknown";
		CurrentData.Model = "Unknown";
		CurrentData.Serial = "Unknown";
		CurrentData.Firmware = "Unknown";

		if (!force && !_isReconnecting)
		{
			Disconnected?.Invoke(this, EventArgs.Empty);
		}

		Log.Info("Disconnected from NUT server");
	}

	public async Task<string?> GetVariableAsync(string variableName, string? fallback = null, CancellationToken cancellationToken = default)
	{
		if (!IsConnected || _writer == null || _reader == null)
		{
			return fallback;
		}

		await _commandLock.WaitAsync(cancellationToken);
		try
		{
			return await GetVariableInternalAsync(variableName, fallback, cancellationToken);
		}
		finally
		{
			_commandLock.Release();
		}
	}

	// Internal version without lock - caller must hold _commandLock
	private async Task<string?> GetVariableInternalAsync(string variableName, string? fallback, CancellationToken cancellationToken)
	{
		if (_writer == null || _reader == null)
			return fallback;

		try
		{
			await SendCommandAsync($"GET VAR {UpsName} {variableName}", cancellationToken);
			var response = await ReadResponseAsync(cancellationToken);
			var result = ParseResponse(response);

			return result switch
			{
				NutResponse.Ok => ExtractValue(response),
				NutResponse.VarNotSupported when fallback != null => fallback,
				NutResponse.UnknownUps => throw new InvalidOperationException($"Unknown UPS: {UpsName}"),
				NutResponse.DataStale => throw new InvalidOperationException($"Data stale for {variableName}"),
				_ => fallback
			};
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error getting variable {Variable}", variableName);
			return fallback;
		}
	}

	public async Task<List<UpsVariable>> ListVariablesAsync(CancellationToken cancellationToken = default)
	{
		var variables = new List<UpsVariable>();

		if (!IsConnected || _writer == null || _reader == null)
		{
			return variables;
		}

		await _commandLock.WaitAsync(cancellationToken);
		try
		{
			await SendCommandAsync($"LIST VAR {UpsName}", cancellationToken);

			// Read all lines until END LIST
			var timeout = DateTime.Now.AddSeconds(30);
			while (DateTime.Now < timeout)
			{
				var line = await _reader.ReadLineAsync(cancellationToken);
				if (line == null || line.Contains("END LIST"))
					break;

				if (line.StartsWith("VAR"))
				{
					var parts = line.Split('"');
					if (parts.Length >= 2)
					{
						var keyParts = parts[0].Split(' ');
						if (keyParts.Length >= 3)
						{
							var key = keyParts[2];
							var value = parts[1];
							// Skip description lookup during list to avoid lock nesting
							variables.Add(new UpsVariable(key, value, null));
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error listing variables");
		}
		finally
		{
			_commandLock.Release();
		}

		return variables;
	}

	public async Task<Dictionary<string, string>> GetAllVariablesAsync(CancellationToken cancellationToken = default)
	{
		var vars = await ListVariablesAsync(cancellationToken);
		return vars.ToDictionary(v => v.Key, v => v.Value);
	}

	private async Task GetUpsProductInfoAsync(CancellationToken cancellationToken)
	{
		// Caller must hold _commandLock
		CurrentData.Manufacturer = await GetVariableInternalAsync("ups.mfr", "Unknown", cancellationToken) ?? "Unknown";
		CurrentData.Model = await GetVariableInternalAsync("ups.model", "Unknown", cancellationToken) ?? "Unknown";
		CurrentData.Serial = await GetVariableInternalAsync("ups.serial", "Unknown", cancellationToken) ?? "Unknown";
		CurrentData.Firmware = await GetVariableInternalAsync("ups.firmware", "Unknown", cancellationToken) ?? "Unknown";
	}

	private async Task RetrieveUpsDataAsync(CancellationToken cancellationToken = default)
	{
		if (!IsConnected || _isReconnecting)
			return;

		await _commandLock.WaitAsync(cancellationToken);
		try
		{
			Log.Debug("Retrieving UPS data");

			// Get frequency fallback if not set
			if (_frequencyFallback == 0)
			{
				var nominalFreq = await GetVariableInternalAsync("output.frequency.nominal", DefaultFrequencyHz.ToString(), cancellationToken);
				_frequencyFallback = ParseDouble(nominalFreq, DefaultFrequencyHz);
			}

			// Retrieve all UPS variables
			CurrentData.BatteryCharge = ParseDouble(await GetVariableInternalAsync("battery.charge", "255", cancellationToken), 255);
			CurrentData.BatteryVoltage = ParseDouble(await GetVariableInternalAsync("battery.voltage", "12", cancellationToken), 12);
			CurrentData.BatteryRuntimeSeconds = ParseDouble(await GetVariableInternalAsync("battery.runtime", "86400", cancellationToken), 86400);
			CurrentData.BatteryCapacity = ParseDouble(await GetVariableInternalAsync("battery.capacity", "7", cancellationToken), 7);

			var inputFreq = await GetVariableInternalAsync("input.frequency", null, cancellationToken);
			if (inputFreq == null)
			{
				var outputFreq = await GetVariableInternalAsync("output.frequency", _frequencyFallback.ToString(InvariantCulture), cancellationToken);
				CurrentData.InputFrequency = ParseDouble(outputFreq, _frequencyFallback);
			}
			else
			{
				CurrentData.InputFrequency = ParseDouble(inputFreq, _frequencyFallback);
			}

			CurrentData.InputVoltage = ParseDouble(await GetVariableInternalAsync("input.voltage", "220", cancellationToken), 220);
			CurrentData.OutputVoltage = ParseDouble(await GetVariableInternalAsync("output.voltage", CurrentData.InputVoltage.ToString(InvariantCulture), cancellationToken), CurrentData.InputVoltage);
			CurrentData.Load = ParseDouble(await GetVariableInternalAsync("ups.load", "100", cancellationToken), 100);
			CurrentData.Status = await GetVariableInternalAsync("ups.status", "OL", cancellationToken) ?? "OL";

			// Calculate output power
			var nominalPower = ParseDouble(await GetVariableInternalAsync("ups.realpower.nominal", "0", cancellationToken), 0);
			if (nominalPower == 0)
			{
				CurrentData.InputCurrent = ParseDouble(await GetVariableInternalAsync("ups.current.nominal", "1", cancellationToken), 1);
				CurrentData.OutputPower = Math.Round(CurrentData.InputVoltage * 0.95 * CurrentData.InputCurrent * CosPhi);
			}
			else
			{
				CurrentData.OutputPower = Math.Round(nominalPower * (CurrentData.Load / 100));
			}

			// Calculate battery charge if not reported
			if (CurrentData.BatteryCharge >= 255)
			{
				var nBatt = Math.Floor(CurrentData.BatteryVoltage / 12);
				CurrentData.BatteryCharge = Math.Floor((CurrentData.BatteryVoltage - (11.6 * nBatt)) / (0.02 * nBatt));
			}

			// Calculate runtime if not reported
			if (CurrentData.BatteryRuntimeSeconds >= 86400)
			{
				var load = CurrentData.Load != 0 ? CurrentData.Load : 0.1;
				var powerDivider = load switch
				{
					>= 76 => 0.4,
					>= 51 => 0.3,
					_ => 0.5
				};
				var battInstantCurrent = (CurrentData.OutputVoltage * load) / (CurrentData.BatteryVoltage * 100);
				CurrentData.BatteryRuntimeSeconds = Math.Floor(CurrentData.BatteryCapacity * 0.6 * CurrentData.BatteryCharge * (1 - powerDivider) * 3600 / (battInstantCurrent * 100));
			}

			// Process status flags
			ProcessUpsStatus();

			DataUpdated?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error retrieving UPS data");
			await HandleConnectionErrorAsync(ex);
		}
		finally
		{
			_commandLock.Release();
		}
	}

	private void ProcessUpsStatus()
	{
		var statusParts = CurrentData.Status.Trim().Split(' ');

		foreach (var state in statusParts)
		{
			switch (state)
			{
				case "OL":
					// On line - cancel any pending shutdown
					if (_shutdownInProgress)
					{
						Log.Info("Power restored - cancelling shutdown");
						_shutdownInProgress = false;
						ShutdownCancelled?.Invoke(this, EventArgs.Empty);
					}
					break;

				case "OB":
					// On battery - check thresholds
					if (!_shutdownInProgress &&
						(CurrentData.BatteryCharge <= BatteryLimitPercent ||
						 CurrentData.BatteryRuntimeSeconds <= BackupLimitSeconds))
					{
						Log.Warn("Shutdown condition reached: Battery={Charge}%, Runtime={Runtime}s",
							CurrentData.BatteryCharge, CurrentData.BatteryRuntimeSeconds);
						_shutdownInProgress = true;
						ShutdownCondition?.Invoke(this, EventArgs.Empty);
					}
					break;

				case "FSD":
					// Forced shutdown from server
					if (FollowFsd && !_shutdownInProgress)
					{
						Log.Warn("Forced shutdown commanded by NUT server");
						_shutdownInProgress = true;
						ShutdownCondition?.Invoke(this, EventArgs.Empty);
					}
					break;

				case "LB":
					Log.Info("UPS reports low battery");
					break;
				case "HB":
					Log.Info("UPS reports high battery");
					break;
				case "CHRG":
					Log.Debug("Battery is charging");
					break;
				case "DISCHRG":
					Log.Debug("Battery is discharging");
					break;
				case "BYPASS":
					Log.Warn("UPS bypass circuit is active - no battery protection");
					break;
				case "CAL":
					Log.Info("UPS is performing runtime calibration");
					break;
				case "OFF":
					Log.Warn("UPS is offline and not supplying power");
					break;
				case "OVER":
					Log.Warn("UPS is overloaded");
					break;
				case "TRIM":
					Log.Debug("UPS is trimming incoming voltage");
					break;
				case "BOOST":
					Log.Debug("UPS is boosting incoming voltage");
					break;
			}
		}
	}

	private async Task HandleConnectionErrorAsync(Exception ex)
	{
		await DisconnectAsync(true);

		if (AutoReconnect && _retryCount < MaxRetries)
		{
			_isReconnecting = true;
			ConnectionLost?.Invoke(this, EventArgs.Empty);
			StartReconnectTimer();
		}
		else
		{
			Disconnected?.Invoke(this, EventArgs.Empty);
		}
	}

	private void StartPollingTimer()
	{
		StopTimers();

		_pollingTimer = new System.Timers.Timer(PollingIntervalMs);
		_pollingTimer.Elapsed += async (s, e) => await RetrieveUpsDataAsync();
		_pollingTimer.AutoReset = true;
		_pollingTimer.Start();
	}

	private void StartReconnectTimer()
	{
		_reconnectTimer?.Stop();
		_reconnectTimer = new System.Timers.Timer(30000); // 30 seconds
		_reconnectTimer.Elapsed += async (s, e) => await AttemptReconnectAsync();
		_reconnectTimer.AutoReset = false;
		_reconnectTimer.Start();
	}

	private async Task AttemptReconnectAsync()
	{
		_retryCount++;

		if (_retryCount > MaxRetries)
		{
			Log.Error("Max reconnection attempts reached");
			_isReconnecting = false;
			Disconnected?.Invoke(this, EventArgs.Empty);
			return;
		}

		Log.Info("Reconnection attempt {Retry}/{Max}", _retryCount, MaxRetries);
		RetryAttempt?.Invoke(this, EventArgs.Empty);

		try
		{
			await ConnectAsync();
		}
		catch
		{
			if (_retryCount < MaxRetries)
			{
				StartReconnectTimer();
			}
		}
	}

	private void StopTimers()
	{
		_pollingTimer?.Stop();
		_pollingTimer?.Dispose();
		_pollingTimer = null;

		_reconnectTimer?.Stop();
		_reconnectTimer?.Dispose();
		_reconnectTimer = null;
	}

	private async Task SendCommandAsync(string command, CancellationToken cancellationToken)
	{
		if (_writer == null)
			throw new InvalidOperationException("Not connected");

		await _writer.WriteLineAsync(command.AsMemory(), cancellationToken);
	}

	private async Task<string> ReadResponseAsync(CancellationToken cancellationToken)
	{
		if (_reader == null)
			throw new InvalidOperationException("Not connected");

		return await _reader.ReadLineAsync(cancellationToken) ?? string.Empty;
	}

	private static NutResponse ParseResponse(string response)
	{
		var sanitized = response.Replace("-", string.Empty);
		var parts = sanitized.Split(' ');

		return parts[0] switch
		{
			"OK" or "VAR" or "BEGIN" or "DESC" => NutResponse.Ok,
			"ERR" when parts.Length > 1 && Enum.TryParse<NutResponse>(parts[1], true, out var result) => result,
			_ => throw new InvalidOperationException($"Unknown NUT response: {response}")
		};
	}

	private static string? ExtractValue(string response)
	{
		var parts = response.Split('"');
		return parts.Length >= 2 ? parts[1].Trim() : null;
	}

	private static double ParseDouble(string? value, double fallback)
	{
		if (string.IsNullOrEmpty(value))
			return fallback;
		return double.TryParse(value, NumberStyles.Any, InvariantCulture, out var result) ? result : fallback;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		StopTimers();

		_writer?.Dispose();
		_reader?.Dispose();
		_networkStream?.Dispose();
		_tcpClient?.Dispose();
		_commandLock.Dispose();

		GC.SuppressFinalize(this);
	}
}
