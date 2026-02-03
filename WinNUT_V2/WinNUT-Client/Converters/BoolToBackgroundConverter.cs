using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WinNUT_Client.Converters;

/// <summary>
/// Converts a boolean to a background brush for selection highlighting.
/// </summary>
public class BoolToBackgroundConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isSelected && isSelected)
		{
			return new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)); // Semi-transparent blue
		}
		return Brushes.Transparent;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
