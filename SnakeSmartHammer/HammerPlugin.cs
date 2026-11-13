using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace SnakeSmartHammer;

/// <summary>
/// The smart hammer (SmartBuild 1.21.0 / SnakeHammer port on the vanilla hammer) as its own
/// plugin. It used to live inside SnakeSkills (up to 1.28); it is split out so SnakeSkills can be
/// shared on its own. GitHub-only: this code derives from SmartBuild and must never be uploaded
/// to Thunderstore.
/// </summary>
[BepInPlugin(ModGUID, ModName, ModVersion)]
public class HammerPlugin : BaseUnityPlugin
{
	internal const string ModName = "SnakeSmartHammer";
	internal const string ModVersion = "1.29.0";
	internal const string ModGUID = "com.snakesquad.snakesmarthammer";
	private const string SnakeSkillsGUID = "com.snakesquad.snakeskills";

	internal static HammerPlugin instance = null!;

	internal enum Toggle
	{
		On = 1,
		Off = 0,
	}

	public void Awake()
	{
		instance = this;
		CarryOverSnakeSkillsSettings();
		Plugin.Init(this);
	}

	/// <summary>
	/// First start only: SnakeSkills 1.28 and older kept the smart hammer's settings in its own
	/// .cfg under "Smart Hammer" sections. Copy those sections into this plugin's file, so keys and
	/// options stay as they were.
	/// </summary>
	private void CarryOverSnakeSkillsSettings()
	{
		string oldPath = Path.Combine(Paths.ConfigPath, SnakeSkillsGUID + ".cfg");
		if (File.Exists(Config.ConfigFilePath) || !File.Exists(oldPath))
		{
			return;
		}

		List<string> lines = new();
		bool copying = false;
		foreach (string raw in File.ReadAllLines(oldPath))
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]"))
			{
				copying = line.StartsWith("[Smart Hammer");
			}
			if (copying)
			{
				lines.Add(raw);
			}
		}

		if (lines.Count == 0)
		{
			return;
		}

		File.WriteAllLines(Config.ConfigFilePath, lines);
		Config.Reload();
		Logger.LogInfo($"First start: copied the smart hammer settings from {SnakeSkillsGUID}.cfg");
	}
}
