using Avalonia.Controls;
using WinNUT_Client.ViewModels;

namespace WinNUT_Client.Views;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();

		var vm = new AboutViewModel();
		DataContext = vm;
		vm.CloseRequested += (_, _) => Close();
	}
}
