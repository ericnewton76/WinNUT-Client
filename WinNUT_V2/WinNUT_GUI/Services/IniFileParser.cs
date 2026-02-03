// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

namespace WinNUT_Client.Services;

/// <summary>
/// Simple INI file parser for importing legacy WinNUT 1.x configuration files.
/// </summary>
public class IniFileParser
{
	/// <summary>
	/// Represents parsed INI file data organized by section and key.
	/// </summary>
	public class IniData
	{
		private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);

		public IReadOnlyDictionary<string, Dictionary<string, string>> Sections => _sections;

		internal void SetValue(string section, string key, string value)
		{
			if (!_sections.TryGetValue(section, out var sectionData))
			{
				sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				_sections[section] = sectionData;
			}
			sectionData[key] = value;
		}

		public bool TryGetValue(string section, string key, out string value)
		{
			value = string.Empty;
			if (_sections.TryGetValue(section, out var sectionData))
			{
				return sectionData.TryGetValue(key, out value!);
			}
			return false;
		}

		public bool TryGetInt(string section, string key, out int value)
		{
			value = 0;
			if (TryGetValue(section, key, out var strValue))
			{
				return int.TryParse(strValue, out value);
			}
			return false;
		}

		public bool TryGetBool(string section, string key, out bool value)
		{
			value = false;
			if (TryGetValue(section, key, out var strValue))
			{
				// Handle various boolean representations
				if (int.TryParse(strValue, out var intValue))
				{
					value = intValue != 0;
					return true;
				}
				if (bool.TryParse(strValue, out value))
				{
					return true;
				}
				// Handle "true"/"false", "yes"/"no"
				value = strValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
						strValue.Equals("yes", StringComparison.OrdinalIgnoreCase);
				return true;
			}
			return false;
		}

		public bool TryGetDouble(string section, string key, out double value)
		{
			value = 0;
			if (TryGetValue(section, key, out var strValue))
			{
				return double.TryParse(strValue, out value);
			}
			return false;
		}
	}

	/// <summary>
	/// Loads and parses an INI file asynchronously.
	/// </summary>
	public async Task<IniData> LoadAsync(string filePath)
	{
		var content = await File.ReadAllTextAsync(filePath);
		return Parse(content);
	}

	/// <summary>
	/// Loads and parses an INI file synchronously.
	/// </summary>
	public IniData Load(string filePath)
	{
		var content = File.ReadAllText(filePath);
		return Parse(content);
	}

	/// <summary>
	/// Parses INI content from a string.
	/// </summary>
	public IniData Parse(string content)
	{
		var data = new IniData();
		var currentSection = "Default";

		var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

		foreach (var rawLine in lines)
		{
			var line = rawLine.Trim();

			// Skip empty lines and comments
			if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
				continue;

			// Section header
			if (line.StartsWith('[') && line.EndsWith(']'))
			{
				currentSection = line[1..^1].Trim();
				continue;
			}

			// Key-value pair
			var equalsIndex = line.IndexOf('=');
			if (equalsIndex > 0)
			{
				var key = line[..equalsIndex].Trim();
				var value = line[(equalsIndex + 1)..].Trim();

				// Remove surrounding quotes if present
				if (value.Length >= 2 &&
					((value.StartsWith('"') && value.EndsWith('"')) ||
					 (value.StartsWith('\'') && value.EndsWith('\''))))
				{
					value = value[1..^1];
				}

				data.SetValue(currentSection, key, value);
			}
		}

		return data;
	}
}
