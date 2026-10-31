using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;

namespace SnakeSkills;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInDependency("org.bepinex.plugins.creaturelevelcontrol", BepInDependency.DependencyFlags.SoftDependency)]
// The separate mods register the same skills and patches, so SnakeSkills stays out of the way
// while any of them is installed (BepInEx logs which one blocked it).
[BepInIncompatibility(SailingGUID)]
[BepInIncompatibility(RanchingGUID)]
[BepInIncompatibility(MiningGUID)]
public class SnakeSkillsPlugin : BaseUnityPlugin
{
	internal const string ModName = "SnakeSkills";
	internal const string ModVersion = "1.10.0";
	internal const string ModGUID = "com.snakesquad.snakeskills";
	internal const string SailingGUID = "org.bepinex.plugins.sailing";
	internal const string RanchingGUID = "org.bepinex.plugins.ranching";
	internal const string MiningGUID = "org.bepinex.plugins.mining";
	private const string ValheimPlusGUID = "org.bepinex.plugins.valheim_plus";

	private static readonly ConfigSync configSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	internal static SnakeSkillsPlugin instance = null!;

	private static ConfigEntry<Toggle> serverConfigLocked = null!;

	internal enum Toggle
	{
		On = 1,
		Off = 0,
	}

	internal class ConfigurationManagerAttributes
	{
		[UsedImplicitly] public bool? ShowRangeAsPercent;
		[UsedImplicitly] public int? Order;
		[UsedImplicitly] public string? Description;
		[UsedImplicitly] public string? DispName;
	}

	/// <summary>Binds a server-synced entry. oldFile/oldSection name where the separate mod kept
	/// the same setting, so a first start picks up the values the server already used.</summary>
	internal ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, string? oldFile = null, string? oldSection = null, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
		if (oldFile != null && oldSection != null)
		{
			ConfigImport.Apply(configEntry, oldFile, oldSection, name);
		}

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	internal ConfigEntry<T> config<T>(string group, string name, T value, string description, string? oldFile = null, string? oldSection = null, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), oldFile, oldSection, synchronizedSetting);

	public void Awake()
	{
		instance = this;

		// Must run before the first Bind, which creates the file.
		ConfigImport.Begin(!File.Exists(Config.ConfigFilePath), Logger);

		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.", SailingGUID, "1 - General");
		configSync.AddLockingConfigEntry(serverConfigLocked);

		Sailing.Init(this);
		Ranching.Init(this);
		Mining.Init(this);
		Fishing.Init(this);
		Landscaping.Init(this);
		RestoreTerrain.Init(this);
		Trinkets.Init(this);

		Harmony harmony = new(ModGUID);
		harmony.PatchAll(Assembly.GetExecutingAssembly());

		if (Chainloader.PluginInfos.ContainsKey(ValheimPlusGUID))
		{
			Logger.LogWarning("Valheim Plus (or a fork of it) is also loaded. Both mods patch ship speed and controls, taming/breeding, mining and fishing XP, so behavior may not be exactly what either mod intends on its own - test on your server before relying on it.");
		}
	}

	private void Update()
	{
		Mining.Update();
		Landscaping.Update();
	}

	internal static void Log(string message) => instance.Logger.LogInfo(message);
}
