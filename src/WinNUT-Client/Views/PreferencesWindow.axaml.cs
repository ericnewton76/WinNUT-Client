using Avalonia.Controls;
using WinNUT_Client.ViewModels;

namespace WinNUT_Client.Views;

public partial class PreferencesWindow : Window
{
	public PreferencesWindow()
	{
		InitializeComponent();

		var vm = new PreferencesViewModel();
		DataContext = vm;
		vm.CloseRequested += (_, _) => Close(vm.DialogResult);
	}
}
