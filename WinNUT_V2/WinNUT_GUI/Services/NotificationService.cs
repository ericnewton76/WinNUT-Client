// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NLog;

namespace WinNUT_Client.Services;

/// <summary>
/// Provides Windows toast notification functionality.
/// </summary>
[SupportedOSPlatform("windows10.0.18362.0")]
public class NotificationService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly string _appId;

    public NotificationService()
    {
        _appId = $"{WinNutGlobals.ProgramName} - {WinNutGlobals.ShortProgramVersion}";
    }

    /// <summary>
    /// Checks if toast notifications are supported on this platform.
    /// </summary>
    public static bool IsSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        Environment.OSVersion.Version >= new Version(10, 0, 18362);

    /// <summary>
    /// Sends a toast notification with a title and message.
    /// </summary>
    public void SendToast(string title, string message)
    {
        SendToast(new[] { title, message });
    }

    /// <summary>
    /// Sends a toast notification with multiple text lines.
    /// </summary>
    public void SendToast(params string[] textParts)
    {
        if (!IsSupported)
        {
            Log.Debug("Toast notifications not supported on this platform");
            return;
        }

        try
        {
            SendWindowsToast(textParts);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send toast notification");
        }
    }

    private void SendWindowsToast(string[] textParts)
    {
        // Use Windows.UI.Notifications via WinRT interop
        // This requires the Microsoft.Windows.SDK.Contracts NuGet package or 
        // targeting windows10.0.18362 in the TFM
        
        var templateType = textParts.Length >= 3
            ? Windows.UI.Notifications.ToastTemplateType.ToastText04
            : Windows.UI.Notifications.ToastTemplateType.ToastText02;

        var toastXml = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(templateType);
        var textElements = toastXml.GetElementsByTagName("text");

        for (int i = 0; i < Math.Min(textParts.Length, (int)textElements.Length); i++)
        {
            textElements.Item((uint)i)!.InnerText = textParts[i];
        }

        var toast = new Windows.UI.Notifications.ToastNotification(toastXml);
        Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(_appId).Show(toast);
        
        Log.Debug("Toast notification sent: {Title}", textParts.FirstOrDefault());
    }

    /// <summary>
    /// Sends a notification about UPS status change.
    /// </summary>
    public void NotifyUpsStatus(string status, string details)
    {
        SendToast(WinNutGlobals.ProgramName, status, details);
    }

    /// <summary>
    /// Sends a notification about connection status.
    /// </summary>
    public void NotifyConnected(string server, int port)
    {
        SendToast(WinNutGlobals.ProgramName, "Connected", $"Connected to {server}:{port}");
    }

    /// <summary>
    /// Sends a notification about disconnection.
    /// </summary>
    public void NotifyDisconnected()
    {
        SendToast(WinNutGlobals.ProgramName, "Disconnected", "Lost connection to NUT server");
    }

    /// <summary>
    /// Sends a notification about impending shutdown.
    /// </summary>
    public void NotifyShutdownPending(int secondsRemaining)
    {
        SendToast(WinNutGlobals.ProgramName, "Shutdown Warning", 
            $"System will shutdown in {secondsRemaining} seconds due to low battery");
    }
}
