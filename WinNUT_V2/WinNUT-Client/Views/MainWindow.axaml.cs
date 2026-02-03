using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WinNUT_Client.Services;
using WinNUT_Client.ViewModels;

namespace WinNUT_Client.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		// Handle window state changes for minimize to tray
		PropertyChanged += OnPropertyChanged;
	}

	private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == WindowStateProperty)
		{
			var newState = (WindowState)e.NewValue!;
			if (newState == WindowState.Minimized && App.Settings.Settings.Appearance.MinimizeToTray)
			{
				Hide();
			}
		}
	}

	protected override void OnClosing(WindowClosingEventArgs e)
	{
		if (App.Settings.Settings.Appearance.CloseToTray)
		{
			e.Cancel = true;
			Hide();
		}
		else
		{
			base.OnClosing(e);
		}
	}

	private void SidebarItem_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is Border border && border.DataContext is UpsSummary ups)
		{
			if (DataContext is MainWindowViewModel vm)
			{
				vm.SelectUps(ups);
			}
		}
	}
}
