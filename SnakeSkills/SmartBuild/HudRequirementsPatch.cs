#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
internal static class HudRequirementsPatch
{
	// Fork: the full cost text is rebuilt at most this often (the chest-aware affordability probe
	// can be slow with build-from-chest mods, and SetupPieceInfo runs every frame).
	private const float CostTextInterval = 0.5f;

	private static string _costKey;

	private static float _costAt;

	private static string _costText;

	private static void Postfix(Hud __instance, Piece piece)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || !SmartHammer.Equipped(localPlayer))
		{
			return;
		}
		try
		{
			// Fork: any blueprint stub shows its cost, not only the armed one. Upstream fell through
			// to the stub's empty resource list while browsing the build menu, so a blueprint showed
			// no materials until it was selected and its preview had been measured.
			if (piece != null && BlueprintPieces.TryGetBlueprint(Utils.GetPrefabName(piece.gameObject), out var blueprint))
			{
				ShowBlueprint(__instance, localPlayer, blueprint);
			}
			else if (!(piece == null) && GridState.IsGrid)
			{
				bool craft = piece.FreeBuildKey() == GlobalKeys.NoCraftCost;
				int num = Math.Min(piece.m_resources.Length, __instance.m_requirementItems.Length);
				for (int i = 0; i < num; i++)
				{
					InventoryGui.SetupRequirement(__instance.m_requirementItems[i].transform, piece.m_resources[i], localPlayer, craft, 0, GridState.Cells);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("hud", "Requirement display failed: " + ex);
		}
	}

	private static void ShowBlueprint(Hud hud, Player player, Blueprint blueprint)
	{
		bool armed = blueprint == BlueprintPlacer.Active;
		int copies = armed ? Math.Max(1, GridState.Cells) : 1;
		hud.m_buildSelection.text = blueprint.Name;
		string description = blueprint.Items.Count + ((blueprint.Items.Count == 1) ? " piece" : " pieces") + ((copies > 1) ? (" x " + copies + " copies") : "") + ", repeats with the grid controls (X / Z / X+Z)";
		List<(Piece.Requirement, bool)> list = new List<(Piece.Requirement, bool)>();
		Piece host = null;
		// Fork: look the pieces up directly when the preview hasn't measured this blueprint yet
		// (upstream showed nothing until then). Costs only need the prefabs, not their bounds.
		Dictionary<string, Piece> pieces = (armed ? GridPreview.ArmedBlueprintPieces : null) ?? LookUpPieces(blueprint);
		if (pieces != null)
		{
			Dictionary<string, int> counts = blueprint.PieceCounts(copies);
			pieces.TryGetValue(blueprint.Items[0].Prefab, out host);
			bool item = host != null && host.FreeBuildKey() == GlobalKeys.NoCraftCost;
			foreach (Piece.Requirement item2 in GridCost.Combined(player, blueprint, pieces, counts))
			{
				list.Add((item2, item));
			}
		}
		else
		{
			description += "\n<color=#ff6050>Some pieces in this blueprint aren't loaded (missing mod?)</color>";
		}
		for (int i = 0; i < hud.m_requirementItems.Length; i++)
		{
			if (i >= list.Count)
			{
				hud.m_requirementItems[i].SetActive(value: false);
				continue;
			}
			hud.m_requirementItems[i].SetActive(value: true);
			// The amounts are already totals for every copy, so the multiplier stays 1.
			InventoryGui.SetupRequirement(hud.m_requirementItems[i].transform, list[i].Item1, player, list[i].Item2, 0, 1);
		}
		int slots = hud.m_requirementItems.Length;
		if (list.Count > slots && BlueprintCostPanel.Enabled)
		{
			// SnakeSkills: more materials than slots; all of them go in the panel beside the info.
			BlueprintCostPanel.Show(hud, CostEntries(player, blueprint, copies, host, list));
			description += "\n" + list.Count + " materials, all listed in the materials panel.";
		}
		else if (ModConfig.FullCostList.Value && list.Count > 0)
		{
			description += "\n" + CostText(player, blueprint, copies, host, list, slots);
		}
		hud.m_pieceDescription.text = description;
	}

	private static Dictionary<string, Piece> LookUpPieces(Blueprint blueprint)
	{
		if (ZNetScene.instance == null)
		{
			return null;
		}
		Dictionary<string, Piece> dictionary = new Dictionary<string, Piece>();
		foreach (BlueprintItem item in blueprint.Items)
		{
			if (!dictionary.ContainsKey(item.Prefab))
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(item.Prefab);
				Piece piece = ((prefab != null) ? prefab.GetComponent<Piece>() : null);
				if (piece == null)
				{
					return null;
				}
				dictionary[item.Prefab] = piece;
			}
		}
		return dictionary;
	}

	// Every material on one or two lines: "Needs: 144 Wood, 20 Stone (have 12), ...". Ones you
	// can't cover (counting build-from-chest mods, like the placement check does) are red.
	private static string CostText(Player player, Blueprint blueprint, int copies, Piece host, List<(Piece.Requirement, bool)> list, int slots)
	{
		string key = blueprint.Name + "|" + copies + "|" + list.Count;
		if (key == _costKey && Time.time - _costAt < CostTextInterval)
		{
			return _costText;
		}
		Inventory inventory = player.GetInventory();
		StringBuilder stringBuilder = new StringBuilder("Needs: ");
		for (int i = 0; i < list.Count; i++)
		{
			Piece.Requirement req = list[i].Item1;
			if (i > 0)
			{
				stringBuilder.Append(", ");
			}
			string name = GameApi.Localize(req.m_resItem.m_itemData.m_shared.m_name);
			int have = ((inventory != null) ? GameApi.CountItems(inventory, req.m_resItem.m_itemData.m_shared.m_name) : 0);
			bool enough = list[i].Item2 || have >= req.m_amount || (host != null && Safe.Call(() => ProbeOne(player, host, req), fallback: true, "Player.HaveRequirements"));
			string entry = req.m_amount + " " + name + ((have < req.m_amount) ? (" (have " + have + ")") : "");
			stringBuilder.Append(enough ? entry : ("<color=#ff6050>" + entry + "</color>"));
		}
		if (list.Count > slots)
		{
			stringBuilder.Append("  (" + (list.Count - slots) + " more than the icons below show)");
		}
		_costKey = key;
		_costAt = Time.time;
		_costText = stringBuilder.ToString();
		return _costText;
	}

	private static string _entriesKey;

	private static float _entriesAt;

	private static readonly List<BlueprintCostPanel.Entry> _entries = new List<BlueprintCostPanel.Entry>();

	// SnakeSkills: the same numbers as CostText, for the material panel (same refresh interval).
	private static List<BlueprintCostPanel.Entry> CostEntries(Player player, Blueprint blueprint, int copies, Piece host, List<(Piece.Requirement, bool)> list)
	{
		string key = blueprint.Name + "|" + copies + "|" + list.Count;
		if (key == _entriesKey && Time.time - _entriesAt < CostTextInterval)
		{
			return _entries;
		}
		_entries.Clear();
		Inventory inventory = player.GetInventory();
		for (int i = 0; i < list.Count; i++)
		{
			Piece.Requirement req = list[i].Item1;
			ItemDrop.ItemData.SharedData shared = req.m_resItem.m_itemData.m_shared;
			int have = ((inventory != null) ? GameApi.CountItems(inventory, shared.m_name) : 0);
			bool enough = list[i].Item2 || have >= req.m_amount || (host != null && Safe.Call(() => ProbeOne(player, host, req), fallback: true, "Player.HaveRequirements"));
			_entries.Add(new BlueprintCostPanel.Entry
			{
				Icon = (shared.m_icons != null && shared.m_icons.Length > 0) ? shared.m_icons[0] : null,
				Name = GameApi.Localize(shared.m_name),
				Need = req.m_amount,
				Have = have,
				Enough = enough
			});
		}
		_entriesKey = key;
		_entriesAt = Time.time;
		return _entries;
	}

	private static bool ProbeOne(Player player, Piece host, Piece.Requirement req)
	{
		// Only the material counts here, not whether the host piece's crafting station is in range.
		Piece.Requirement[] resources = host.m_resources;
		CraftingStation craftingStation = host.m_craftingStation;
		try
		{
			host.m_resources = new Piece.Requirement[1] { req };
			host.m_craftingStation = null;
			return player.HaveRequirements(host, Player.RequirementMode.CanBuild);
		}
		finally
		{
			host.m_resources = resources;
			host.m_craftingStation = craftingStation;
		}
	}
}
