using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace SnakeSkills;

/// <summary>
/// On the very first start (no SnakeSkills .cfg yet), copies values from the separate Sailing,
/// Ranching and Mining config files, so a server switching over keeps its settings. Runs once:
/// after that the SnakeSkills file exists and wins.
/// </summary>
internal static class ConfigImport
{
	private static bool active;
	private static ManualLogSource logger = null!;
	private static readonly Dictionary<string, Dictionary<string, string>?> files = new();

	public static void Begin(bool firstStart, ManualLogSource log)
	{
		active = firstStart;
		logger = log;
	}

	public static void Apply(ConfigEntryBase entry, string oldGuid, string oldSection, string key)
	{
		if (!active || Read(oldGuid) is not { } values || !values.TryGetValue(oldSection + "\n" + key, out string value))
		{
			return;
		}

		try
		{
			entry.SetSerializedValue(value);
			logger.LogInfo($"Imported [{entry.Definition.Section}] {key} = {value} from {oldGuid}.cfg");
		}
		catch (Exception e)
		{
			logger.LogWarning($"Could not import {key} from {oldGuid}.cfg: {e.Message}");
		}
	}

	private static Dictionary<string, string>? Read(string guid)
	{
		if (files.TryGetValue(guid, out Dictionary<string, string>? cached))
		{
			return cached;
		}

		string path = Path.Combine(Paths.ConfigPath, guid + ".cfg");
		Dictionary<string, string>? values = null;
		if (File.Exists(path))
		{
			values = new Dictionary<string, string>();
			string section = "";
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					section = line.Substring(1, line.Length - 2);
				}
				else if (!line.StartsWith("#") && line.IndexOf('=') is var eq and > 0)
				{
					values[section + "\n" + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
				}
			}
			logger.LogInfo($"First start: importing settings from {guid}.cfg");
		}

		files[guid] = values;
		return values;
	}
}
