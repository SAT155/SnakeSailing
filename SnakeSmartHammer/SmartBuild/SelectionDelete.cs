#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class SelectionDelete
{
	public static bool Pending { get; private set; }

	public static Piece AimedForRemove(Player player)
	{
		return GameApi.AimedPiece(player, ModConfig.SelectReach.Value);
	}

	public static bool TryIntercept(Player player)
	{
		if (Pending && !Safe.Call(() => UnifiedPopup.IsVisible(), fallback: false, "UnifiedPopup.IsVisible"))
		{
			Pending = false;
		}
		if (Pending)
		{
			return true;
		}
		if (BlueprintSession.Count < 2)
		{
			return false;
		}
		Piece piece = AimedForRemove(player);
		if (piece == null || !BlueprintSession.IsSelected(piece))
		{
			return false;
		}
		return Ask(player);
	}

	private static bool Ask(Player player)
	{
		if (!UnifiedPopup.IsAvailable())
		{
			GameApi.Message(player, "Could not open the delete confirmation, see the BepInEx log");
			Plugin.Log.LogError("Could not confirm a selection delete: no UnifiedPopup in the scene");
			return true;
		}
		int count = BlueprintSession.SelectedPieces().Count;
		Pending = true;
		UnifiedPopup.Push(new YesNoPopup("Delete pieces", "You are about to delete " + count + ((count == 1) ? " piece" : " pieces"), delegate
		{
			Close();
			Confirm(player);
		}, Close, localizeText: false));
		Safe.Run(delegate
		{
			UnifiedPopup.instance.buttonRightText.text = "Confirm";
			UnifiedPopup.instance.buttonLeftText.text = Localization.instance.Localize("$menu_cancel");
		}, "UnifiedPopup button labels");
		return true;
	}

	private static void Close()
	{
		Pending = false;
		Safe.Run(delegate
		{
			if (UnifiedPopup.IsVisible())
			{
				UnifiedPopup.Pop();
			}
		}, "UnifiedPopup.Pop");
	}

	private static void Confirm(Player player)
	{
		if (!(player == null))
		{
			List<Piece> list = BlueprintSession.SelectedPieces();
			if (list.Count == 0)
			{
				GameApi.Message(player, "Nothing selected any more");
			}
			else if (PlacementHistory.TryStart(player, "Still placing"))
			{
				BlueprintSession.Clear();
				Plugin.Instance.StartCoroutine(Run(player, list));
			}
		}
	}

	private static IEnumerator Run(Player player, List<Piece> pieces)
	{
		int deleted = 0;
		int skipped = 0;
		try
		{
			int perFrame = ModConfig.PlacePerFrame.Value;
			for (int i = 0; i < pieces.Count; i++)
			{
				if (player == null)
				{
					break;
				}
				bool flag = false;
				try
				{
					flag = RemoveOne(player, pieces[i]);
				}
				catch (Exception ex)
				{
					Plugin.LogOnce("selection-delete", "Removing a selected piece failed: " + ex);
				}
				if (flag)
				{
					deleted++;
				}
				else
				{
					skipped++;
				}
				if (i % perFrame == perFrame - 1)
				{
					yield return null;
				}
			}
		}
		finally
		{
			PlacementHistory.Finish();
		}
		if (player != null)
		{
			GameApi.Message(player, "Deleted " + deleted + ((deleted == 1) ? " piece" : " pieces") + ((skipped > 0) ? (" (" + skipped + " couldn't be removed)") : ""));
		}
	}

	private static bool RemoveOne(Player player, Piece piece)
	{
		if (piece == null || !piece.m_canBeRemoved)
		{
			return false;
		}
		Vector3 position = piece.transform.position;
		if (Location.IsInsideNoBuildLocation(position))
		{
			return false;
		}
		if (!PrivateArea.CheckAccess(position, 0f, flash: false))
		{
			return false;
		}
		if (!player.CheckCanRemovePiece(piece))
		{
			return false;
		}
		ZNetView component = piece.GetComponent<ZNetView>();
		if (component == null || !component.IsValid())
		{
			return false;
		}
		if (!piece.CanBeRemoved())
		{
			return false;
		}
		piece.GetComponent<IRemoved>()?.OnRemoved();
		WearNTear component2 = piece.GetComponent<WearNTear>();
		if (component2 != null)
		{
			component2.Remove();
			return true;
		}
		Character component3 = piece.GetComponent<Character>();
		if (component3 != null)
		{
			component3.Damage(new HitData(1E+10f));
			return true;
		}
		component.ClaimOwnership();
		piece.DropResources();
		ZNetScene.instance.Destroy(piece.gameObject);
		return true;
	}
}
