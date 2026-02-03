using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinNUT_Client.ViewModels;

public partial class ShutdownViewModel : ViewModelBase
{
	private readonly System.Timers.Timer _countdownTimer;
	private int _remainingSeconds;

	[ObservableProperty]
	private string _message = "System will shutdown due to low battery";

	[ObservableProperty]
	private string _countdownText = string.Empty;

	[ObservableProperty]
	private double _progressValue = 100;

	public int TotalSeconds { get; private set; }

	public event EventHandler? ShutdownConfirmed;
	public event EventHandler? ShutdownCancelled;

	public ShutdownViewModel(int delaySeconds = 60, string? customMessage = null)
	{
		TotalSeconds = delaySeconds;
		_remainingSeconds = delaySeconds;

		if (!string.IsNullOrEmpty(customMessage))
		{
			Message = customMessage;
		}

		UpdateCountdownDisplay();

		_countdownTimer = new System.Timers.Timer(1000);
		_countdownTimer.Elapsed += OnCountdownTick;
		_countdownTimer.AutoReset = true;
	}

	public void StartCountdown()
	{
		_countdownTimer.Start();
	}

	public void StopCountdown()
	{
		_countdownTimer.Stop();
	}

	private void OnCountdownTick(object? sender, System.Timers.ElapsedEventArgs e)
	{
		_remainingSeconds--;

		Avalonia.Threading.Dispatcher.UIThread.Post(() =>
		{
			UpdateCountdownDisplay();

			if (_remainingSeconds <= 0)
			{
				_countdownTimer.Stop();
				ShutdownConfirmed?.Invoke(this, EventArgs.Empty);
			}
		});
	}

	private void UpdateCountdownDisplay()
	{
		var minutes = _remainingSeconds / 60;
		var seconds = _remainingSeconds % 60;
		CountdownText = $"{minutes:D2}:{seconds:D2}";
		ProgressValue = (double)_remainingSeconds / TotalSeconds * 100;
	}

	[RelayCommand]
	private void Cancel()
	{
		_countdownTimer.Stop();
		ShutdownCancelled?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand]
	private void ShutdownNow()
	{
		_countdownTimer.Stop();
		ShutdownConfirmed?.Invoke(this, EventArgs.Empty);
	}
}
