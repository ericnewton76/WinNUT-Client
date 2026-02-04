using Avalonia.Media.Imaging;
using NLog;
using WinNUT_Client.Services;

namespace WinNUT_Client.ViewModels;

/// <summary>
/// Static helper class for loading UPS-related icons.
/// </summary>
public static class IconHelper
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	/// <summary>
	/// Loads the battery level icon based on UPS data (charge level and online status).
	/// Uses the numbered .ico files in Assets (e.g., 1072.ico).
	/// </summary>
	public static Bitmap? LoadBatteryIcon(UpsData data)
	{
		// Calculate icon index using AppIconIndex bit flags
		int iconIndex = (int)AppIconIndex.Offset;

		// Battery level
		if (data.BatteryCharge >= 87.5)
			iconIndex |= (int)AppIconIndex.Battery100;
		else if (data.BatteryCharge >= 62.5)
			iconIndex |= (int)AppIconIndex.Battery75;
		else if (data.BatteryCharge >= 37.5)
			iconIndex |= (int)AppIconIndex.Battery50;
		else if (data.BatteryCharge >= 12.5)
			iconIndex |= (int)AppIconIndex.Battery25;
		else
			iconIndex |= (int)AppIconIndex.Battery0;

		// Online/Offline status
		if (data.IsOnline)
			iconIndex |= (int)AppIconIndex.OnLine;

		// Check for dark mode
		var app = Avalonia.Application.Current;
		if (app?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark)
			iconIndex |= (int)AppIconIndex.WindowsDark;

		try
		{
			var uri = new Uri($"avares://WinNUT-Client/Assets/{iconIndex}.ico");
			return new Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Loads the battery state icon (charged/charging/discharging) based on UPS data.
	/// Uses Battery_Charged.png, Battery_Charging.png, Battery_Discharging.png files.
	/// </summary>
	public static Bitmap? LoadBatteryStateIcon(UpsData data)
	{
		const string IconCharged = "Battery_Charged";
		const string IconCharging = "Battery_Charging";
		const string IconDischarging = "Battery_Discharging";
		const string DarkModeSuffix = "_dm";
		
		// Use ups.status flags: CHRG = charging, DISCHRG = discharging
		// Also check battery.charger.status if available (high-end UPS only)
		string? iconName = null;
		
		// First check battery.charger.status if available
		var chargerStatus = data.ChargerStatus.ToLowerInvariant();
		if (chargerStatus == "floating" || chargerStatus == "resting")
			iconName = IconCharged;
		else if (chargerStatus == "charging")
			iconName = IconCharging;
		else if (chargerStatus == "discharging")
			iconName = IconDischarging;
		// Fall back to ups.status flags
		else if (data.IsCharging)
			iconName = IconCharging;
		else if (data.IsDischarging || data.IsOnBattery)
			iconName = IconDischarging;
		else if (data.IsOnline && data.BatteryCharge >= 95)
			iconName = IconCharged;
		else if (data.IsOnline)
			iconName = IconCharging;

		if (iconName == null)
			return null;

		// Check for dark mode
		var app = Avalonia.Application.Current;
		if (app?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark)
			iconName += DarkModeSuffix;

		try
		{
			var uri = new Uri($"avares://WinNUT-Client/Assets/{iconName}.png");
			return new Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to load battery state icon: {IconName}", iconName);
			return null;
		}
	}
}
