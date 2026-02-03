using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinNUT_Client.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
	public string AppName => WinNutGlobals.AppName;
	public string Version => $"Version {WinNutGlobals.Version}";
	public string Copyright => WinNutGlobals.Copyright;
	public string GitHubUrl => WinNutGlobals.GitHubUrl;

	public event EventHandler? CloseRequested;

	[RelayCommand]
	private void OpenGitHub()
	{
		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
	}

	[RelayCommand]
	private void Close()
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
