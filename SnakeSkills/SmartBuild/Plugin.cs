#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

/// <summary>
/// SnakeSkills: SmartBuild's former BepInEx plugin, now a component on SnakeSkills' plugin object
/// (Update/OnGUI as before). <see cref="Init"/> binds its config, loads the saved blueprints and
/// patches SmartBuild's Harmony classes, unless SmartBuild or SnakeHammer itself is installed.
/// </summary>
internal class Plugin : MonoBehaviour
{
	/// <summary>SmartBuild's and SnakeHammer's GUID. With either installed this part stays off.</summary>
	public const string SmartBuildGuid = "com.codepeople.smartbuild";

	public const string Namespace = "SnakeSkills.SmartBuild";

	internal static Plugin Instance;

	internal static ManualLogSource Log;

	private static bool _installed;

	private static readonly HashSet<string> Logged = new HashSet<string>();

	private bool _keyClashesChecked;

	private KeyCode _repeatKey;

	private float _repeatAt;

	private bool _rotateHeldLast;

	private bool _ctrlHeldLast;

	private bool _ctrlTapClean;

	private float _designerDownAt = -1f;

	private bool _designerHoldFired;

	private GridAxis _lastRotationAxis;

	private bool _lastRotationAxisRan;

	internal static bool RotationAdjusted { get; private set; }

	/// <summary>Installed (no SmartBuild/SnakeHammer next to it) and switched on in the config.</summary>
	internal static bool Active => _installed && ModConfig.Enabled != null && ModConfig.Enabled.Value == SnakeSkillsPlugin.Toggle.On;

	public static void Init(SnakeSkillsPlugin host)
	{
		Log = BepInEx.Logging.Logger.CreateLogSource("SnakeSkills.SmartHammer");
		ModConfig.Init(host.Config);
		if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(SmartBuildGuid))
		{
			Log.LogWarning("SmartBuild or SnakeHammer is installed, so SnakeSkills' smart hammer stays off. Remove it to use the smart hammer on the vanilla hammer (same blueprints folder).");
			return;
		}
		try
		{
			BlueprintLibrary.Load();
		}
		catch (Exception ex3)
		{
			Log.LogError("Could not load the saved blueprints: " + ex3);
		}
		try
		{
			ControlPanel.Init();
		}
		catch (Exception ex2)
		{
			Log.LogError("Could not set up the SmartBuild panel: " + ex2);
		}
		Harmony harmony = new Harmony(SnakeSkillsPlugin.ModGUID + ".smarthammer");
		List<string> list = new List<string>();
		foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
		{
			if (type.Namespace == Namespace && type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length != 0)
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
					list.Add(type.Name);
				}
				catch (Exception ex4)
				{
					Log.LogError("Failed to apply " + type.Name + ": " + ex4);
				}
			}
		}
		Instance = host.gameObject.AddComponent<Plugin>();
		_installed = true;
		Log.LogInfo("Smart hammer (SmartBuild 1.21.0 / SnakeHammer port): applied " + list.Count + " patches: " + string.Join(", ", list));
	}

	internal static void LogOnce(string key, string message)
	{
		if (Logged.Add(key))
		{
			Log.LogError(message);
		}
	}

	private void Update()
	{
		try
		{
			Player localPlayer = Player.m_localPlayer;
			if (!_keyClashesChecked && localPlayer != null)
			{
				_keyClashesChecked = true;
				KeyConflicts.Report();
				BlueprintPieces.RegisterAll(BlueprintLibrary.All, localPlayer);
			}
			GUIManager.Tick();
			SmartHammer.Sync();
			HandleModeKeys(localPlayer);
			if (localPlayer == null || !localPlayer.InPlaceMode() || !SmartHammer.Equipped(localPlayer))
			{
				GridPreview.Hide();
				ControlPanel.Close();
				DesignerPrompt.Close();
				GridHold.Release();
				GridState.ResetAll();
				Wheel.Discard();
				BlueprintSession.Clear();
				BlueprintPlacer.Disarm();
				return;
			}
			if (BlueprintList.IsOpen)
			{
				TrackDesignerKey(localPlayer, active: false);
				return;
			}
			if (UiBlocking())
			{
				ControlPanel.Close();
				TrackDesignerKey(localPlayer, active: false);
				DesignerPrompt.Close();
				return;
			}
			if (DesignerPrompt.IsOpen)
			{
				TrackDesignerKey(localPlayer, active: false);
				BlueprintSession.Highlight();
				if (Keys.Down(KeyCode.Escape))
				{
					DesignerPrompt.Close();
				}
				return;
			}
			TrackRotationHold(localPlayer);
			if (!ControlPanel.Typing)
			{
				TrackCtrlTap(localPlayer);
			}
			BlueprintSession.Highlight();
			TrackDesignerKey(localPlayer, !ControlPanel.IsOpen);
			DesignerSession.Update(localPlayer);
			TrackRotationAxis(localPlayer);
			if (ControlPanel.IsOpen)
			{
				ControlPanel.Refresh();
				if (Keys.Down(ModConfig.KeyPanel.Value) && !ControlPanel.Typing)
				{
					ControlPanel.Close();
				}
				if (Keys.Held(ModConfig.GridModifier.Value) && Keys.Down(ModConfig.KeyBlueprintSave.Value))
				{
					BlueprintSavePrompt.Show(localPlayer);
				}
			}
			else
			{
				HandleKeys(localPlayer);
			}
		}
		catch (Exception ex)
		{
			LogOnce("update", "Key handling failed: " + ex);
		}
	}

	// SnakeSkills: the smart mode toggle and the blueprint list, on the vanilla hammer.
	private void HandleModeKeys(Player player)
	{
		if (!Active)
		{
			if (SmartHammer.On)
			{
				SmartHammer.SetOn(player, on: false, announce: false);
			}
			BlueprintList.Close();
			return;
		}
		if (player == null || !SmartHammer.HoldingHammer(player) || !player.InPlaceMode())
		{
			BlueprintList.Close();
			return;
		}
		if (BlueprintList.IsOpen)
		{
			BlueprintThumbnails.Tick();
			if (Keys.Down(KeyCode.Escape) || (!BlueprintList.Typing && Keys.Down(ModConfig.KeyBlueprintList.Value)))
			{
				BlueprintList.Close();
			}
			return;
		}
		if (UiBlocking() || ControlPanel.Typing || DesignerPrompt.IsOpen)
		{
			return;
		}
		if (Keys.Down(ModConfig.KeySmartMode.Value))
		{
			SmartHammer.SetOn(player, !SmartHammer.On);
		}
		else if (Keys.Down(ModConfig.KeyBlueprintList.Value))
		{
			SmartHammer.SetOn(player, on: true, announce: false);
			ControlPanel.Close();
			BlueprintList.Open(player);
		}
	}

	private void TrackCtrlTap(Player player)
	{
		bool flag = Keys.Held(ModConfig.GridModifier.Value);
		if (flag && !_ctrlHeldLast)
		{
			_ctrlTapClean = !Keys.AnyOtherThanCtrlHeld();
		}
		else if (flag && Keys.AnyOtherThanCtrlHeld())
		{
			_ctrlTapClean = false;
		}
		else if (!flag && _ctrlHeldLast && _ctrlTapClean)
		{
			GridState.ToggleTiltMode();
			string text = GridPreview.KeyName(ModConfig.KeyRotation.Value);
			string text2 = GridPreview.KeyName(ModConfig.GridModifier.Value);
			string text3 = " (+ " + GridPreview.KeyName(ModConfig.AxisX.Value) + " / " + GridPreview.KeyName(ModConfig.AxisZ.Value) + ")";
			GameApi.Message(player, GridState.TiltMode ? ("Switched to whole-grid tilt: hold " + text + text3 + " and press Up / Down to tilt every piece together. Tap " + text2 + " for per-piece rotation") : ("Switched to per-piece rotation: hold " + text + text3 + " and press Up / Down to turn each piece a bit more than the last. Tap " + text2 + " for whole-grid tilt"));
		}
		_ctrlHeldLast = flag;
	}

	private void TrackDesignerKey(Player player, bool active)
	{
		if (!active || !Keys.Held(ModConfig.KeyDesigner.Value))
		{
			if (active && _designerDownAt >= 0f && !_designerHoldFired)
			{
				DesignerSession.OnKey(player);
			}
			_designerDownAt = -1f;
		}
		else if (_designerDownAt < 0f)
		{
			_designerDownAt = Time.time;
			_designerHoldFired = false;
		}
		else if (!_designerHoldFired && Time.time - _designerDownAt >= 0.5f)
		{
			_designerHoldFired = true;
			DesignerSession.TurnOff(player);
		}
	}

	private void TrackRotationAxis(Player player)
	{
		GridAxis rotationAxis = GridState.RotationAxis;
		if (!GridState.RotationAxisChoice.HasValue && !GridState.TiltMode && GridState.IsGrid && GridState.AnnounceAxisChange(_lastRotationAxis, _lastRotationAxisRan, rotationAxis))
		{
			GameApi.Message(player, GridPreview.KeyName(ModConfig.KeyRotation.Value) + " now turns " + GridState.AxisLabel(rotationAxis) + " (was " + GridState.AxisLabel(_lastRotationAxis) + "). " + GridPreview.KeyName(ModConfig.KeyRotationAxis.Value) + " picks the axis");
		}
		_lastRotationAxis = rotationAxis;
		_lastRotationAxisRan = GridState.AxisRuns(rotationAxis);
	}

	private void TrackRotationHold(Player player)
	{
		bool flag = Keys.Held(ModConfig.KeyRotation.Value);
		if (flag && !_rotateHeldLast)
		{
			RotationAdjusted = false;
		}
		else if (!flag && _rotateHeldLast && !RotationAdjusted)
		{
			GameApi.Holster(player);
		}
		_rotateHeldLast = flag;
	}

	private void HandleKeys(Player player)
	{
		if (BlueprintSession.Sweeping)
		{
			if (Keys.Held(ModConfig.KeyBlueprintSelect.Value))
			{
				BlueprintSession.SweepStep(player);
			}
			else
			{
				BlueprintSession.EndSweep(player);
			}
		}
		if (Keys.Held(ModConfig.GridModifier.Value) && Keys.Down(ModConfig.UndoKey.Value))
		{
			PlacementHistory.Undo(player);
			return;
		}
		if (Keys.Held(ModConfig.GridModifier.Value) && Keys.Down(ModConfig.KeyBlueprintSave.Value))
		{
			BlueprintSavePrompt.Show(player);
			return;
		}
		if (Keys.Down(ModConfig.KeyPanel.Value))
		{
			ControlPanel.Toggle();
			return;
		}
		if (Keys.Down(ModConfig.KeyRotationAxis.Value))
		{
			GridState.CycleRotationAxis();
			_lastRotationAxis = GridState.RotationAxis;
			_lastRotationAxisRan = GridState.AxisRuns(_lastRotationAxis);
			string text = GridPreview.KeyName(ModConfig.KeyRotation.Value);
			GameApi.Message(player, (!GridState.RotationAxisChoice.HasValue) ? (text + " turns: auto (whichever way the grid runs, now " + GridState.AxisLabel(GridState.RotationAxis) + ")") : (text + " turns: " + GridState.AxisLabel(GridState.RotationAxis) + ", whatever the grid's size"));
			return;
		}
		if (Keys.Down(ModConfig.KeyTerrain.Value))
		{
			GridState.SetFollowTerrain(!GridState.FollowTerrain);
			GameApi.Message(player, "Follow terrain: " + (GridState.FollowTerrain ? "on" : "off"));
			return;
		}
		if (Keys.Down(ModConfig.KeyHold.Value))
		{
			GridHold.Toggle(player);
			return;
		}
		if (Keys.Down(ModConfig.KeyBlueprintSelect.Value))
		{
			if (Keys.Held(KeyCode.LeftAlt) || Keys.Held(KeyCode.RightAlt))
			{
				BlueprintSession.OnGrowKey(player);
			}
			else
			{
				BlueprintSession.OnSelectKey(player, Keys.Held(ModConfig.BigStepModifier.Value));
			}
			return;
		}
		if (Keys.Down(ModConfig.KeyDeleteBlueprint.Value))
		{
			BlueprintSession.OnDeleteKey(player);
			return;
		}
		bool flag = Keys.Held(ModConfig.KeyRotation.Value);
		bool flag2 = flag && GridState.TiltMode;
		if (!flag && !AxisKeyHeld())
		{
			Wheel.Discard();
			if (!Keys.Held(ModConfig.GapModifier.Value) && !Keys.Held(ModConfig.KeyStagger.Value))
			{
				HandleNudge(player);
			}
			return;
		}
		int num = 0;
		if (Pressed(ModConfig.KeyUp.Value))
		{
			num = 1;
		}
		if (Pressed(ModConfig.KeyDown.Value))
		{
			num = -1;
		}
		if (num == 0)
		{
			num = Wheel.Take(player.m_scrollAmountThreshold);
		}
		if (num == 0)
		{
			return;
		}
		if (flag2)
		{
			RotationAdjusted = true;
			bool flag3 = Keys.Held(ModConfig.BigStepModifier.Value);
			float degrees = (float)num * (flag3 ? ModConfig.RotationBigStep.Value : ModConfig.RotationStep.Value);
			GridState.AdjustTilt(GridState.TiltTarget(Keys.Held(ModConfig.AxisX.Value), Keys.Held(ModConfig.AxisZ.Value)), degrees);
			return;
		}
		if (flag)
		{
			RotationAdjusted = true;
			bool flag4 = Keys.Held(ModConfig.BigStepModifier.Value);
			float degrees2 = (float)num * (flag4 ? ModConfig.RotationBigStep.Value : ModConfig.RotationStep.Value);
			var (axis, turn) = GridState.RotationTarget(Keys.Held(ModConfig.AxisX.Value), Keys.Held(ModConfig.AxisZ.Value));
			GridState.AdjustTurn(axis, turn, degrees2);
			return;
		}
		bool flag5 = Keys.Held(ModConfig.AxisX.Value);
		bool flag6 = Keys.Held(ModConfig.AxisZ.Value);
		bool flag7 = flag5 && flag6;
		bool flag8 = flag5 && !flag6;
		bool flag9 = flag7;
		bool flag10 = flag6 && !flag5;
		if (Keys.Held(ModConfig.KeyStagger.Value))
		{
			GridState.AdjustStagger(GridState.StaggerTarget(flag5, flag6), (float)num * ModConfig.StaggerStep.Value);
		}
		else if (Keys.Held(ModConfig.GapModifier.Value))
		{
			if ((!flag8 || !GridState.HasGapAxis(0)) && (!flag9 || !GridState.HasGapAxis(1)) && (!flag10 || !GridState.HasGapAxis(2)))
			{
				string text2 = (flag8 ? "Width" : (flag9 ? "Height" : "Depth"));
				GameApi.Message(player, text2 + " is 1 piece, so there is no gap to set. Add pieces on that axis first");
			}
			else
			{
				float num2 = (float)num * (Keys.Held(ModConfig.BigStepModifier.Value) ? ModConfig.GapBigStep.Value : ModConfig.GapStep.Value);
				GridState.AdjustGap(flag8 ? num2 : 0f, flag9 ? num2 : 0f, flag10 ? num2 : 0f);
			}
		}
		else if (DesignerSession.CanResize)
		{
			DesignerSession.Resize(flag8 ? num : 0, flag9 ? num : 0, flag10 ? num : 0, Keys.Held(ModConfig.BigStepModifier.Value));
		}
		else
		{
			int num3 = num * ((!Keys.Held(ModConfig.BigStepModifier.Value)) ? 1 : ModConfig.BigStep.Value);
			GridState.AdjustSize(flag8 ? num3 : 0, flag9 ? num3 : 0, flag10 ? num3 : 0);
		}
	}

	private void HandleNudge(Player player)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (Pressed(ModConfig.KeyNudgeForward.Value))
		{
			num = 1;
		}
		else if (Pressed(ModConfig.KeyNudgeBack.Value))
		{
			num = -1;
		}
		else if (Pressed(ModConfig.KeyNudgeRight.Value))
		{
			num2 = 1;
		}
		else if (Pressed(ModConfig.KeyNudgeLeft.Value))
		{
			num2 = -1;
		}
		else if (Pressed(ModConfig.KeyNudgeRaise.Value))
		{
			num3 = 1;
		}
		else if (Pressed(ModConfig.KeyNudgeLower.Value))
		{
			num3 = -1;
		}
		if (num != 0 || num2 != 0 || num3 != 0)
		{
			float step = (Keys.Held(ModConfig.BigStepModifier.Value) ? ModConfig.NudgeBigStep.Value : ModConfig.NudgeStep.Value);
			GridHold.Nudge(player, num2, num, num3, step);
		}
	}

	internal static bool AxisKeyHeld()
	{
		if (SmartHammer.Equipped(Player.m_localPlayer))
		{
			if (!Keys.Held(ModConfig.AxisX.Value))
			{
				return Keys.Held(ModConfig.AxisZ.Value);
			}
			return true;
		}
		return false;
	}

	private bool Pressed(KeyCode key)
	{
		if (Keys.Down(key))
		{
			_repeatKey = key;
			_repeatAt = Time.unscaledTime + 0.4f;
			return true;
		}
		if (Keys.Held(key) && _repeatKey == key && Time.unscaledTime >= _repeatAt)
		{
			_repeatAt = Time.unscaledTime + 0.07f;
			return true;
		}
		return false;
	}

	private static bool UiBlocking()
	{
		if (!Safe(() => Menu.IsVisible()) && !Safe(() => Console.IsVisible()) && !Safe(() => TextInput.IsVisible()) && !Safe(() => Chat.instance != null && Chat.instance.HasFocus()))
		{
			return Safe(() => GameApi.BuildSearchFocused());
		}
		return true;
	}

	private static bool Safe(Func<bool> check)
	{
		try
		{
			return check();
		}
		catch (Exception ex)
		{
			LogOnce("ui", "UI state check unavailable: " + ex.Message);
			return false;
		}
	}

	private void OnGUI()
	{
		try
		{
			GridPreview.Draw(Player.m_localPlayer);
			BlueprintList.Draw(Player.m_localPlayer);
		}
		catch (Exception ex)
		{
			LogOnce("gui", "Readout failed: " + ex);
		}
	}
}
