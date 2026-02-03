// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Reflection;

namespace WinNUT_Client;

/// <summary>
/// Global application information and constants.
/// </summary>
public static class WinNutGlobals
{
	public static string LongProgramName { get; private set; } = string.Empty;
	public static string ProgramName { get; private set; } = string.Empty;
	public static string AppName => ProgramName;
	public static string ProgramVersion { get; private set; } = string.Empty;
	public static string Version => ProgramVersion;
	public static string ShortProgramVersion { get; private set; } = string.Empty;
	public static string GitHubUrl { get; private set; } = string.Empty;
	public static string Copyright { get; private set; } = string.Empty;
	public static string AppDataDirectory { get; private set; } = string.Empty;

	/// <summary>
	/// Initializes global application information from assembly metadata.
	/// </summary>
	public static void Initialize()
	{
		var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
		var version = assembly.GetName().Version ?? new Version(3, 0, 0);

		ProgramName = GetAssemblyAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "WinNUT-Client";
		LongProgramName = GetAssemblyAttribute<AssemblyDescriptionAttribute>(assembly)?.Description ??
			"WinNUT is a NUT Windows client for monitoring your UPS";
		ProgramVersion = version.ToString();
		ShortProgramVersion = $"{version.Major}.{version.Minor}";
		Copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright ??
			"Copyright (C) 2019-2026";
		GitHubUrl = "https://github.com/ericnewton76/WinNUT-Client";

		// Set up AppData directory
		AppDataDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"WinNUT-Client");

		if (!Directory.Exists(AppDataDirectory))
		{
			Directory.CreateDirectory(AppDataDirectory);
		}
	}

	private static T? GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
	{
		return assembly.GetCustomAttribute<T>();
	}
}

/// <summary>
/// Application icon indices for tray and status icons.
/// </summary>
public enum AppIconIndex
{
	Battery0 = 1,
	Battery25 = 2,
	Battery50 = 4,
	Battery75 = 8,
	Battery100 = 16,
	OnLine = 32,
	WindowsDark = 64,
	Offline = 128,
	Retry = 256,
	ViewLog = 2001,
	DeleteLog = 2002,
	Offset = 1024
}
