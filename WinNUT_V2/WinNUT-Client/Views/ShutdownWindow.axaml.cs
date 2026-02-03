using Avalonia.Controls;
using WinNUT_Client.ViewModels;

namespace WinNUT_Client.Views;

public partial class ShutdownWindow : Window
{
	private ShutdownViewModel? _viewModel;

	public bool ShutdownConfirmed { get; private set; }

	/// <summary>
	/// Parameterless constructor required by XAML loader.
	/// </summary>
	public ShutdownWindow() : this(60, null)
	{
	}

	public ShutdownWindow(int delaySeconds, string? message = null)
	{
		InitializeComponent();

		_viewModel = new ShutdownViewModel(delaySeconds, message);
		DataContext = _viewModel;

		_viewModel.ShutdownConfirmed += (_, _) =>
		{
			ShutdownConfirmed = true;
			Close();
		};

		_viewModel.ShutdownCancelled += (_, _) =>
		{
			ShutdownConfirmed = false;
			Close();
		};
	}

	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		_viewModel?.StartCountdown();
	}

	protected override void OnClosing(WindowClosingEventArgs e)
	{
		_viewModel?.StopCountdown();
		base.OnClosing(e);
	}
}
