using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

/// <summary>
/// ViewModel for the UPS card display component.
/// Contains all the data binding properties for displaying UPS information.
/// </summary>
public partial class UpsCardViewModel : ObservableObject
{
	[ObservableProperty]
	private string _upsDisplayName = string.Empty;

	[ObservableProperty]
	private string _manfModelInfo = string.Empty;

	[ObservableProperty]
	private string _upsSerial = string.Empty;

	[ObservableProperty]
	private bool _hasSerial;

	[ObservableProperty]
	private string _upsStatus = string.Empty;

	[ObservableProperty]
	private string _upsStatusDisplay = "Unknown";

	[ObservableProperty]
	private IBrush _statusBadgeColor = Brushes.Gray;

	[ObservableProperty]
	private Bitmap? _batteryIcon;

	[ObservableProperty]
	private Bitmap? _batteryStateIcon;

	[ObservableProperty]
	private bool _hasBatteryStateIcon;

	[ObservableProperty]
	private double _batteryCharge;

	[ObservableProperty]
	private double _batteryVoltage;

	[ObservableProperty]
	private double _batteryRuntimeMinutes;

	[ObservableProperty]
	private string _batteryRuntimeDisplay = "--";

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

	/// <summary>
	/// Updates the card with data from a UPS.
	/// </summary>
	/// <param name="data">The UPS data to display.</param>
	/// <param name="displayName">The configured display name for this UPS.</param>
	public void UpdateFromUpsData(UpsData data, string displayName)
	{
		UpsDisplayName = displayName;
		
		// Combine manufacturer and model for second line
		var parts = new List<string>();
		if (!string.IsNullOrEmpty(data.Manufacturer)) parts.Add(data.Manufacturer);
		if (!string.IsNullOrEmpty(data.Model)) parts.Add(data.Model);
		ManfModelInfo = string.Join(" ", parts);
		
		UpsSerial = data.Serial;
		HasSerial = !string.IsNullOrEmpty(data.Serial) && !data.Serial.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
		UpsStatus = data.Status;
		BatteryCharge = data.BatteryCharge;
		BatteryVoltage = data.BatteryVoltage;
		BatteryRuntimeMinutes = data.BatteryRuntimeSeconds / 60.0;
		InputVoltage = data.InputVoltage;
		OutputVoltage = data.OutputVoltage;
		InputFrequency = data.InputFrequency;
		Load = data.Load;
		OutputPower = data.OutputPower;

		UpdateStatusDisplay(data);
		UpdateBatteryRuntimeDisplay();
	}

	/// <summary>
	/// Clears all UPS data (e.g., when disconnected).
	/// </summary>
	public void Clear()
	{
		UpsDisplayName = string.Empty;
		ManfModelInfo = string.Empty;
		UpsSerial = string.Empty;
		HasSerial = false;
		UpsStatus = string.Empty;
		UpsStatusDisplay = "Unknown";
		StatusBadgeColor = Brushes.Gray;
		BatteryIcon = null;
		BatteryStateIcon = null;
		HasBatteryStateIcon = false;
		BatteryCharge = 0;
		BatteryVoltage = 0;
		BatteryRuntimeMinutes = 0;
		BatteryRuntimeDisplay = "--";
		InputVoltage = 0;
		OutputVoltage = 0;
		InputFrequency = 0;
		Load = 0;
		OutputPower = 0;
	}

	private void UpdateStatusDisplay(UpsData data)
	{
		if (data.IsOnline)
		{
			UpsStatusDisplay = data.IsCharging ? "Online (Charging)" : "Online";
			StatusBadgeColor = Brushes.Green;
		}
		else if (data.IsOnBattery)
		{
			UpsStatusDisplay = data.IsLowBattery ? "On Battery (Low!)" : "On Battery";
			StatusBadgeColor = data.IsLowBattery ? Brushes.Red : Brushes.Orange;
		}
		else if (data.IsForcedShutdown)
		{
			UpsStatusDisplay = "Forced Shutdown";
			StatusBadgeColor = Brushes.Red;
		}
		else
		{
			UpsStatusDisplay = data.Status;
			StatusBadgeColor = Brushes.Gray;
		}

		// Update battery icons
		BatteryIcon = IconHelper.LoadBatteryIcon(data);
		BatteryStateIcon = IconHelper.LoadBatteryStateIcon(data);
		HasBatteryStateIcon = BatteryStateIcon != null;
	}

	private void UpdateBatteryRuntimeDisplay()
	{
		if (BatteryRuntimeMinutes < 1)
		{
			BatteryRuntimeDisplay = $"{BatteryRuntimeMinutes * 60:F0} sec";
		}
		else if (BatteryRuntimeMinutes < 60)
		{
			BatteryRuntimeDisplay = $"{BatteryRuntimeMinutes:F0} min";
		}
		else
		{
			var hours = (int)(BatteryRuntimeMinutes / 60);
			var mins = (int)(BatteryRuntimeMinutes % 60);
			BatteryRuntimeDisplay = $"{hours}h {mins}m";
		}
	}
}
