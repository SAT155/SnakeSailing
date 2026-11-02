#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class GameApi
{
	public static bool IsClipping(Player player, GameObject ghost)
	{
		return Safe.Call(() => player.TestGhostClipping(ghost, 0.2f), fallback: false, "Player.TestGhostClipping");
	}

	public static bool HasWardAccess(Vector3 pos)
	{
		return Safe.Call(() => PrivateArea.CheckAccess(pos, 0f, flash: false), fallback: true, "PrivateArea.CheckAccess");
	}

	public static bool InNoBuildZone(Vector3 pos)
	{
		return Safe.Call(() => Location.IsInsideNoBuildLocation(pos), fallback: false, "Location.IsInsideNoBuildLocation");
	}

	public static bool WrongBiome(Piece piece, Vector3 pos)
	{
		return Safe.Call(() => piece.m_onlyInBiome != 0 && (Heightmap.FindBiome(pos) & piece.m_onlyInBiome) == 0, fallback: false, "Heightmap.FindBiome");
	}

	public static bool IsFree(Player player, Piece piece)
	{
		return Safe.Call(() => player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()), fallback: false, "Piece.FreeBuildKey");
	}

	public static void ConsumeResources(Player player, Piece piece, int pieces)
	{
		Safe.Run(delegate
		{
			player.ConsumeResources(piece.m_resources, 0, -1, pieces);
		}, "Player.ConsumeResources");
	}

	public static void PlacePiece(Player player, Piece piece, Vector3 pos, Quaternion rot, bool doAttack, bool cheated)
	{
		player.PlacePiece(piece, pos, rot, doAttack, cheated);
	}

	public static bool AddItem(Inventory inventory, string prefabName, int amount)
	{
		return Safe.Call(() => inventory.AddItem(prefabName, amount, 1, 0, 0L, "", cheated: false) != null, fallback: false, "Inventory.AddItem");
	}

	public static int CountItems(Inventory inventory, string itemName)
	{
		return Safe.Call(() => inventory.CountItems(itemName), 0, "Inventory.CountItems");
	}

	public static float? GroundHeight(Vector3 point)
	{
		float height;
		return Safe.Call(() => (!ZoneSystem.instance.GetGroundHeight(point, out height)) ? null : new float?(height), null, "ZoneSystem.GetGroundHeight");
	}

	public static Piece AimedPiece(Player player, float reach)
	{
		GameCamera instance = GameCamera.instance;
		if (instance == null || player == null)
		{
			return null;
		}
		float maxDistance = reach + Vector3.Distance(instance.transform.position, player.m_eye.position) + 1f;
		if (!Physics.Raycast(instance.transform.position, instance.transform.forward, out var hitInfo, maxDistance, player.m_removeRayMask))
		{
			return null;
		}
		if (Vector3.Distance(hitInfo.point, player.m_eye.position) > reach)
		{
			return null;
		}
		return hitInfo.collider.GetComponentInParent<Piece>();
	}

	public static float TerrainShift(float anchorGround, Vector3 copyAnchor)
	{
		float? num = GroundHeight(copyAnchor);
		if (!num.HasValue)
		{
			return 0f;
		}
		return num.Value - anchorGround;
	}

	public static Vector3 FollowTerrain(float anchorGround, Vector3 position)
	{
		float? num = GroundHeight(position);
		if (num.HasValue)
		{
			position.y += num.Value - anchorGround;
		}
		return position;
	}

	public static bool IsDead(Player player)
	{
		return Safe.Call(() => player.IsDead(), fallback: false, "Player.IsDead");
	}

	public static bool BuildSearchFocused()
	{
		return Safe.Call(() => Hud.instance != null && Hud.instance.m_buildUi != null && Hud.instance.m_buildUi.SearchFieldFocused, fallback: false, "BuildUi.SearchFieldFocused");
	}

	public static void Holster(Player player)
	{
		Safe.Run(delegate
		{
			player.HideHandItems();
		}, "Humanoid.HideHandItems");
	}

	public static void Message(Player player, string text)
	{
		if (player == null)
		{
			Plugin.Log.LogInfo(text);
			return;
		}
		Safe.Run(delegate
		{
			player.Message(MessageHud.MessageType.Center, text);
		}, "Character.Message");
	}

	public static string Localize(string key)
	{
		return Safe.Call(() => Localization.instance.Localize(key), key, "Localization.Localize");
	}

	public static void Highlight(GameObject ghost, bool valid)
	{
		Safe.Run(delegate
		{
			Piece component = ghost.GetComponent<Piece>();
			if (component != null)
			{
				component.SetInvalidPlacementHeightlight(!valid);
			}
		}, "Piece.SetInvalidPlacementHeightlight");
	}

	public static GameObject CloneGhost(Player player)
	{
		GameObject selectedPrefab = player.GetBuildTool().GetSelectedPrefab();
		if (selectedPrefab == null)
		{
			throw new InvalidOperationException("No selected prefab to clone");
		}
		return CloneGhost(selectedPrefab, player);
	}

	public static GameObject CloneGhost(GameObject prefab, Player player)
	{
		TerrainModifier componentInChildren = prefab.GetComponentInChildren<TerrainModifier>();
		bool enabled = componentInChildren != null && componentInChildren.enabled;
		if (componentInChildren != null)
		{
			componentInChildren.enabled = false;
		}
		bool forceDisableInit = ZNetView.m_forceDisableInit;
		bool forceDisableTerrainOps = TerrainOp.m_forceDisableTerrainOps;
		TerrainOp.m_forceDisableTerrainOps = true;
		ZNetView.m_forceDisableInit = true;
		GameObject gameObject;
		try
		{
			gameObject = UnityEngine.Object.Instantiate(prefab);
			ItemDrop component = gameObject.GetComponent<ItemDrop>();
			if (component != null)
			{
				component.MakePiece();
			}
		}
		finally
		{
			ZNetView.m_forceDisableInit = forceDisableInit;
			TerrainOp.m_forceDisableTerrainOps = forceDisableTerrainOps;
			if (componentInChildren != null)
			{
				componentInChildren.enabled = enabled;
			}
		}
		gameObject.name = prefab.name;
		MakeGhost(player, gameObject);
		return gameObject;
	}

	private static void MakeGhost(Player player, GameObject ghost)
	{
		Destroy<Joint>(ghost);
		Destroy<Rigidbody>(ghost);
		Destroy<ParticleSystemForceField>(ghost);
		Destroy<Demister>(ghost);
		Collider[] componentsInChildren = ghost.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (((1 << collider.gameObject.layer) & player.m_placeRayMask) == 0)
			{
				collider.enabled = false;
			}
		}
		int layer = LayerMask.NameToLayer("ghost");
		Transform[] componentsInChildren2 = ghost.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].gameObject.layer = layer;
		}
		Destroy<TerrainModifier>(ghost);
		Destroy<GuidePoint>(ghost);
		Destroy<LightLod>(ghost);
		Destroy<LightFlicker>(ghost);
		Destroy<Light>(ghost);
		Destroy<WispSpawner>(ghost);
		AudioSource[] componentsInChildren3 = ghost.GetComponentsInChildren<AudioSource>();
		for (int i = 0; i < componentsInChildren3.Length; i++)
		{
			componentsInChildren3[i].enabled = false;
		}
		ZSFX[] componentsInChildren4 = ghost.GetComponentsInChildren<ZSFX>();
		for (int i = 0; i < componentsInChildren4.Length; i++)
		{
			componentsInChildren4[i].enabled = false;
		}
		Windmill[] componentsInChildren5 = ghost.GetComponentsInChildren<Windmill>();
		for (int i = 0; i < componentsInChildren5.Length; i++)
		{
			componentsInChildren5[i].enabled = false;
		}
		ParticleSystem[] componentsInChildren6 = ghost.GetComponentsInChildren<ParticleSystem>();
		for (int i = 0; i < componentsInChildren6.Length; i++)
		{
			componentsInChildren6[i].gameObject.SetActive(value: false);
		}
	}

	private static void Destroy<T>(GameObject root) where T : Component
	{
		T[] componentsInChildren = root.GetComponentsInChildren<T>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
	}
}
