using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// Decorative plants on the hoe (SnakeSkills 1.16, the user's ask: "add all of the regular plant
// types (non item giving, other than wood and resin) to the hoe's tool list ... to spruce up the
// place"). The game's own tree and bush prefabs are found at runtime: trees (TreeBase, they give
// wood/resin) and bushes/shrubs/ferns that drop nothing or only wood/resin. Berry bushes, crops,
// pickables and rocks are left out. Each gets a hoe piece that is only a ghost (a copy with every
// script and network view removed, under an inactive root, like Restore terrain); placing it spawns
// the vanilla prefab itself, so everyone sees it, with or without the mod, and it can be chopped
// or cut down as usual. Build-menu icons are rendered from the model once.
internal static class DecorPlants
{
	private const string Section = "7 - Landscaping";
	private const string Prefix = "SnakeSkills_Decor_";

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<int> treeWoodCost = null!;

	private static readonly Dictionary<GameObject, string> ghosts = new();
	private static readonly List<GameObject> ordered = new();
	private static bool built;
	private static GameObject? root;

	private static readonly Regex plantName = new("bush|shrub|fern|reed", RegexOptions.IgnoreCase);
	private static readonly HashSet<string> woodish = new() { "Wood", "RoundLog", "FineWood", "ElderBark", "YggdrasilWood", "Blackwood", "Resin" };

	public static void Init(SnakeSkillsPlugin mod)
	{
		enabled = mod.config(Section, "Decor Plants", Toggle.On, "If on, the hoe menu also lists the game's trees and decorative bushes (none that give items other than wood or resin). Placing one plants the real thing, which anyone can see and chop down. Re-equip the hoe after changing this.");
		treeWoodCost = mod.config(Section, "Tree Wood Cost", 5, new ConfigDescription("Wood each planted tree costs (bushes are free), so trees aren't free wood. 0 = free.", new AcceptableValueRange<int>(0, 50)));
	}

	internal static bool IsDecor(GameObject go) => ghosts.ContainsKey(go);

	private static PieceTable? HoeTable()
	{
		GameObject? hoe = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Hoe") : null;
		return hoe != null && hoe.GetComponent<ItemDrop>() is { } drop ? drop.m_itemData.m_shared.m_buildPieces : null;
	}

	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
	private static class AddToHoe
	{
		private static void Prefix(PieceTable __instance, HashSet<string> knownRecipies, Player player)
		{
			if (player != Player.m_localPlayer || knownRecipies == null || __instance != HoeTable() || ZNetScene.instance == null)
			{
				return;
			}
			bool on = enabled.Value == Toggle.On && Landscaping.Active;
			if (on && !built)
			{
				Build(__instance);
			}
			foreach (GameObject ghost in ordered)
			{
				string name = ghost.GetComponent<Piece>().m_name;
				if (on)
				{
					if (!__instance.m_pieces.Contains(ghost))
					{
						__instance.m_pieces.Add(ghost);
					}
					knownRecipies.Add(name);
				}
				else
				{
					knownRecipies.Remove(name);
				}
			}
		}
	}

	private static void Build(PieceTable table)
	{
		built = true;
		Piece? effectSource = table.m_pieces.Find(p => p != null && p.name.StartsWith("mud_road"))?.GetComponent<Piece>();
		root = new GameObject("SnakeSkills_DecorPrefabs");
		root.SetActive(false);
		Object.DontDestroyOnLoad(root);

		List<GameObject> trees = new(), bushes = new();
		foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
		{
			if (prefab == null || prefab.GetComponent<ZNetView>() == null)
			{
				continue;
			}
			if (prefab.GetComponent<Piece>() || prefab.GetComponentInChildren<Pickable>(true) || prefab.GetComponent<Plant>() || prefab.GetComponent<ItemDrop>() || prefab.GetComponent<Character>() || prefab.GetComponent<MineRock>() || prefab.GetComponent<MineRock5>() || prefab.GetComponent<Container>())
			{
				continue;
			}
			if (prefab.GetComponent<TreeBase>() != null)
			{
				trees.Add(prefab);
			}
			else if (prefab.GetComponent<Destructible>() != null && plantName.IsMatch(prefab.name) && OnlyWoodDrops(prefab))
			{
				bushes.Add(prefab);
			}
		}
		trees.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
		bushes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

		foreach (GameObject prefab in bushes)
		{
			Add(prefab, false, effectSource);
		}
		foreach (GameObject prefab in trees)
		{
			Add(prefab, true, effectSource);
		}
		Log($"Decor plants: {bushes.Count} bushes and {trees.Count} trees added to the hoe ({string.Join(", ", bushes.ConvertAll(p => p.name))}; {string.Join(", ", trees.ConvertAll(p => p.name))}).");
	}

	private static bool OnlyWoodDrops(GameObject prefab)
	{
		foreach (DropOnDestroyed drops in prefab.GetComponentsInChildren<DropOnDestroyed>(true))
		{
			foreach (DropTable.DropData drop in drops.m_dropWhenDestroyed.m_drops)
			{
				if (drop.m_item != null && !woodish.Contains(drop.m_item.name))
				{
					return false;
				}
			}
		}
		return true;
	}

	private static void Add(GameObject prefab, bool tree, Piece? effectSource)
	{
		try
		{
			GameObject ghost = Object.Instantiate(prefab, root!.transform);
			ghost.name = Prefix + prefab.name;
			// Keep only the looks: every script goes (network view, tree/destructible logic, ...).
			MonoBehaviour[] scripts = ghost.GetComponentsInChildren<MonoBehaviour>(true);
			for (int i = scripts.Length - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(scripts[i]);
			}
			foreach (Rigidbody body in ghost.GetComponentsInChildren<Rigidbody>(true))
			{
				Object.DestroyImmediate(body);
			}

			Piece piece = ghost.AddComponent<Piece>();
			piece.m_name = Pretty(prefab.name);
			int cost = tree ? treeWoodCost.Value : 0;
			piece.m_description = (tree ? "Plants a tree (the game's own, so anyone can chop it)." : "Plants a bush (the game's own).") + " Ctrl+Z takes it back.";
			// The game itself checks and takes these (and stamina) after a placement.
			ItemDrop? woodItem = ObjectDB.instance.GetItemPrefab("Wood")?.GetComponent<ItemDrop>();
			piece.m_resources = cost > 0 && woodItem != null ? new[] { new Piece.Requirement { m_resItem = woodItem, m_amount = cost, m_recover = false } } : new Piece.Requirement[0];
			piece.m_groundOnly = true;
			piece.m_allowedInDungeons = false;
			piece.m_repairPiece = false;
			if (effectSource != null)
			{
				piece.m_placeEffect = effectSource.m_placeEffect;
				piece.m_icon = effectSource.m_icon;
			}
			if (IconRenderer.Render(ghost) is { } icon)
			{
				piece.m_icon = icon;
			}
			ghosts[ghost] = prefab.name;
			ordered.Add(ghost);
		}
		catch (Exception e)
		{
			Log($"Decor plants: skipped {prefab.name}: {e.Message}");
		}
	}

	// "FirTree_small_dead" -> "Fir tree small dead".
	private static string Pretty(string name)
	{
		string spaced = Regex.Replace(name.Replace('_', ' '), "(?<=[a-z])(?=[A-Z0-9])", " ").Trim();
		return spaced.Length > 0 ? char.ToUpper(spaced[0]) + spaced.Substring(1).ToLowerInvariant() : name;
	}

	// Spawns the vanilla prefab where the ghost stands (persistent, owned by us).
	internal static GameObject? Spawn(string prefabName, Vector3 pos, Quaternion rot)
	{
		GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
		if (prefab == null)
		{
			return null;
		}
		if (prefab.GetComponent<ZNetView>() is { } view)
		{
			view.m_persistent = true;
		}
		return Object.Instantiate(prefab, pos, rot);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
	private static class PlantDecor
	{
		private static bool Prefix(Player __instance, Piece piece, Vector3 pos, Quaternion rot, bool doAttack)
		{
			if (__instance != Player.m_localPlayer || piece == null || !ghosts.TryGetValue(piece.gameObject, out string prefabName))
			{
				return true;
			}
			// Stamina, durability and the wood cost are paid by the game once TryPlacePiece returns
			// (Player.UpdatePlacement), even though PlacePiece itself is skipped here.
			int wood = 0;
			foreach (Piece.Requirement req in piece.m_resources)
			{
				wood += req.m_amount;
			}

			if (doAttack && __instance.m_zanim != null)
			{
				__instance.FaceLookDirection();
				__instance.m_zanim.SetTrigger(__instance.GetRightItem()?.m_shared.m_attack?.m_attackAnimation ?? "attack");
			}
			GameObject? placed = Spawn(prefabName, pos, rot);
			if (placed != null)
			{
				piece.m_placeEffect?.Create(pos, rot, null, 1f, -1, default(ZDOID));
				TerrainUndo.RecordPlant(placed, prefabName, pos, rot, __instance.NoCostCheat() ? 0 : wood);
			}
			return false;
		}
	}
}

// Renders a model to a small transparent sprite for the build menu (one-off, synchronous).
internal static class IconRenderer
{
	private const int Layer = 31;
	private const int Size = 128;

	internal static Sprite? Render(GameObject model)
	{
		GameObject? copy = null, camGo = null, lightGo = null;
		RenderTexture? rt = null;
		RenderTexture previous = RenderTexture.active;
		try
		{
			copy = Object.Instantiate(model);
			copy.transform.position = new Vector3(0f, 6000f, 0f);
			copy.transform.rotation = Quaternion.identity;
			Bounds? bounds = null;
			foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
			{
				t.gameObject.layer = Layer;
			}
			foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
			{
				if (r is ParticleSystemRenderer || !r.enabled)
				{
					continue;
				}
				if (bounds is { } b)
				{
					b.Encapsulate(r.bounds);
					bounds = b;
				}
				else
				{
					bounds = r.bounds;
				}
			}
			if (bounds is not { } box || box.size.sqrMagnitude < 0.0001f)
			{
				return null;
			}

			camGo = new GameObject("SnakeSkills_IconCamera");
			Camera cam = camGo.AddComponent<Camera>();
			cam.enabled = false;
			cam.orthographic = true;
			cam.orthographicSize = Mathf.Max(box.extents.y, Mathf.Max(box.extents.x, box.extents.z)) * 1.1f;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
			cam.cullingMask = 1 << Layer;
			Vector3 view = new Vector3(1f, 0.6f, -1f).normalized;
			float distance = box.extents.magnitude * 3f + 1f;
			cam.transform.position = box.center + view * distance;
			cam.transform.LookAt(box.center);
			cam.nearClipPlane = 0.1f;
			cam.farClipPlane = distance * 2f + box.extents.magnitude * 2f;

			lightGo = new GameObject("SnakeSkills_IconLight");
			Light light = lightGo.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.3f;
			light.cullingMask = 1 << Layer;
			lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

			rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
			cam.targetTexture = rt;
			cam.Render();
			RenderTexture.active = rt;
			Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false);
			texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
			texture.Apply();
			cam.targetTexture = null;
			return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
		}
		catch (Exception e)
		{
			SnakeSkillsPlugin.Log("Decor plant icon not rendered: " + e.Message);
			return null;
		}
		finally
		{
			RenderTexture.active = previous;
			if (copy != null)
			{
				Object.DestroyImmediate(copy);
			}
			if (camGo != null)
			{
				Object.DestroyImmediate(camGo);
			}
			if (lightGo != null)
			{
				Object.DestroyImmediate(lightGo);
			}
			if (rt != null)
			{
				rt.Release();
				Object.Destroy(rt);
			}
		}
	}
}
