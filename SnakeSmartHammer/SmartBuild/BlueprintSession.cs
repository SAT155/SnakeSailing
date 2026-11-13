#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class BlueprintSession
{
	private static readonly Color OriginColor = new Color(1f, 0.85f, 0.2f);

	private static readonly Color SelectedColor = new Color(0.25f, 0.9f, 1f);

	private static readonly BlueprintSelection<int> Selection = new BlueprintSelection<int>();

	private static readonly Dictionary<int, Piece> Live = new Dictionary<int, Piece>();

	private static bool _lit;

	private static bool _growing;

	private static bool _sweeping;

	private static bool _sweepAdding;

	private static bool _sweepPressedPiece;

	private static int _sweepLastId;

	private static int _sweepChanged;

	private const int GrowPerFrame = 40;

	private const float TouchMargin = 0.01f;

	private static readonly HashSet<int> BoxLast = new HashSet<int>();

	private static readonly HashSet<int> BoxExcluded = new HashSet<int>();

	public static int Count => Selection.Count;

	public static bool Sweeping => _sweeping;

	public static bool IsSelected(Piece piece)
	{
		if (piece != null)
		{
			return Selection.Contains(piece.GetInstanceID());
		}
		return false;
	}

	public static List<Piece> SelectedPieces()
	{
		List<Piece> list = new List<Piece>(Live.Count);
		foreach (Piece value in Live.Values)
		{
			if (value != null)
			{
				list.Add(value);
			}
		}
		return list;
	}

	public static void OnSelectKey(Player player, bool wholeGroup)
	{
		try
		{
			Select(player, wholeGroup);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("blueprint-select", "Selecting a piece failed: " + ex);
		}
	}

	private static void Select(Player player, bool wholeGroup)
	{
		if (BlueprintPlacer.Active != null)
		{
			BlueprintPlacer.Disarm();
		}
		Piece aimed = GameApi.AimedPiece(player, ModConfig.SelectReach.Value);
		if (aimed == null)
		{
			StartSweep(adding: true, 0, pressed: false);
			return;
		}
		if (!TryCapture(player, aimed, out var captured, out var reason))
		{
			GameApi.Message(player, reason);
			StartSweep(adding: true, aimed.GetInstanceID(), pressed: true);
			return;
		}
		int instanceID = aimed.GetInstanceID();
		if (!wholeGroup)
		{
			bool flag = Selection.Toggle(instanceID, captured);
			Track(instanceID, aimed, flag);
			GameApi.Message(player, (flag ? "Added " : "Removed ") + GameApi.Localize(aimed.m_name) + " (" + Selection.Count + " selected)");
			StartSweep(flag, instanceID, pressed: true);
			return;
		}
		List<KeyValuePair<int, WorldPiece>> list = new List<KeyValuePair<int, WorldPiece>>
		{
			new KeyValuePair<int, WorldPiece>(instanceID, captured)
		};
		Dictionary<int, Piece> dictionary = new Dictionary<int, Piece> { { instanceID, aimed } };
		long num = GroupTag.Read(aimed);
		if (num != 0L)
		{
			List<Piece> list2 = new List<Piece>();
			Piece.GetAllPiecesInRadius(aimed.transform.position, 100000f, list2);
			list2.Sort((Piece a, Piece b) => (a.transform.position - aimed.transform.position).sqrMagnitude.CompareTo((b.transform.position - aimed.transform.position).sqrMagnitude));
			foreach (Piece item in list2)
			{
				int instanceID2 = item.GetInstanceID();
				if (instanceID2 != instanceID && GroupTag.Read(item) == num && TryCapture(player, item, out var captured2, out var _))
				{
					list.Add(new KeyValuePair<int, WorldPiece>(instanceID2, captured2));
					dictionary[instanceID2] = item;
				}
			}
		}
		Selection.ToggleGroup(list);
		foreach (KeyValuePair<int, WorldPiece> item2 in list)
		{
			Track(item2.Key, dictionary[item2.Key], Selection.Contains(item2.Key));
		}
		GameApi.Message(player, list.Count + ((list.Count == 1) ? " piece" : " pieces") + " in the group, " + Selection.Count + " selected");
		StartSweep(Selection.Contains(instanceID), instanceID, pressed: true);
	}

	private static void StartSweep(bool adding, int startId, bool pressed)
	{
		_sweeping = true;
		_sweepAdding = adding;
		_sweepPressedPiece = pressed;
		_sweepLastId = startId;
		_sweepChanged = 0;
	}

	public static void SweepStep(Player player)
	{
		if (!_sweeping || player == null)
		{
			return;
		}
		try
		{
			Piece piece = GameApi.AimedPiece(player, ModConfig.SelectReach.Value);
			if (piece == null)
			{
				return;
			}
			int instanceID = piece.GetInstanceID();
			if (instanceID == _sweepLastId)
			{
				return;
			}
			_sweepLastId = instanceID;
			if (_sweepAdding)
			{
				if (!Selection.Contains(instanceID) && TryCapture(player, piece, out var captured, out var _) && Selection.Add(instanceID, captured))
				{
					Track(instanceID, piece, selected: true);
					_sweepChanged++;
				}
			}
			else if (Selection.Remove(instanceID))
			{
				Track(instanceID, piece, selected: false);
				_sweepChanged++;
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("blueprint-sweep", "Sweep-selecting failed: " + ex);
		}
	}

	public static void EndSweep(Player player)
	{
		if (_sweeping)
		{
			_sweeping = false;
			if (_sweepChanged > 0)
			{
				GameApi.Message(player, (_sweepAdding ? "Swept in " : "Swept out ") + _sweepChanged + ((_sweepChanged == 1) ? " piece" : " pieces") + " (" + Selection.Count + " selected)");
			}
			else if (!_sweepPressedPiece)
			{
				GameApi.Message(player, "Aim at a built piece first");
			}
		}
	}

	public static void OnGrowKey(Player player)
	{
		try
		{
			StartGrow(player);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("blueprint-grow", "Growing the selection failed: " + ex);
		}
	}

	private static void StartGrow(Player player)
	{
		if (_growing)
		{
			GameApi.Message(player, "Still growing the selection");
			return;
		}
		Piece piece = GameApi.AimedPiece(player, ModConfig.SelectReach.Value);
		if (piece == null)
		{
			GameApi.Message(player, "Aim at a built piece first");
			return;
		}
		if (!TryCapture(player, piece, out var _, out var reason))
		{
			GameApi.Message(player, reason);
			return;
		}
		bool adding = !Selection.Contains(piece.GetInstanceID());
		Plugin.Instance.StartCoroutine(Grow(player, piece, adding));
	}

	private static IEnumerator Grow(Player player, Piece start, bool adding)
	{
		_growing = true;
		int changed = 0;
		try
		{
			HashSet<int> visited = new HashSet<int> { start.GetInstanceID() };
			Queue<Piece> frontier = new Queue<Piece>();
			frontier.Enqueue(start);
			int sinceYield = 0;
			while (frontier.Count > 0)
			{
				Piece piece = frontier.Dequeue();
				if (piece == null)
				{
					continue;
				}
				if (TryCapture(player, piece, out var captured, out var _))
				{
					int instanceID = piece.GetInstanceID();
					bool flag = Selection.Contains(instanceID);
					if (adding && !flag)
					{
						Selection.Toggle(instanceID, captured);
						Track(instanceID, piece, selected: true);
						changed++;
					}
					else if (!adding && flag)
					{
						Selection.Toggle(instanceID, captured);
						Track(instanceID, piece, selected: false);
						changed++;
					}
					Bounds sourceBounds = ColliderBounds(piece);
					if (sourceBounds.size != Vector3.zero)
					{
						List<Piece> list = new List<Piece>();
						Piece.GetAllPiecesInRadius(piece.transform.position, sourceBounds.extents.magnitude + 8f, list);
						foreach (Piece item in list)
						{
							if (!(item == null))
							{
								int instanceID2 = item.GetInstanceID();
								if (!visited.Contains(instanceID2) && Touches(sourceBounds, item))
								{
									visited.Add(instanceID2);
									frontier.Enqueue(item);
								}
							}
						}
					}
				}
				if (player == null)
				{
					break;
				}
				int num = sinceYield + 1;
				sinceYield = num;
				if (num >= 40)
				{
					sinceYield = 0;
					yield return null;
				}
			}
		}
		finally
		{
			_growing = false;
		}
		if (player != null)
		{
			GameApi.Message(player, changed + ((changed == 1) ? " piece " : " pieces ") + (adding ? "added" : "removed") + " (" + Selection.Count + " selected)");
		}
	}

	public static int SelectInBox(Player player, DesignerBox box)
	{
		List<Piece> list = new List<Piece>();
		float radius = box.Size.magnitude * 0.5f + 4f;
		Piece.GetAllPiecesInRadius(box.Center, radius, list);
		List<Piece> list2 = new List<Piece>();
		List<Vector3> list3 = new List<Vector3>();
		Dictionary<int, WorldPiece> dictionary = new Dictionary<int, WorldPiece>();
		foreach (Piece item in list)
		{
			if (!(item == null))
			{
				Bounds bounds = ColliderBounds(item);
				Vector3 vector = ((bounds.size != Vector3.zero) ? bounds.center : item.transform.position);
				if (box.Contains(vector) && TryCapture(player, item, out var captured, out var _))
				{
					list2.Add(item);
					list3.Add(vector);
					dictionary[item.GetInstanceID()] = captured;
				}
			}
		}
		List<int> list4 = new List<int>();
		Dictionary<int, Piece> dictionary2 = new Dictionary<int, Piece>();
		foreach (int item2 in box.OriginOrder(list3))
		{
			int instanceID = list2[item2].GetInstanceID();
			list4.Add(instanceID);
			dictionary2[instanceID] = list2[item2];
		}
		Dictionary<int, WorldPiece> dictionary3 = new Dictionary<int, WorldPiece>();
		for (int i = 0; i < Selection.Count; i++)
		{
			dictionary3[Selection.Keys[i]] = Selection.Pieces[i];
		}
		List<int> list5 = DesignerBox.MergeSelection(new List<int>(Selection.Keys), BoxLast, BoxExcluded, list4);
		Selection.Clear();
		BoxLast.Clear();
		foreach (int item3 in list5)
		{
			WorldPiece value2;
			if (dictionary.TryGetValue(item3, out var value))
			{
				Selection.Add(item3, value);
				Live[item3] = dictionary2[item3];
				BoxLast.Add(item3);
			}
			else if (dictionary3.TryGetValue(item3, out value2))
			{
				Selection.Add(item3, value2);
			}
		}
		return BoxLast.Count;
	}

	public static void ForgetBox()
	{
		BoxLast.Clear();
		BoxExcluded.Clear();
	}

	private static Bounds ColliderBounds(Piece piece)
	{
		Bounds result = default(Bounds);
		bool flag = false;
		Collider[] componentsInChildren = piece.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!collider.isTrigger)
			{
				if (!flag)
				{
					result = collider.bounds;
					flag = true;
				}
				else
				{
					result.Encapsulate(collider.bounds);
				}
			}
		}
		return result;
	}

	private static bool Touches(Bounds sourceBounds, Piece candidate)
	{
		Bounds bounds = sourceBounds;
		bounds.Expand(0.02f);
		Collider[] componentsInChildren = candidate.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!collider.isTrigger && bounds.Intersects(collider.bounds))
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryCapture(Player player, Piece piece, out WorldPiece captured, out string reason)
	{
		captured = default(WorldPiece);
		if (piece.GetComponent<PrivateArea>() != null)
		{
			reason = "Wards can't go in a blueprint";
			return false;
		}
		if (piece.GetComponent<TeleportWorld>() != null)
		{
			reason = "Portals can't go in a blueprint";
			return false;
		}
		string prefabName = Utils.GetPrefabName(piece.gameObject);
		GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefabName) : null);
		Piece piece2 = ((gameObject != null) ? gameObject.GetComponent<Piece>() : null);
		if (piece2 == null)
		{
			Plugin.Log.LogWarning("Can't capture '" + piece.gameObject.name + "' for a blueprint: prefab name '" + prefabName + "', ZNetScene.GetPrefab found " + (gameObject != null) + ", it has a Piece " + (piece2 != null));
			reason = "That piece can't be copied";
			return false;
		}
		if (!player.IsPieceAvailable(piece2))
		{
			reason = "You haven't unlocked that piece yet";
			return false;
		}
		captured = new WorldPiece(prefabName, piece.transform.position, piece.transform.rotation);
		reason = null;
		return true;
	}

	private static void Track(int id, Piece piece, bool selected)
	{
		if (selected)
		{
			Live[id] = piece;
			return;
		}
		Live.Remove(id);
		ResetLook(piece);
	}

	public static void Highlight()
	{
		if (Live.Count == 0)
		{
			return;
		}
		try
		{
			MaterialMan instance = MaterialMan.instance;
			if (instance == null)
			{
				return;
			}
			_lit = true;
			List<int> list = null;
			int num = ((Selection.Count > 0) ? Selection.Keys[0] : 0);
			foreach (KeyValuePair<int, Piece> item in Live)
			{
				if (item.Value == null)
				{
					(list ?? (list = new List<int>())).Add(item.Key);
					continue;
				}
				Color color = ((item.Key == num) ? OriginColor : SelectedColor);
				instance.SetValue(item.Value.gameObject, ShaderProps._EmissionColor, color * 0.5f);
				instance.SetValue(item.Value.gameObject, ShaderProps._Color, color);
			}
			if (list == null)
			{
				return;
			}
			foreach (int item2 in list)
			{
				Live.Remove(item2);
				Selection.Toggle(item2, default(WorldPiece));
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("blueprint-highlight", "Highlighting the selection failed: " + ex);
		}
	}

	public static void ClearHighlight()
	{
		if (!_lit)
		{
			return;
		}
		_lit = false;
		foreach (Piece value in Live.Values)
		{
			ResetLook(value);
		}
	}

	private static void ResetLook(Piece piece)
	{
		if (piece == null)
		{
			return;
		}
		Safe.Run(delegate
		{
			MaterialMan instance = MaterialMan.instance;
			if (!(instance == null))
			{
				instance.ResetValue(piece.gameObject, ShaderProps._Color);
				instance.ResetValue(piece.gameObject, ShaderProps._EmissionColor);
			}
		}, "MaterialMan.ResetValue");
	}

	public static void Clear()
	{
		_sweeping = false;
		ClearHighlight();
		Live.Clear();
		Selection.Clear();
		BoxLast.Clear();
		BoxExcluded.Clear();
	}

	public static string Save(Player player, string name)
	{
		if (Selection.Count == 0)
		{
			return "Select at least one piece first (aim at it and press F)";
		}
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Give the blueprint a name first";
		}
		try
		{
			Blueprint blueprint = Selection.ToBlueprint(name.Trim());
			BlueprintLibrary.Save(blueprint, player);
			Clear();
			return "Saved blueprint '" + blueprint.Name + "' (" + blueprint.Items.Count + ((blueprint.Items.Count == 1) ? " piece)" : " pieces)");
		}
		catch (ArgumentException ex)
		{
			return ex.Message;
		}
		catch (Exception ex2)
		{
			Plugin.LogOnce("blueprint-save", "Saving the blueprint failed: " + ex2);
			return "Could not save the blueprint, see the BepInEx log";
		}
	}

	public static void OnDeleteKey(Player player)
	{
		try
		{
			GameObject placementGhost = player.m_placementGhost;
			if (placementGhost == null || !BlueprintPieces.TryGetBlueprint(placementGhost.name, out var blueprint))
			{
				GameApi.Message(player, "Select a saved blueprint in the build menu first");
				return;
			}
			BlueprintLibrary.Delete(blueprint.Name);
			BlueprintPlacer.Disarm();
			GameApi.Message(player, "Deleted blueprint '" + blueprint.Name + "'");
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("blueprint-delete", "Deleting the blueprint failed: " + ex);
		}
	}
}
