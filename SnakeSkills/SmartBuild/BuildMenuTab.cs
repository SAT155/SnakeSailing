#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeSkills.SmartBuild;

/// <summary>
/// SnakeSkills: a "Blueprints" tab next to Categories / Materials / Recent / Favorites in the game's
/// own build menu (BuildUi, Deep North). The tab button is a copy of the last vanilla tab, and its
/// piece list is a vanilla <see cref="RecentPieceList"/> that is refilled with the blueprint stubs
/// each time it is shown (it is never saved: BuildUi only saves its own recent/favorite lists).
/// The tab only shows with the vanilla hammer out; the stubs are kept out of Categories/Materials.
/// </summary>
internal static class BuildMenuTab
{
	private const string TabName = "Blueprints";

	private static BuildUi _ui;

	private static RecentPieceList _list;

	private static int _index = -1;

	private static Button _button;

	private static TabHandler.Tab _tab;

	internal static bool Installed => _index >= 0;

	private static bool IsOurs(BuildUi ui)
	{
		return ui != null && ui == _ui && _index >= 0;
	}

	internal static bool Showing
	{
		get
		{
			return IsOurs(_ui) && _ui.gameObject.activeSelf && _ui.m_currentPieceList == _index;
		}
	}

	internal static void Install(BuildUi ui)
	{
		try
		{
			if (ModConfig.BuildMenuTab != null && !ModConfig.BuildMenuTab.Value)
			{
				return;
			}
			if (ui.m_tabContainer == null || ui.m_tabContainer.childCount == 0 || ui.m_tabHandler == null)
			{
				Plugin.Log.LogWarning("Build menu has no tabs to copy; the Blueprints tab is not added (use the blueprint list key)");
				return;
			}
			int index = ui.m_pieceLists.Count;
			if (ui.m_tabHandler.m_tabs.Count != index || ui.m_tabButtons.Count != index)
			{
				Plugin.Log.LogWarning("Build menu tabs (" + ui.m_tabHandler.m_tabs.Count + ") don't match its lists (" + index + "), probably another mod; the Blueprints tab is not added");
				return;
			}
			Transform template = ui.m_tabContainer.GetChild(ui.m_tabContainer.childCount - 1);
			GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, ui.m_tabContainer);
			clone.name = "SnakeSkills_BlueprintsTab";
			SetLabel(clone, TabName);
			Button button = clone.GetComponent<Button>();
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(delegate
			{
				ui.SelectPieceList(index);
			});
			Transform selected = clone.transform.Find("Selected");
			if (selected != null)
			{
				selected.gameObject.SetActive(false);
			}
			_list = new RecentPieceList(TabName, 100000);
			ui.m_pieceLists.Add(_list);
			ui.m_tabButtons.Add(button);
			_tab = new TabHandler.Tab
			{
				m_button = button
			};
			ui.m_tabHandler.m_tabs.Add(_tab);
			_ui = ui;
			_index = index;
			_button = button;
			Plugin.Log.LogInfo("Added the Blueprints tab to the build menu (tab " + (index + 1) + ")");
		}
		catch (Exception ex)
		{
			_index = -1;
			Plugin.LogOnce("buildmenutab", "Could not add the Blueprints tab to the build menu: " + ex);
		}
	}

	/// <summary>Shows the tab only with the vanilla hammer out and the feature on.</summary>
	internal static void UpdateVisibility(BuildUi ui)
	{
		if (!IsOurs(ui))
		{
			return;
		}
		try
		{
			bool show = SmartHammer.Equipped(Player.m_localPlayer);
			_button.gameObject.SetActive(show);
			// TabHandler skips tabs without a button when cycling with Q/E.
			_tab.m_button = show ? _button : null;
			if (!show && ui.m_currentPieceList == _index)
			{
				ui.m_currentPieceList = 0;
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("buildmenutab-show", "Could not update the Blueprints tab: " + ex);
		}
	}

	/// <summary>Refills the tab's list with the current blueprints, A to Z.</summary>
	internal static void Refill(BuildUi ui)
	{
		if (!IsOurs(ui) || ui.m_currentPieceList != _index)
		{
			return;
		}
		List<Piece> pieces = new List<Piece>();
		foreach (GameObject stub in BlueprintPieces.Stubs)
		{
			Piece piece = stub.GetComponent<Piece>();
			if (piece != null)
			{
				pieces.Add(piece);
			}
		}
		pieces.Sort((a, b) => string.Compare(a.m_name, b.m_name, StringComparison.OrdinalIgnoreCase));
		_list.Reset();
		// AddPiece puts each name first, so add them Z to A.
		for (int i = pieces.Count - 1; i >= 0; i--)
		{
			_list.AddPiece(pieces[i].gameObject.name);
		}
	}

	/// <summary>A blueprint picture arrived: show it on the open menu's button at once.</summary>
	internal static void RefreshIcon(Piece stub)
	{
		if (!Showing || stub == null)
		{
			return;
		}
		foreach (BuildUiPieceButton button in _ui.m_pieceButtons)
		{
			if (button != null && button.Piece == stub && button.m_icon != null)
			{
				button.m_icon.sprite = stub.m_icon;
			}
		}
	}

	// The tab's label is a TextMeshPro text (and a second one under "Selected"); set it by
	// reflection so SnakeSkills needs no TextMeshPro reference, and drop the game's Localize
	// component so it doesn't put the copied tab's name back.
	private static void SetLabel(GameObject tab, string text)
	{
		foreach (Component component in tab.GetComponentsInChildren<Component>(includeInactive: true))
		{
			if (component == null)
			{
				continue;
			}
			Type type = component.GetType();
			if (type.Name == "Localize")
			{
				UnityEngine.Object.DestroyImmediate(component);
				continue;
			}
			if (type.Name.IndexOf("TMP", StringComparison.Ordinal) < 0 && type.Name.IndexOf("TextMeshPro", StringComparison.Ordinal) < 0 && !(component is Text))
			{
				continue;
			}
			PropertyInfo property = type.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
			if (property != null && property.CanWrite && property.PropertyType == typeof(string))
			{
				property.SetValue(component, text, null);
			}
		}
	}
}

[HarmonyPatch(typeof(BuildUi), "Awake")]
internal static class BuildUiAwakePatch
{
	private static void Postfix(BuildUi __instance)
	{
		BuildMenuTab.Install(__instance);
	}
}

[HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OpenBuildMenu))]
internal static class BuildUiOpenPatch
{
	private static void Prefix(BuildUi __instance)
	{
		BuildMenuTab.UpdateVisibility(__instance);
	}
}

[HarmonyPatch(typeof(BuildUi), "UpdatePieceButtons")]
internal static class BuildUiPieceButtonsPatch
{
	private static void Prefix(BuildUi __instance)
	{
		try
		{
			BuildMenuTab.Refill(__instance);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("buildmenutab-fill", "Could not fill the Blueprints tab: " + ex);
		}
	}
}

/// <summary>Keeps the blueprint stubs out of the Categories and Materials tabs (they have their
/// own tab). Recent and Favorites only hold what the player put there.</summary>
[HarmonyPatch]
internal static class PieceListHideStubsPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (string typeName in new[] { "ByUsagePieceList", "ByMaterialPieceList" })
		{
			Type type = AccessTools.TypeByName(typeName);
			MethodInfo method = type != null ? AccessTools.Method(type, "GetAvailablePiecesWithTag") : null;
			if (method != null)
			{
				yield return method;
			}
		}
	}

	private static void Postfix(IList<Piece> resultOut)
	{
		if (resultOut == null || !BuildMenuTab.Installed)
		{
			return;
		}
		for (int i = resultOut.Count - 1; i >= 0; i--)
		{
			Piece piece = resultOut[i];
			if (piece != null && BlueprintPieces.TryGetBlueprint(piece.gameObject.name, out _))
			{
				resultOut.RemoveAt(i);
			}
		}
	}
}
