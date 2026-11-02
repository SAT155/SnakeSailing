#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class BlueprintPieces
{
	private const string PiecePrefix = "SmartBuildBP_";

	private const string Category = "Blueprints";

	private static readonly Dictionary<string, GameObject> StubsByName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, Blueprint> BlueprintsByPrefabName = new Dictionary<string, Blueprint>();

	private static Sprite _blankIcon;

	public static int Version { get; private set; }

	public static IEnumerable<GameObject> Stubs
	{
		get
		{
			foreach (GameObject value in StubsByName.Values)
			{
				if (value != null)
				{
					yield return value;
				}
			}
		}
	}

	public static Piece StubPiece(string blueprintName)
	{
		if (StubsByName.TryGetValue(blueprintName ?? "", out var stub) && stub != null)
		{
			return stub.GetComponent<Piece>();
		}
		return null;
	}

	public static bool TryGetBlueprint(string prefabName, out Blueprint blueprint)
	{
		return BlueprintsByPrefabName.TryGetValue(prefabName ?? "", out blueprint);
	}

	public static void RegisterAll(IEnumerable<Blueprint> blueprints, Player player)
	{
		foreach (Blueprint blueprint in blueprints)
		{
			Register(blueprint, player);
		}
	}

	public static void Register(Blueprint blueprint, Player player)
	{
		try
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			string text = PrefabNameFor(blueprint.Name);
			GameObject gameObject = StubsByName.TryGetValue(blueprint.Name, out var existing) ? existing : null;
			if (gameObject == null)
			{
				gameObject = CreateStub(text);
				if (gameObject == null)
				{
					Plugin.Log.LogError("Could not add '" + blueprint.Name + "' to the build menu: '" + text + "' could not be created");
					return;
				}
			}
			Decorate(gameObject, blueprint.Name, blueprint.Items.Count + ((blueprint.Items.Count == 1) ? " piece" : " pieces"), blueprint, player);
			// SnakeSkills: no Jotunn "Blueprints" tab; the stubs go in the hammer's Misc tab while
			// smart mode is on (SmartHammer.Sync), and in the blueprint list.
			gameObject.GetComponent<Piece>().m_category = Piece.PieceCategory.Misc;
			StubsByName[blueprint.Name] = gameObject;
			BlueprintThumbnails.Forget(blueprint.Name);
			BlueprintsByPrefabName[text] = blueprint;
			Version++;
			Plugin.Log.LogInfo("Registered blueprint piece '" + text + "' for '" + blueprint.Name + "' (version " + Version + ", " + StubsByName.Count + " stub(s) known)");
			SmartHammer.Sync();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Could not add '" + blueprint.Name + "' to the build menu: " + ex);
		}
	}

	public static void Unregister(string name)
	{
		try
		{
			if (!StubsByName.TryGetValue(name, out var value))
			{
				return;
			}
			StubsByName.Remove(name);
			BlueprintThumbnails.Forget(name);
			if (!(value == null))
			{
				string name2 = value.name;
				BlueprintsByPrefabName.Remove(name2);
				SmartHammer.Remove(value);
				Version++;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Could not remove '" + name + "' from the build menu: " + ex);
		}
	}

	private static GameObject _stubRoot;

	// Stand-in for Jotunn's CreateEmptyPrefab: an empty object under an inactive root, so the
	// stub itself never runs in the world; ghosts instantiated from it are active.
	private static GameObject CreateStub(string name)
	{
		if (_stubRoot == null)
		{
			_stubRoot = new GameObject("SnakeSkills_SmartBuildStubs");
			_stubRoot.SetActive(false);
			UnityEngine.Object.DontDestroyOnLoad(_stubRoot);
		}
		GameObject stub = new GameObject(name);
		stub.transform.SetParent(_stubRoot.transform, worldPositionStays: false);
		return stub;
	}

	private static void Decorate(GameObject stub, string name, string description, Blueprint blueprint, Player player)
	{
		for (int num = stub.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.DestroyImmediate(stub.transform.GetChild(num).gameObject);
		}
		Piece piece = stub.GetComponent<Piece>();
		if (piece == null)
		{
			piece = stub.AddComponent<Piece>();
		}
		piece.m_name = name;
		piece.m_description = description;
		piece.m_icon = IconFor(blueprint);
		piece.m_resources = Array.Empty<Piece.Requirement>();
		BuildChildren(stub, blueprint);
		Dictionary<string, PieceBounds> bounds = null;
		if (player != null)
		{
			BlueprintPlacer.TryMeasure(player, blueprint, out var _, out bounds, out var _);
		}
		BuildAnchorCollider(stub, blueprint, bounds);
		if (bounds != null)
		{
			BuildSnapPoints(stub, blueprint, bounds);
		}
	}

	private static void BuildAnchorCollider(GameObject stub, Blueprint blueprint, Dictionary<string, PieceBounds> bounds)
	{
		GameObject gameObject = new GameObject("SmartBuildAnchor")
		{
			layer = LayerMask.NameToLayer("piece_nonsolid")
		};
		gameObject.transform.SetParent(stub.transform, worldPositionStays: false);
		if (bounds != null)
		{
			PieceBounds value;
			(Vector3 Min, Vector3 Max) tuple = blueprint.LocalBounds((string prefab) => (!bounds.TryGetValue(prefab, out value)) ? default(PieceBounds) : value);
			Vector3 item = tuple.Min;
			Vector3 item2 = tuple.Max;
			Vector3 vector = item2 - item;
			vector = new Vector3(Mathf.Max(vector.x, 0.1f), Mathf.Max(vector.y, 0.1f), Mathf.Max(vector.z, 0.1f));
			BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
			boxCollider.center = (item + item2) * 0.5f;
			boxCollider.size = vector;
		}
		else
		{
			gameObject.AddComponent<SphereCollider>().radius = 0.05f;
		}
	}

	private static void BuildChildren(GameObject stub, Blueprint blueprint)
	{
		bool forceDisableInit = ZNetView.m_forceDisableInit;
		bool forceDisableTerrainOps = TerrainOp.m_forceDisableTerrainOps;
		ZNetView.m_forceDisableInit = true;
		TerrainOp.m_forceDisableTerrainOps = true;
		try
		{
			BuildChildrenCore(stub, blueprint);
		}
		finally
		{
			ZNetView.m_forceDisableInit = forceDisableInit;
			TerrainOp.m_forceDisableTerrainOps = forceDisableTerrainOps;
		}
	}

	private static void BuildChildrenCore(GameObject stub, Blueprint blueprint)
	{
		foreach (BlueprintItem item in blueprint.Items)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(item.Prefab);
			if (!(prefab == null))
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(prefab, stub.transform);
				gameObject.transform.localPosition = item.Position;
				gameObject.transform.localRotation = item.Rotation;
				StripToVisual(gameObject);
			}
		}
	}

	private static void BuildSnapPoints(GameObject stub, Blueprint blueprint, Dictionary<string, PieceBounds> bounds)
	{
		PieceBounds value;
		Func<string, PieceBounds> localBounds = (string prefab) => (!bounds.TryGetValue(prefab, out value)) ? default(PieceBounds) : value;
		(Vector3 Min, Vector3 Max) tuple = blueprint.LocalBounds(localBounds);
		Vector3 item = tuple.Min;
		Vector3 item2 = tuple.Max;
		Vector3[] array = blueprint.OuterSnapPoints(localBounds);
		foreach (Vector3 vector in array)
		{
			GameObject gameObject = new GameObject(Blueprint.SnapPointLabel(vector, item, item2));
			gameObject.tag = "snappoint";
			gameObject.layer = LayerMask.NameToLayer("piece");
			gameObject.transform.SetParent(stub.transform, worldPositionStays: false);
			gameObject.transform.localPosition = vector;
		}
	}

	private static void StripToVisual(GameObject clone)
	{
		MonoBehaviour[] componentsInChildren = clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		foreach (MonoBehaviour monoBehaviour in componentsInChildren)
		{
			if (monoBehaviour != null)
			{
				UnityEngine.Object.DestroyImmediate(monoBehaviour);
			}
		}
		Collider[] componentsInChildren2 = clone.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren2[i]);
		}
	}

	private static Sprite IconFor(Blueprint blueprint)
	{
		foreach (string item in blueprint.PrefabsByUsage())
		{
			GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(item) : null);
			Piece piece = ((gameObject != null) ? gameObject.GetComponent<Piece>() : null);
			if (piece != null && piece.m_icon != null)
			{
				return piece.m_icon;
			}
		}
		return BlankIcon();
	}

	private static Sprite BlankIcon()
	{
		if (_blankIcon != null)
		{
			return _blankIcon;
		}
		Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
		texture2D.SetPixel(0, 0, Color.clear);
		texture2D.Apply();
		_blankIcon = Sprite.Create(texture2D, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
		return _blankIcon;
	}

	private static string PrefabNameFor(string blueprintName)
	{
		StringBuilder stringBuilder = new StringBuilder("SmartBuildBP_".Length + blueprintName.Length);
		stringBuilder.Append("SmartBuildBP_");
		foreach (char c in blueprintName)
		{
			stringBuilder.Append(char.IsLetterOrDigit(c) ? c : '_');
		}
		return stringBuilder.ToString();
	}
}
