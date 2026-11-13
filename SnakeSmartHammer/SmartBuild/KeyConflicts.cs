#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class KeyConflicts
{
	public static void Report()
	{
		Dictionary<KeyCode, string> dictionary = new Dictionary<KeyCode, string>();
		Add(dictionary, ModConfig.AxisX.Value, "width key (AxisX)");
		Add(dictionary, ModConfig.AxisZ.Value, "depth key (AxisZ)");
		Add(dictionary, ModConfig.KeyRotation.Value, "rotation key");
		Add(dictionary, ModConfig.KeyRotationAxis.Value, "rotation axis key");
		Add(dictionary, ModConfig.KeyStagger.Value, "stagger key");
		Add(dictionary, ModConfig.KeyHold.Value, "Hold key");
		Add(dictionary, ModConfig.KeyPanel.Value, "control panel key");
		Add(dictionary, ModConfig.KeyTerrain.Value, "follow terrain key");
		Add(dictionary, ModConfig.KeyBlueprintSelect.Value, "blueprint select key");
		Add(dictionary, ModConfig.KeyDeleteBlueprint.Value, "blueprint delete key");
		Add(dictionary, ModConfig.KeyDesigner.Value, "designer box key");
		Add(dictionary, ModConfig.KeyBlueprintSave.Value, "blueprint save key (with the grid modifier)");
		Add(dictionary, ModConfig.GapModifier.Value, "gap key");
		Add(dictionary, ModConfig.KeyUp.Value, "Up key");
		Add(dictionary, ModConfig.KeyDown.Value, "Down key");
		Add(dictionary, ModConfig.KeyNudgeForward.Value, "nudge forward key");
		Add(dictionary, ModConfig.KeyNudgeBack.Value, "nudge back key");
		Add(dictionary, ModConfig.KeyNudgeLeft.Value, "nudge left key");
		Add(dictionary, ModConfig.KeyNudgeRight.Value, "nudge right key");
		Add(dictionary, ModConfig.KeyNudgeRaise.Value, "nudge up key");
		Add(dictionary, ModConfig.KeyNudgeLower.Value, "nudge down key");
		int num = 0;
		foreach (PluginInfo value2 in Chainloader.PluginInfos.Values)
		{
			BaseUnityPlugin instance = value2.Instance;
			if (instance == null || instance == HammerPlugin.instance)
			{
				continue;
			}
			try
			{
				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> item in instance.Config)
				{
					if (TryGetKey(item.Value, out var key, out var hasModifiers) && dictionary.TryGetValue(key, out var value))
					{
						num++;
						Plugin.Log.LogWarning("Key clash: " + value2.Metadata.Name + " binds '" + item.Key.Key + "' to " + key.ToString() + (hasModifiers ? " (with modifiers)" : "") + ", which is SmartBuild's " + value + ". SmartBuild still works (it blocks other item use while its keys are held), but rebind one of them if anything misbehaves.");
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogDebug("Could not read the config of " + value2.Metadata.Name + ": " + ex.Message);
			}
		}
		if (num == 0)
		{
			Plugin.Log.LogInfo("No key clashes found with other loaded mods.");
		}
	}

	private static void Add(Dictionary<KeyCode, string> map, KeyCode key, string what)
	{
		if (key != 0 && !map.ContainsKey(key))
		{
			map[key] = what;
		}
	}

	private static bool TryGetKey(ConfigEntryBase entry, out KeyCode key, out bool hasModifiers)
	{
		key = KeyCode.None;
		hasModifiers = false;
		if (entry.SettingType == typeof(KeyCode))
		{
			key = (KeyCode)entry.BoxedValue;
			return true;
		}
		if (entry.SettingType == typeof(KeyboardShortcut))
		{
			KeyboardShortcut keyboardShortcut = (KeyboardShortcut)entry.BoxedValue;
			key = keyboardShortcut.MainKey;
			hasModifiers = keyboardShortcut.Modifiers.GetEnumerator().MoveNext();
			return true;
		}
		return false;
	}
}
