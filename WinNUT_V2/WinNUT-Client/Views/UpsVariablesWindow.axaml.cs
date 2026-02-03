using Avalonia.Controls;
using WinNUT_Client.ViewModels;

namespace WinNUT_Client.Views;

public partial class UpsVariablesWindow : Window
{
	public UpsVariablesWindow()
	{
		InitializeComponent();

		var vm = new UpsVariablesViewModel();
		DataContext = vm;
		vm.CloseRequested += (_, _) => Close();
	}
}
