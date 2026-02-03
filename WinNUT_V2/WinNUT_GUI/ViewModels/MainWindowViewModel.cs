using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly UpsNetworkService _upsNetwork;

	[ObservableProperty]
	private string _connectionStatus = "Not Connected";

	[ObservableProperty]
	private bool _isConnected;

	[ObservableProperty]
	private string _upsManufacturer = string.Empty;

	[ObservableProperty]
	private string _upsModel = string.Empty;

	[ObservableProperty]
	private string _upsStatus = string.Empty;

	[ObservableProperty]
	private double _batteryCharge;

	[ObservableProperty]
	private double _batteryVoltage;

	[ObservableProperty]
	private double _batteryRuntimeMinutes;

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

	public MainWindowViewModel()
	{
		_upsNetwork = App.UpsNetwork;

		// Subscribe to UPS events
		_upsNetwork.Connected += OnUpsConnected;
		_upsNetwork.Disconnected += OnUpsDisconnected;
		_upsNetwork.ConnectionLost += OnUpsConnectionLost;
		_upsNetwork.DataUpdated += OnUpsDataUpdated;
		_upsNetwork.RetryAttempt += OnUpsRetryAttempt;
		_upsNetwork.ShutdownCondition += OnUpsShutdownCondition;
		_upsNetwork.ShutdownCancelled += OnUpsShutdownCancelled;
	}

	[RelayCommand]
	private async Task ConnectAsync()
	{
		if (IsConnected)
			return;

		ConnectionStatus = "Connecting...";
		try
		{
			await _upsNetwork.ConnectAsync();
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

		await _upsNetwork.DisconnectAsync();
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

			// Re-apply settings to UPS service after preferences saved
			if (prefsWindow.DataContext is PreferencesViewModel vm && vm.DialogResult == true)
			{
				App.ApplySettingsToUpsNetwork();
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
		var logPath = LoggingService.LogFilePath;
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
		IsConnected = true;
		ConnectionStatus = $"Connected to {_upsNetwork.Host}:{_upsNetwork.Port}";
		RetryCount = 0;

		App.Notifications?.NotifyConnected(_upsNetwork.Host, _upsNetwork.Port);
	}

	private void OnUpsDisconnected(object? sender, EventArgs e)
	{
		IsConnected = false;
		ConnectionStatus = "Disconnected";
		ClearUpsData();
	}

	private void OnUpsConnectionLost(object? sender, EventArgs e)
	{
		IsConnected = false;
		ConnectionStatus = "Connection lost - reconnecting...";

		App.Notifications?.NotifyDisconnected();
	}

	private void OnUpsDataUpdated(object? sender, EventArgs e)
	{
		var data = _upsNetwork.CurrentData;

		UpsManufacturer = data.Manufacturer;
		UpsModel = data.Model;
		UpsStatus = data.Status;
		BatteryCharge = data.BatteryCharge;
		BatteryVoltage = data.BatteryVoltage;
		BatteryRuntimeMinutes = data.BatteryRuntimeSeconds / 60.0;
		InputVoltage = data.InputVoltage;
		OutputVoltage = data.OutputVoltage;
		InputFrequency = data.InputFrequency;
		Load = data.Load;
		OutputPower = data.OutputPower;
	}

	private void OnUpsRetryAttempt(object? sender, EventArgs e)
	{
		RetryCount = _upsNetwork.RetryCount;
		ConnectionStatus = $"Reconnecting... ({RetryCount}/{_upsNetwork.MaxRetries})";
	}

	private void OnUpsShutdownCondition(object? sender, EventArgs e)
	{
		// TODO: Show shutdown dialog or initiate shutdown sequence
		LoggingService.Warning("Shutdown condition triggered");
	}

	private void OnUpsShutdownCancelled(object? sender, EventArgs e)
	{
		LoggingService.Notice("Shutdown cancelled - power restored");
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
