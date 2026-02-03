// WinNUT-Client is a NUT Windows client for monitoring your UPS.
// Copyright (C) 2019-2026 Gawindx (Decaux Nicolas), Eric Newton
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY.

using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace WinNUT_Client.Services;

/// <summary>
/// Provides encryption and decryption services using machine-specific keys.
/// Used to securely store sensitive configuration data like passwords.
/// </summary>
public sealed class CryptographyService : IDisposable
{
	private readonly TripleDES _tripleDes;
	private bool _disposed;

	public CryptographyService()
	{
		_tripleDes = TripleDES.Create();
		_tripleDes.Key = TruncateHash(GetUniqueKeyHash(), _tripleDes.KeySize / 8);
		_tripleDes.IV = TruncateHash(string.Empty, _tripleDes.BlockSize / 8);
	}

	private static byte[] TruncateHash(string key, int length)
	{
		var keyBytes = Encoding.Unicode.GetBytes(key);
		var hash = SHA1.HashData(keyBytes);

		// Truncate or pad the hash
		Array.Resize(ref hash, length);
		return hash;
	}

	public string Encrypt(string? plaintext)
	{
		plaintext ??= string.Empty;

		var plaintextBytes = Encoding.Unicode.GetBytes(plaintext);

		using var ms = new MemoryStream();
		using var encStream = new CryptoStream(ms, _tripleDes.CreateEncryptor(), CryptoStreamMode.Write);

		encStream.Write(plaintextBytes, 0, plaintextBytes.Length);
		encStream.FlushFinalBlock();

		return Convert.ToBase64String(ms.ToArray());
	}

	public string Decrypt(string encryptedText)
	{
		var encryptedBytes = Convert.FromBase64String(encryptedText);

		using var ms = new MemoryStream();
		using var decStream = new CryptoStream(ms, _tripleDes.CreateDecryptor(), CryptoStreamMode.Write);

		decStream.Write(encryptedBytes, 0, encryptedBytes.Length);
		decStream.FlushFinalBlock();

		return Encoding.Unicode.GetString(ms.ToArray());
	}

	public bool IsEncrypted(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		try
		{
			var decrypted = Decrypt(text);
			var reEncrypted = Encrypt(decrypted);
			return text == reEncrypted;
		}
		catch
		{
			return false;
		}
	}

	private static string GetUniqueKeyHash()
	{
		var uniqueKey = GetMotherboardId() + GetProcessorId();
		var hash = SHA1.HashData(Encoding.UTF8.GetBytes(uniqueKey));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static string GetMotherboardId()
	{
		try
		{
			using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
			foreach (var obj in searcher.Get())
			{
				return obj["SerialNumber"]?.ToString() ?? string.Empty;
			}
		}
		catch
		{
			// Ignore WMI errors
		}
		return string.Empty;
	}

	private static string GetProcessorId()
	{
		try
		{
			using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
			foreach (var obj in searcher.Get())
			{
				return obj["ProcessorId"]?.ToString() ?? string.Empty;
			}
		}
		catch
		{
			// Ignore WMI errors
		}
		return string.Empty;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_tripleDes.Dispose();
			_disposed = true;
		}
	}
}
