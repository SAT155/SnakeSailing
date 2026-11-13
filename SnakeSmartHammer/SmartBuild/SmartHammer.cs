#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

/// <summary>
/// SnakeSkills: SmartBuild runs on the vanilla hammer whenever it's out (1.17-1.20 had a smart
/// mode toggle key; removed in 1.22). Upstream had its own Smart Hammer item (a Jotunn CustomItem
/// with a copy of the hammer's piece table); here the saved blueprints are added to the vanilla
/// hammer's table, and every SmartBuild patch checks <see cref="Equipped"/>.
/// </summary>
internal static class SmartHammer
{
	public const string HammerPrefab = "Hammer";

	private static PieceTable _table;

	private static readonly List<GameObject> _added = new List<GameObject>();

	private static int _syncedVersion = -1;

	internal static PieceTable Table => _table;

	public static bool HoldingHammer(Player player)
	{
		if (player == null)
		{
			return false;
		}
		ItemDrop.ItemData rightItem = player.RightItem;
		if (rightItem == null || rightItem.m_dropPrefab == null)
		{
			return false;
		}
		return rightItem.m_dropPrefab.name == HammerPrefab;
	}

	/// <summary>True while SmartBuild should act: the vanilla hammer is out (and the feature is on).
	/// No separate mode: the user found a toggle key confusing (1.22).</summary>
	public static bool Equipped(Player player)
	{
		return Plugin.Active && HoldingHammer(player);
	}

	/// <summary>Puts the blueprint stubs into the vanilla hammer's table while the feature is on,
	/// and takes them out again when it's off. Cheap when nothing changed.</summary>
	internal static void Sync()
	{
		try
		{
			bool want = Plugin.Active;
			if (!want)
			{
				if (_added.Count > 0)
				{
					RemoveAll();
					RefreshAvailability();
				}
				_syncedVersion = -1;
				return;
			}
			PieceTable table = FindHammerTable();
			if (table == null)
			{
				return;
			}
			if (table != _table)
			{
				RemoveAll();
				_table = table;
				_syncedVersion = -1;
			}
			if (_syncedVersion == BlueprintPieces.Version)
			{
				return;
			}
			_syncedVersion = BlueprintPieces.Version;
			RemoveAll();
			foreach (GameObject stub in BlueprintPieces.Stubs)
			{
				if (!_table.m_pieces.Contains(stub))
				{
					_table.m_pieces.Add(stub);
					_added.Add(stub);
				}
			}
			Plugin.Log.LogInfo("Smart hammer: " + _added.Count + " blueprint(s) in the hammer's build menu (blueprint version " + _syncedVersion + ")");
			RefreshAvailability();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("smarthammer-sync", "Could not sync the hammer's blueprint pieces: " + ex);
		}
	}

	/// <summary>Takes one stub out of the table (a deleted blueprint).</summary>
	internal static void Remove(GameObject stub)
	{
		if (stub == null)
		{
			return;
		}
		_added.Remove(stub);
		if (_table != null)
		{
			_table.m_pieces.Remove(stub);
		}
	}

	private static void RemoveAll()
	{
		if (_table != null)
		{
			foreach (GameObject stub in _added)
			{
				_table.m_pieces.Remove(stub);
			}
		}
		_added.Clear();
	}

	private static PieceTable FindHammerTable()
	{
		Player player = Player.m_localPlayer;
		if (HoldingHammer(player) && player.RightItem.m_shared.m_buildPieces != null)
		{
			return player.RightItem.m_shared.m_buildPieces;
		}
		GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(HammerPrefab) : null;
		ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
		return drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
	}

	internal static void RefreshAvailability()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return;
		}
		try
		{
			localPlayer.GetInventory()?.m_onChanged?.Invoke();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("smarthammer-refresh", "Could not refresh the hammer's available pieces: " + ex);
		}
	}
}
