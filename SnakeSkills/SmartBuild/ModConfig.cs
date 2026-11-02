#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class ModConfig
{
	public static ConfigEntry<SnakeSkillsPlugin.Toggle> Enabled;

	public static ConfigEntry<KeyCode> KeyBlueprintList;

	public static ConfigEntry<bool> ListThumbnails;

	public static ConfigEntry<bool> BuildMenuTab;

	public static ConfigEntry<bool> CostPanel;

	// SnakeSkills: every SmartBuild section is shown as "Smart Hammer - <section>". The first time
	// they appear in SnakeSkills' config, values come over from SmartBuild's/SnakeHammer's .cfg.
	private const string SectionPrefix = "Smart Hammer - ";

	private const string MainSection = "Smart Hammer";

	private static Dictionary<string, string> _imported;

	public static ConfigEntry<KeyCode> GridModifier;

	public static ConfigEntry<KeyCode> GapModifier;

	public static ConfigEntry<KeyCode> UndoKey;

	public static ConfigEntry<KeyCode> KeyUp;

	public static ConfigEntry<KeyCode> KeyDown;

	public static ConfigEntry<KeyCode> KeyNudgeForward;

	public static ConfigEntry<KeyCode> KeyNudgeBack;

	public static ConfigEntry<KeyCode> KeyNudgeLeft;

	public static ConfigEntry<KeyCode> KeyNudgeRight;

	public static ConfigEntry<KeyCode> KeyNudgeRaise;

	public static ConfigEntry<KeyCode> KeyNudgeLower;

	public static ConfigEntry<float> NudgeStep;

	public static ConfigEntry<float> NudgeBigStep;

	public static ConfigEntry<KeyCode> KeyRotation;

	public static ConfigEntry<KeyCode> KeyRotationAxis;

	public static ConfigEntry<KeyCode> KeyStagger;

	public static ConfigEntry<KeyCode> KeyHold;

	public static ConfigEntry<KeyCode> KeyPanel;

	public static ConfigEntry<KeyCode> KeyTerrain;

	public static ConfigEntry<KeyCode> KeyBlueprintSelect;

	public static ConfigEntry<KeyCode> KeyDeleteBlueprint;

	public static ConfigEntry<KeyCode> KeyDesigner;

	public static ConfigEntry<float> DesignerMaxSize;

	public static ConfigEntry<bool> DesignerSelectsRepair;

	public static ConfigEntry<KeyCode> KeyBlueprintSave;

	public static ConfigEntry<KeyCode> AxisX;

	public static ConfigEntry<bool> LogPlacement;

	public static ConfigEntry<bool> FullCostList;

	public static ConfigEntry<KeyCode> AxisZ;

	public static ConfigEntry<KeyCode> BigStepModifier;

	public static ConfigEntry<int> MaxGridSize;

	public static ConfigEntry<int> MaxCells;

	public static ConfigEntry<bool> ShowHud;

	public static ConfigEntry<bool> ShowControlsHint;

	public static ConfigEntry<bool> PanelShowControls;

	public static ConfigEntry<float> MaxGap;

	public static ConfigEntry<float> GapStep;

	public static ConfigEntry<float> RotationStep;

	public static ConfigEntry<float> GapBigStep;

	public static ConfigEntry<float> RotationBigStep;

	public static ConfigEntry<float> StaggerStep;

	public static ConfigEntry<float> MaxStagger;

	public static ConfigEntry<int> PlacePerFrame;

	public static ConfigEntry<int> PreviewMaxCells;

	public static ConfigEntry<int> BigStep;

	public static ConfigEntry<float> HoldReach;

	public static ConfigEntry<float> SelectReach;

	private const string Size = "Size";

	private const string Gap = "Gap";

	private const string Stagger = "Stagger";

	private const string Rotation = "Rotation";

	private const string Nudge = "Nudge";

	private const string Hold = "Hold";

	private const string Terrain = "Follow terrain";

	private const string Undo = "Undo";

	private const string Panel = "Control panel";

	private const string Blueprints = "Blueprints";

	private const string Modifiers = "Modifiers";

	private const string Display = "Display";

	private const string Performance = "Performance";

	public static void Init(ConfigFile config)
	{
		bool saveOnConfigSet = config.SaveOnConfigSet;
		config.SaveOnConfigSet = false;
		try
		{
			bool firstTime = !File.Exists(config.ConfigFilePath) || !File.ReadAllText(config.ConfigFilePath).Contains("[" + MainSection + "]");
			_imported = firstTime ? ReadOldConfig() : null;
			Enabled = config.Bind(MainSection, "Smart Hammer", SnakeSkillsPlugin.Toggle.On, "SmartBuild (grids, blueprints, Hold, the K panel) on the vanilla hammer. Off: the hammer is plain vanilla. Stays off by itself while SmartBuild or SnakeHammer is installed.");
			KeyBlueprintList = config.Bind(MainSection, "Blueprint List Key", KeyCode.Y, "With the hammer out in build mode, press to open the list of saved blueprints. Click one to build it; the list can also save the pieces you selected as a new blueprint. Saved blueprints are also in the hammer's build menu, on the Blueprints tab.");
			ListThumbnails = config.Bind(MainSection, "Blueprint List Pictures", true, "Show a small picture of each blueprint in the blueprint list and as its icon in the build menu. They are drawn while the hammer is out (one per frame) and kept until the game closes.");
			BuildMenuTab = config.Bind(MainSection, "Blueprints Tab", true, "Add a Blueprints tab to the hammer's build menu, after Favorites (Q/E or click to reach it). Off: blueprints are listed under Misc instead. Takes effect after restarting the game.");
			CostPanel = config.Bind(MainSection, "Blueprint Materials Panel", true, "When a blueprint needs more materials than the build info's slots show, list all of them (icon, have / need, red when short) in a panel beside it.");
			BindAll(config);
		}
		finally
		{
			_imported = null;
			config.SaveOnConfigSet = saveOnConfigSet;
		}
		config.Save();
	}

	private static Dictionary<string, string> ReadOldConfig()
	{
		string path = Path.Combine(Paths.ConfigPath, Plugin.SmartBuildGuid + ".cfg");
		if (!File.Exists(path))
		{
			return null;
		}
		Dictionary<string, string> values = new Dictionary<string, string>();
		string section = "";
		foreach (string raw in File.ReadAllLines(path))
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]"))
			{
				section = line.Substring(1, line.Length - 2);
			}
			else if (!line.StartsWith("#") && line.IndexOf('=') > 0)
			{
				int eq = line.IndexOf('=');
				values[section + "\n" + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
			}
		}
		Plugin.Log.LogInfo("Smart hammer: importing settings from " + path);
		return values;
	}

	private static void BindAll(ConfigFile config)
	{
		GridModifier = Bind(config, "Modifiers", "GridModifier", "Controls", KeyCode.LeftControl, "Hold with the undo key to undo the last grid, or with the blueprint save key to save a blueprint. Tap it on its own to switch the rotation key (R) between turning each piece and tilting the whole grid.");
		BigStepModifier = Bind(config, "Modifiers", "BigStepModifier", "Controls", KeyCode.LeftShift, "Hold for bigger steps: size by BigStep instead of 1, and the gap, rotation and nudge by their own big step.");
		AxisX = Bind(config, "Size", "AxisX", "Controls", KeyCode.X, "Hold, then press Up/Down or scroll, to change the width (X). Hold together with the depth key to change the height (Y) instead. Sitting is blocked while this is held with a piece showing.");
		AxisZ = Bind(config, "Size", "AxisZ", "Controls", KeyCode.Z, "Hold, then press Up/Down or scroll, to change the depth (Z). Hold together with the width key to change the height (Y) instead.");
		KeyUp = Bind(config, "Size", "KeyUp", "Controls", KeyCode.UpArrow, "Grow the held axis (or its gap, stagger or rotation). Scrolling up does the same.");
		KeyDown = Bind(config, "Size", "KeyDown", "Controls", KeyCode.DownArrow, "Shrink the held axis (or its gap, stagger or rotation). Scrolling down does the same.");
		BigStep = Bind(config, "Size", "BigStep", "Grid", 10, new ConfigDescription("Size change per key press while the big step modifier is held.", new AcceptableValueRange<int>(2, 50)));
		MaxGridSize = Bind(config, "Size", "MaxGridSize", "Grid", 100, new ConfigDescription("Largest size along any one axis (width, height or depth). Can be raised to 1000.", new AcceptableValueRange<int>(1, 1000)));
		MaxCells = Bind(config, "Size", "MaxCells", "Grid", 100000, new ConfigDescription("Largest total number of pieces in one placement. Can be raised to 1,000,000, but that many pieces takes minutes to place, cannot be cancelled and makes a world heavy.", new AcceptableValueRange<int>(1, 1000000)));
		GapModifier = Bind(config, "Gap", "GapModifier", "Controls", KeyCode.G, "Hold together with the width key (X), the depth key (Z), or both (height), and press Up/Down or scroll, to change the gap on that axis instead of the size.");
		GapStep = Bind(config, "Gap", "GapIncrement", "Grid", 0.1f, new ConfigDescription("Gap change per key press, in meters.", new AcceptableValueRange<float>(0.01f, 5f)));
		GapBigStep = Bind(config, "Gap", "GapBigStep", "Grid", 0.5f, new ConfigDescription("Gap change per key press while the big step modifier (Shift) is held, in meters.", new AcceptableValueRange<float>(0.01f, 10f)));
		MaxGap = Bind(config, "Gap", "MaxGap", "Grid", 10f, new ConfigDescription("Largest gap between pieces, in meters, either direction (negative overlaps them).", new AcceptableValueRange<float>(0f, 50f)));
		KeyStagger = Bind(config, "Stagger", "KeyStagger", "Controls", KeyCode.B, "Hold together with the width key (X) to stagger along X, the depth key (Z) along Z, or both to stagger up (Y), and press Up/Down or scroll: each piece shifts a little further than the one before, along whichever way the grid runs.");
		StaggerStep = Bind(config, "Stagger", "StaggerStep", "Grid", 0.25f, new ConfigDescription("Stagger change per key press, in meters.", new AcceptableValueRange<float>(0.05f, 2f)));
		MaxStagger = Bind(config, "Stagger", "MaxStagger", "Grid", 10f, new ConfigDescription("Largest stagger per row, column or layer, in meters (either direction).", new AcceptableValueRange<float>(0.25f, 50f)));
		KeyRotation = Bind(config, "Rotation", "KeyRotation", "Controls", KeyCode.R, "Hold, then press Up/Down or scroll, to turn each piece a bit more than the one before it (pieces stay flush): alone about the vertical (yaw), with the width key (X) about X (pitch), with the depth key (Z) about Z (roll), along whichever way the grid runs. Vanilla's holster is blocked while this is held with a piece showing.");
		KeyRotationAxis = Bind(config, "Rotation", "KeyRotationAxis", "Controls", KeyCode.T, "Choose which axis the rotation key turns (D77): auto (columns if the grid is more than one wide, else rows, else layers), then columns (X), rows (Z), layers (Y). Each press says the new choice on screen; the readout shows it.");
		RotationStep = Bind(config, "Rotation", "RotationIncrement", "Grid", 0.5f, new ConfigDescription("Degrees added per piece for each key press of the rotation control.", new AcceptableValueRange<float>(0.05f, 45f)));
		RotationBigStep = Bind(config, "Rotation", "RotationBigStep", "Grid", 5f, new ConfigDescription("Degrees added per piece for each key press of the rotation control while the big step modifier (Shift) is held.", new AcceptableValueRange<float>(0.05f, 45f)));
		KeyNudgeForward = Bind(config, "Nudge", "KeyNudgeForward", null, KeyCode.UpArrow, "Nudge the held grid away from the camera (D59). Only while no axis or rotation key is held. Nudging a grid that is not held yet holds it first.");
		KeyNudgeBack = Bind(config, "Nudge", "KeyNudgeBack", null, KeyCode.DownArrow, "Nudge the held grid towards the camera.");
		KeyNudgeLeft = Bind(config, "Nudge", "KeyNudgeLeft", "Controls", KeyCode.LeftArrow, "Nudge the held grid left.");
		KeyNudgeRight = Bind(config, "Nudge", "KeyNudgeRight", "Controls", KeyCode.RightArrow, "Nudge the held grid right.");
		KeyNudgeRaise = Bind(config, "Nudge", "KeyNudgeRaise", "Controls", KeyCode.PageUp, "Nudge the held grid straight up.");
		KeyNudgeLower = Bind(config, "Nudge", "KeyNudgeLower", "Controls", KeyCode.PageDown, "Nudge the held grid straight down.");
		NudgeStep = Bind(config, "Nudge", "NudgeStep", "Grid", 0.1f, new ConfigDescription("How far one nudge moves a held grid, in meters.", new AcceptableValueRange<float>(0.01f, 5f)));
		NudgeBigStep = Bind(config, "Nudge", "NudgeBigStep", "Grid", 1f, new ConfigDescription("How far one nudge moves a held grid while the big step modifier (Shift) is held, in meters.", new AcceptableValueRange<float>(0.01f, 20f)));
		KeyHold = Bind(config, "Hold", "KeyHold", "Controls", KeyCode.H, "Hold: press to freeze the grid or blueprint where it is (so you can walk around and look), press again to release.");
		HoldReach = Bind(config, "Hold", "HoldReach", null, 100f, new ConfigDescription("How far away (in meters) you can still place a held grid or blueprint. It also has to be inside the area the game has loaded around you, whatever this is set to.", new AcceptableValueRange<float>(5f, 1000f)));
		KeyTerrain = Bind(config, "Follow terrain", "KeyTerrain", "Controls", KeyCode.L, "Press to turn follow terrain on or off: each piece in the grid then rises and falls with the ground under it instead of staying level with the first piece.");
		UndoKey = Bind(config, "Undo", "UndoKey", "Controls", KeyCode.Z, "Press together with the grid modifier (Ctrl) to undo the last grid.");
		KeyPanel = Bind(config, "Control panel", "KeyPanel", "Controls", KeyCode.K, "Press to open or close the control panel, where the size, gap, rotation and stagger can be typed in. The mouse is freed while it is open.");
		KeyBlueprintSelect = Bind(config, "Blueprints", "KeyBlueprintSelect", "Controls", KeyCode.F, "While building, aim at a built piece and press this to add it to (or remove it from) the blueprint you are putting together. Hold Shift to do the whole group it was placed with, or Alt for it and every piece connected to it. This is vanilla's Guardian Power key, so Guardian Power is blocked while you are building.");
		KeyBlueprintSave = Bind(config, "Blueprints", "KeyBlueprintSave", "Controls", KeyCode.S, "Press together with the grid modifier (Ctrl) to name and save the current blueprint selection - the same as the K panel's Save... button.");
		KeyDeleteBlueprint = Bind(config, "Blueprints", "KeyDeleteBlueprint", "Controls", KeyCode.Delete, "With a saved blueprint selected in the build menu (its ghost showing), press this to delete it.");
		KeyDesigner = Bind(config, "Blueprints", "KeyDesigner", null, KeyCode.J, "Designer box (D74): tap to start a box that follows the piece you are aiming (its corner at the piece's lowest corner), tap again (or place a piece) to set it down (everything inside it joins the blueprint selection; F still works). A tap on a set-down box asks: Save, Discard or Turn off. Hold it to turn the box off (the selection is kept). While it follows, the width / depth / height keys resize it.");
		DesignerMaxSize = Bind(config, "Blueprints", "DesignerMaxSize", null, 64f, new ConfigDescription("The largest the designer box can be on each side, in meters.", new AcceptableValueRange<float>(8f, 256f)));
		DesignerSelectsRepair = Bind(config, "Blueprints", "DesignerSelectsRepair", null, defaultValue: true, "Starting the designer box (D76) switches the hammer to Repair, so no piece ghost shows or gets placed while you aim the box; the piece you had comes back when the box is set down. Off: the box follows whatever piece is selected.");
		SelectReach = Bind(config, "Blueprints", "SelectReach", null, 50f, new ConfigDescription("How far away (in meters) F, Shift+F and Alt+F can select a built piece, and middle-click can delete a selection.", new AcceptableValueRange<float>(2f, 200f)));
		ShowHud = Bind(config, "Display", "ShowHud", null, defaultValue: true, "Show SmartBuild's on-screen readout (size, gap, piece count, blocked) and controls legend while a piece is showing. The K panel, the ghost preview and the outline are not affected. Turn it on or off in the K panel.");
		ShowControlsHint = Bind(config, "Display", "ShowControlsHint", "Grid", defaultValue: true, "Show the controls legend on the left of the screen while the hammer shows a piece (only when ShowHud is also on).");
		PanelShowControls = Bind(config, "Display", "PanelShowControls", null, defaultValue: true, "Show the list of every shortcut in the right-hand column of the K panel. Turn it off for a narrower panel; the K panel has a checkbox for it.");
		PlacePerFrame = Bind(config, "Performance", "PlacePerFrame", null, 25, new ConfigDescription("Pieces spawned per frame when placing a grid.", new AcceptableValueRange<int>(1, 500)));
		PreviewMaxCells = Bind(config, "Performance", "PreviewMaxCells", null, 400, new ConfigDescription("Largest grid drawn as real ghost pieces. Bigger grids show an outline.", new AcceptableValueRange<int>(1, 2000)));
		FullCostList = Bind(config, "Display", "FullCostList", null, defaultValue: true, "SnakeHammer: list every material a grid or blueprint needs, with how many you have, in the build panel's description. The icon slots under it only fit a few materials, so a blueprint with more would otherwise hide the rest.");
		LogPlacement = Bind(config, "Debug", "LogPlacement", null, defaultValue: true, "SnakeHammer: write one line to the BepInEx log for every grid or blueprint placement: where the preview was, where the first piece landed, and a warning if another mod moved the ghost after the preview was drawn.");
	}

	private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, string oldSection, T defaultValue, string description)
	{
		return Bind(config, section, key, oldSection, defaultValue, new ConfigDescription(description, null));
	}

	private static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, string oldSection, T defaultValue, ConfigDescription description)
	{
		ConfigEntry<T> configEntry = config.Bind(SectionPrefix + section, key, defaultValue, description);
		if (_imported != null && (_imported.TryGetValue(section + "\n" + key, out var value) || (oldSection != null && _imported.TryGetValue(oldSection + "\n" + key, out value))))
		{
			try
			{
				configEntry.SetSerializedValue(value);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Could not import " + section + "/" + key + ": " + ex.Message);
			}
		}
		return configEntry;
	}
}
