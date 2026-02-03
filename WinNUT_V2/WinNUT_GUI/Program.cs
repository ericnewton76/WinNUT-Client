using Avalonia;
using System;
using System.Threading;

namespace WinNUT_Client;

sealed class Program
{
	private static Mutex? _mutex;
	private const string MutexName = "WinNUT-Client-SingleInstance-3.0";

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		// Check for single instance
		_mutex = new Mutex(true, MutexName, out bool createdNew);

		if (!createdNew)
		{
			// Another instance is already running
			// TODO: Could signal existing instance to show window
			Console.WriteLine("WinNUT-Client is already running.");
			return;
		}

		try
		{
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}
		finally
		{
			_mutex?.ReleaseMutex();
			_mutex?.Dispose();
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
}
