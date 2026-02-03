using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

public partial class UpsVariableItem : ObservableObject
{
	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _value = string.Empty;
}

public partial class UpsVariablesViewModel : ViewModelBase
{
	[ObservableProperty]
	private ObservableCollection<UpsVariableItem> _variables = new();

	[ObservableProperty]
	private bool _isLoading;

	[ObservableProperty]
	private string _statusMessage = string.Empty;

	public event EventHandler? CloseRequested;

	public UpsVariablesViewModel()
	{
		_ = LoadVariablesAsync();
	}

	private async Task LoadVariablesAsync()
	{
		IsLoading = true;
		StatusMessage = "Loading variables...";
		Variables.Clear();

		try
		{
			var upsNetwork = App.UpsNetwork;
			if (!upsNetwork.IsConnected)
			{
				StatusMessage = "Not connected to UPS";
				return;
			}

			var vars = await upsNetwork.GetAllVariablesAsync();
			foreach (var kvp in vars.OrderBy(x => x.Key))
			{
				Variables.Add(new UpsVariableItem { Name = kvp.Key, Value = kvp.Value });
			}

			StatusMessage = $"Loaded {Variables.Count} variables";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error: {ex.Message}";
			LoggingService.Error($"Failed to load UPS variables: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		await LoadVariablesAsync();
	}

	[RelayCommand]
	private void Close()
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
