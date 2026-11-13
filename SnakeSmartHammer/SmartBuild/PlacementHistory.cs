#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class PlacementHistory
{
	private sealed class Record
	{
		public readonly List<GameObject> Placed = new List<GameObject>();

		public bool Charged;
	}

	private static Record _record;

	private static bool _capturing;

	private static long _groupId;

	private static Record _last;

	public static bool Busy { get; private set; }

	public static IReadOnlyList<GameObject> Captured
	{
		get
		{
			IReadOnlyList<GameObject> readOnlyList = _record?.Placed;
			return readOnlyList ?? Array.Empty<GameObject>();
		}
	}

	public static bool TryStart(Player player, string busyMessage)
	{
		if (Busy)
		{
			GameApi.Message(player, busyMessage);
			return false;
		}
		Busy = true;
		return true;
	}

	public static void Finish()
	{
		Busy = false;
	}

	public static void BeginCapture(long groupId)
	{
		_groupId = groupId;
		_record = new Record();
	}

	public static void EnterCaptureWindow()
	{
		_capturing = true;
	}

	public static void ExitCaptureWindow()
	{
		_capturing = false;
	}

	public static void OnPiecePlaced(GameObject placed)
	{
		if (_capturing && _record != null)
		{
			_record.Placed.Add(placed);
			GroupTag.Apply(placed, _groupId);
		}
	}

	public static void EndCapture(bool charged)
	{
		if (_record != null && _record.Placed.Count > 0)
		{
			_record.Charged = charged;
			_last = _record;
		}
		_record = null;
		_capturing = false;
	}

	public static void Undo(Player player)
	{
		if (Busy)
		{
			GameApi.Message(player, "Still placing");
			return;
		}
		if (_last == null)
		{
			GameApi.Message(player, "Nothing to undo");
			return;
		}
		Record last = _last;
		_last = null;
		Busy = true;
		Plugin.Instance.StartCoroutine(RunUndo(player, last));
	}

	private static IEnumerator RunUndo(Player player, Record record)
	{
		int removed = 0;
		int skipped = 0;
		Dictionary<string, int> refund = new Dictionary<string, int>();
		try
		{
			int perFrame = ModConfig.PlacePerFrame.Value;
			for (int i = 0; i < record.Placed.Count; i++)
			{
				GameObject go = record.Placed[i];
				Piece piece = ((go != null) ? go.GetComponent<Piece>() : null);
				ZNetView view = ((go != null) ? go.GetComponent<ZNetView>() : null);
				if (go == null || piece == null || view == null || !Safe.Call(() => view.IsValid(), fallback: false, "ZNetView.IsValid"))
				{
					skipped++;
				}
				else
				{
					if (record.Charged)
					{
						Count(refund, piece.m_resources);
					}
					Safe.Run(delegate
					{
						view.ClaimOwnership();
					}, "ZNetView.ClaimOwnership");
					Safe.Run(delegate
					{
						ZNetScene.instance.Destroy(go);
					}, "ZNetScene.Destroy");
					removed++;
				}
				if (i % perFrame == perFrame - 1)
				{
					yield return null;
				}
			}
			if (removed > 0 && player != null && record.Charged)
			{
				Refund(player, refund);
			}
			GameApi.Message(player, "Undid " + removed + " pieces" + ((skipped > 0) ? (" (" + skipped + " already gone)") : ""));
		}
		finally
		{
			Busy = false;
		}
	}

	private static void Count(Dictionary<string, int> into, Piece.Requirement[] resources)
	{
		foreach (Piece.Requirement requirement in resources)
		{
			if (!(requirement.m_resItem == null) && requirement.m_recover && !requirement.m_upgraderResource && requirement.m_amount > 0)
			{
				string name = requirement.m_resItem.gameObject.name;
				into.TryGetValue(name, out var value);
				into[name] = value + requirement.m_amount;
			}
		}
	}

	private static void Refund(Player player, Dictionary<string, int> amounts)
	{
		Inventory inventory = player.GetInventory();
		foreach (KeyValuePair<string, int> amount in amounts)
		{
			if (amount.Value <= 0 || GameApi.AddItem(inventory, amount.Key, amount.Value))
			{
				continue;
			}
			GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(amount.Key) : null);
			if (gameObject == null)
			{
				continue;
			}
			ItemDrop component = gameObject.GetComponent<ItemDrop>();
			int b = ((!(component != null)) ? 1 : Mathf.Max(1, component.m_itemData.m_shared.m_maxStackSize));
			int num = amount.Value;
			while (num > 0)
			{
				int num2 = Mathf.Min(num, b);
				ItemDrop component2 = UnityEngine.Object.Instantiate(gameObject, player.transform.position + Vector3.up, Quaternion.identity).GetComponent<ItemDrop>();
				if (component2 != null)
				{
					component2.m_itemData.m_stack = num2;
				}
				num -= num2;
			}
		}
	}
}
