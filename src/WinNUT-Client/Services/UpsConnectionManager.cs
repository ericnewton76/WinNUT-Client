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
/// Event args for UPS-specific events.
/// </summary>
public class UpsEventArgs : EventArgs
{
	public Guid UpsId { get; }
	public UpsConnectionConfig Config { get; }
	public UpsNetworkService Service { get; }

	public UpsEventArgs(Guid upsId, UpsConnectionConfig config, UpsNetworkService service)
	{
		UpsId = upsId;
		Config = config;
		Service = service;
	}
}

/// <summary>
/// Manages multiple UPS connections.
/// </summary>
public class UpsConnectionManager : IDisposable
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	private readonly Dictionary<Guid, UpsNetworkService> _connections = new();
	private readonly Dictionary<Guid, UpsConnectionConfig> _configs = new();
	private readonly CryptographyService _cryptoService;
	private readonly object _lock = new();
	private bool _disposed;

	public UpsConnectionManager(CryptographyService cryptoService)
	{
		_cryptoService = cryptoService;
	}

	/// <summary>
	/// Settings for shutdown behavior.
	/// </summary>
	public MultiUpsShutdownMode ShutdownMode { get; set; } = MultiUpsShutdownMode.AnyUpsCritical;
	public Guid? PrimaryUpsId { get; set; }

	/// <summary>
	/// Power thresholds (shared across all UPS).
	/// </summary>
	public int BatteryLimitPercent { get; set; } = 30;
	public int BackupLimitSeconds { get; set; } = 120;
	public bool FollowFsd { get; set; }

	// Events
	public event EventHandler<UpsEventArgs>? UpsConnected;
	public event EventHandler<UpsEventArgs>? UpsDisconnected;
	public event EventHandler<UpsEventArgs>? UpsDataUpdated;
	public event EventHandler<UpsEventArgs>? UpsConnectionLost;
	public event EventHandler<Guid>? UpsAdded;
	public event EventHandler<Guid>? UpsRemoved;
	public event EventHandler? ShutdownConditionMet;
	public event EventHandler? AggregateStatusChanged;

	/// <summary>
	/// Gets all configured UPS devices.
	/// </summary>
	public IReadOnlyList<UpsConnectionConfig> Configs
	{
		get
		{
			lock (_lock)
			{
				return _configs.Values.ToList();
			}
		}
	}

	/// <summary>
	/// Gets a UPS service by ID.
	/// </summary>
	public UpsNetworkService? GetService(Guid id)
	{
		lock (_lock)
		{
			return _connections.TryGetValue(id, out var service) ? service : null;
		}
	}

	/// <summary>
	/// Gets a UPS config by ID.
	/// </summary>
	public UpsConnectionConfig? GetConfig(Guid id)
	{
		lock (_lock)
		{
			return _configs.TryGetValue(id, out var config) ? config : null;
		}
	}

	/// <summary>
	/// Checks if any UPS is connected.
	/// </summary>
	public bool AnyConnected
	{
		get
		{
			lock (_lock)
			{
				return _connections.Values.Any(s => s.IsConnected);
			}
		}
	}

	/// <summary>
	/// Gets the aggregate status across all UPS devices.
	/// </summary>
	public AggregateUpsStatus GetAggregateStatus()
	{
		lock (_lock)
		{
			var connectedServices = _connections.Values.Where(s => s.IsConnected).ToList();

			if (connectedServices.Count == 0)
				return new AggregateUpsStatus { State = AggregateState.Disconnected };

			var anyOnBattery = connectedServices.Any(s => s.CurrentData.IsOnBattery);
			var anyLowBattery = connectedServices.Any(s => s.CurrentData.IsLowBattery);
			var anyFsd = connectedServices.Any(s => s.CurrentData.IsForcedShutdown);
			var allOnline = connectedServices.All(s => s.CurrentData.IsOnline);

			var state = AggregateState.Online;
			if (anyFsd || anyLowBattery)
				state = AggregateState.Critical;
			else if (anyOnBattery)
				state = AggregateState.OnBattery;
			else if (!allOnline)
				state = AggregateState.Unknown;

			return new AggregateUpsStatus
			{
				State = state,
				ConnectedCount = connectedServices.Count,
				TotalCount = _configs.Count,
				AnyOnBattery = anyOnBattery,
				AnyLowBattery = anyLowBattery,
				AnyFsd = anyFsd
			};
		}
	}

	/// <summary>
	/// Adds a UPS configuration and creates a service for it.
	/// </summary>
	public void AddUps(UpsConnectionConfig config)
	{
		lock (_lock)
		{
			if (_configs.ContainsKey(config.Id))
			{
				Log.Warn("UPS with ID {Id} already exists, updating config", config.Id);
				_configs[config.Id] = config;
				UpdateServiceFromConfig(config);
				return;
			}

			_configs[config.Id] = config;
			var service = CreateServiceFromConfig(config);
			_connections[config.Id] = service;

			Log.Info("Added UPS: {Name} ({Host}:{Port}/{UpsName})", 
				config.EffectiveDisplayName, config.Host, config.Port, config.UpsName);
		}

		UpsAdded?.Invoke(this, config.Id);
	}

	/// <summary>
	/// Removes a UPS by ID.
	/// </summary>
	public async Task RemoveUpsAsync(Guid id)
	{
		UpsNetworkService? service;
		lock (_lock)
		{
			if (!_connections.TryGetValue(id, out service))
				return;

			_connections.Remove(id);
			_configs.Remove(id);
		}

		if (service.IsConnected)
			await service.DisconnectAsync();

		service.Dispose();
		Log.Info("Removed UPS: {Id}", id);

		UpsRemoved?.Invoke(this, id);
	}

	/// <summary>
	/// Updates an existing UPS configuration.
	/// </summary>
	public void UpdateUps(UpsConnectionConfig config)
	{
		lock (_lock)
		{
			if (!_configs.ContainsKey(config.Id))
			{
				Log.Warn("UPS with ID {Id} not found, adding instead", config.Id);
				AddUps(config);
				return;
			}

			_configs[config.Id] = config;
			UpdateServiceFromConfig(config);
		}
	}

	/// <summary>
	/// Connects a specific UPS.
	/// </summary>
	public async Task ConnectAsync(Guid id, CancellationToken cancellationToken = default)
	{
		UpsNetworkService? service;
		lock (_lock)
		{
			if (!_connections.TryGetValue(id, out service))
			{
				Log.Warn("Cannot connect: UPS {Id} not found", id);
				return;
			}
		}

		await service.ConnectAsync(cancellationToken);
	}

	/// <summary>
	/// Disconnects a specific UPS.
	/// </summary>
	public async Task DisconnectAsync(Guid id)
	{
		UpsNetworkService? service;
		lock (_lock)
		{
			if (!_connections.TryGetValue(id, out service))
				return;
		}

		await service.DisconnectAsync();
	}

	/// <summary>
	/// Connects all enabled UPS devices.
	/// </summary>
	public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
	{
		List<(Guid Id, UpsNetworkService Service)> toConnect;
		lock (_lock)
		{
			toConnect = _configs
				.Where(c => c.Value.Enabled && c.Value.AutoConnectOnStartup)
				.Select(c => (c.Key, _connections[c.Key]))
				.ToList();
		}

		var tasks = toConnect.Select(async item =>
		{
			try
			{
				await item.Service.ConnectAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to connect UPS {Id}", item.Id);
			}
		});

		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Disconnects all UPS devices.
	/// </summary>
	public async Task DisconnectAllAsync()
	{
		List<UpsNetworkService> services;
		lock (_lock)
		{
			services = _connections.Values.ToList();
		}

		var tasks = services.Select(async service =>
		{
			try
			{
				await service.DisconnectAsync();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error disconnecting UPS");
			}
		});

		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Loads UPS configurations from settings.
	/// </summary>
	public void LoadFromSettings(AppSettings settings)
	{
		// Migrate legacy single-UPS config if needed
		MigrateLegacySettings(settings);

		// Sync existing configs with settings
		lock (_lock)
		{
			// Find configs that were removed
			var existingIds = _configs.Keys.ToList();
			var newIds = settings.Connection.Devices.Select(d => d.Id).ToHashSet();
			var removedIds = existingIds.Where(id => !newIds.Contains(id)).ToList();

			// Remove deleted configs (disconnect first)
			foreach (var id in removedIds)
			{
				if (_connections.TryGetValue(id, out var service))
				{
					if (service.IsConnected)
					{
						_ = service.DisconnectAsync();
					}
					service.Dispose();
					_connections.Remove(id);
				}
				_configs.Remove(id);
			}
		}

		// Add or update configs
		foreach (var config in settings.Connection.Devices)
		{
			AddUps(config); // AddUps handles both add and update
		}

		ShutdownMode = settings.Connection.ShutdownMode;
		PrimaryUpsId = settings.Connection.PrimaryUpsId;
		BatteryLimitPercent = settings.Power.ShutdownLimitBatteryCharge;
		BackupLimitSeconds = settings.Power.ShutdownLimitUpsRemainTimeSeconds;
		FollowFsd = settings.Power.FollowFsd;

		Log.Info("Loaded {Count} UPS configurations", settings.Connection.Devices.Count);
	}

	/// <summary>
	/// Migrates legacy single-UPS settings to new Devices list format.
	/// </summary>
	private void MigrateLegacySettings(AppSettings settings)
	{
		var conn = settings.Connection;

		// Check if we have legacy settings and no devices
		if (!string.IsNullOrEmpty(conn.ServerAddress) && conn.Devices.Count == 0)
		{
			Log.Info("Migrating legacy single-UPS settings to multi-UPS format");

			var upsName = conn.UpsName ?? "ups";
			var host = conn.ServerAddress;
			var port = conn.Port ?? 3493;

			var legacyConfig = new UpsConnectionConfig
			{
				Id = Guid.NewGuid(),
				DisplayName = upsName,
				Address = UpsConnectionConfig.FormatAddress(upsName, host, port),
				Login = conn.Login,
				EncryptedPassword = conn.EncryptedPassword,
				PollingIntervalSeconds = conn.PollingIntervalSeconds ?? 5,
				AutoReconnect = conn.AutoReconnect ?? false,
				AutoConnectOnStartup = conn.AutoConnectOnStartup ?? false,
				Enabled = true
			};

			conn.Devices.Add(legacyConfig);
			conn.PrimaryUpsId = legacyConfig.Id;

			// Clear legacy fields
			conn.ServerAddress = null;
			conn.Port = null;
			conn.UpsName = null;
			conn.Login = null;
			conn.EncryptedPassword = null;
			conn.PollingIntervalSeconds = null;
			conn.AutoReconnect = null;
			conn.AutoConnectOnStartup = null;

			// Update version
			settings.Version = 2;
		}
	}

	private UpsNetworkService CreateServiceFromConfig(UpsConnectionConfig config)
	{
		var service = new UpsNetworkService
		{
			Host = config.Host,
			Port = config.Port,
			UpsName = config.UpsName,
			Login = config.Login,
			Password = !string.IsNullOrEmpty(config.EncryptedPassword)
				? _cryptoService.Decrypt(config.EncryptedPassword)
				: null,
			PollingIntervalMs = config.PollingIntervalSeconds * 1000,
			AutoReconnect = config.AutoReconnect,
			BatteryLimitPercent = BatteryLimitPercent,
			BackupLimitSeconds = BackupLimitSeconds,
			FollowFsd = FollowFsd
		};

		// Wire up events
		service.Connected += (s, e) => OnUpsConnected(config.Id, config, service);
		service.Disconnected += (s, e) => OnUpsDisconnected(config.Id, config, service);
		service.DataUpdated += (s, e) => OnUpsDataUpdated(config.Id, config, service);
		service.ConnectionLost += (s, e) => OnUpsConnectionLost(config.Id, config, service);
		service.ShutdownCondition += (s, e) => OnUpsShutdownCondition(config.Id);

		return service;
	}

	private void UpdateServiceFromConfig(UpsConnectionConfig config)
	{
		if (!_connections.TryGetValue(config.Id, out var service))
			return;

		service.Host = config.Host;
		service.Port = config.Port;
		service.UpsName = config.UpsName;
		service.Login = config.Login;
		service.Password = !string.IsNullOrEmpty(config.EncryptedPassword)
			? _cryptoService.Decrypt(config.EncryptedPassword)
			: null;
		service.PollingIntervalMs = config.PollingIntervalSeconds * 1000;
		service.AutoReconnect = config.AutoReconnect;
	}

	private void OnUpsConnected(Guid id, UpsConnectionConfig config, UpsNetworkService service)
	{
		Log.Info("UPS connected: {Name}", config.EffectiveDisplayName);
		UpsConnected?.Invoke(this, new UpsEventArgs(id, config, service));
		AggregateStatusChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnUpsDisconnected(Guid id, UpsConnectionConfig config, UpsNetworkService service)
	{
		Log.Info("UPS disconnected: {Name}", config.EffectiveDisplayName);
		UpsDisconnected?.Invoke(this, new UpsEventArgs(id, config, service));
		AggregateStatusChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnUpsDataUpdated(Guid id, UpsConnectionConfig config, UpsNetworkService service)
	{
		UpsDataUpdated?.Invoke(this, new UpsEventArgs(id, config, service));
		AggregateStatusChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnUpsConnectionLost(Guid id, UpsConnectionConfig config, UpsNetworkService service)
	{
		Log.Warn("UPS connection lost: {Name}", config.EffectiveDisplayName);
		UpsConnectionLost?.Invoke(this, new UpsEventArgs(id, config, service));
		AggregateStatusChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnUpsShutdownCondition(Guid id)
	{
		var shouldShutdown = ShutdownMode switch
		{
			MultiUpsShutdownMode.AnyUpsCritical => true,
			MultiUpsShutdownMode.PrimaryOnly => id == PrimaryUpsId,
			MultiUpsShutdownMode.AllUpsCritical => CheckAllUpsCritical(),
			_ => true
		};

		if (shouldShutdown)
		{
			var config = GetConfig(id);
			Log.Warn("Shutdown condition triggered by UPS: {Name}", config?.EffectiveDisplayName ?? id.ToString());
			ShutdownConditionMet?.Invoke(this, EventArgs.Empty);
		}
	}

	private bool CheckAllUpsCritical()
	{
		lock (_lock)
		{
			var connectedServices = _connections.Values.Where(s => s.IsConnected).ToList();
			if (connectedServices.Count == 0)
				return false;

			return connectedServices.All(s =>
				s.CurrentData.IsLowBattery ||
				s.CurrentData.IsForcedShutdown ||
				s.CurrentData.BatteryCharge <= BatteryLimitPercent ||
				s.CurrentData.BatteryRuntimeSeconds <= BackupLimitSeconds);
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;

		lock (_lock)
		{
			foreach (var service in _connections.Values)
			{
				service.Dispose();
			}
			_connections.Clear();
			_configs.Clear();
		}

		GC.SuppressFinalize(this);
	}
}

/// <summary>
/// Aggregate status across all UPS devices.
/// </summary>
public class AggregateUpsStatus
{
	public AggregateState State { get; set; }
	public int ConnectedCount { get; set; }
	public int TotalCount { get; set; }
	public bool AnyOnBattery { get; set; }
	public bool AnyLowBattery { get; set; }
	public bool AnyFsd { get; set; }
}

public enum AggregateState
{
	Disconnected,
	Unknown,
	Online,
	OnBattery,
	Critical
}
