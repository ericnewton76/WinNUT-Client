using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinNUT_Client.ViewModels;

/// <summary>
/// Summary information for a UPS shown in the sidebar.
/// </summary>
public partial class UpsSummary : ObservableObject
{
	public Guid Id { get; set; }

	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _host = string.Empty;

	[ObservableProperty]
	private bool _isOnline;

	[ObservableProperty]
	private bool _isOnBattery;

	[ObservableProperty]
	private bool _isConnected;

	[ObservableProperty]
	private double _batteryCharge;

	[ObservableProperty]
	private IBrush _statusColor = Brushes.Gray;

	[ObservableProperty]
	private string _statusText = "Unknown";

	[ObservableProperty]
	private bool _isSelected;

	[ObservableProperty]
	private Bitmap? _batteryIcon;

	public string DisplayName => string.IsNullOrEmpty(Name) ? Host : Name;
}
