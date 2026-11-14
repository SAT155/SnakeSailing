using System;
using System.Collections.Generic;
using HarmonyLib;
using SkillManager;
using UnityEngine;

namespace SnakeSkills;

// Skill icons taken from the game itself, so they look like Valheim's own: the longship's build
// icon for Sailing, a render of the boar for Ranching, the copper ore icon for Mining and the
// hoe's icon for Landscaping. Nothing from the game is shipped; the pictures are made on the
// player's machine once a world has loaded. The embedded PNGs stay as the fallback.
[HarmonyPatch]
internal static class SkillIcons
{
	private static readonly Dictionary<string, Sprite?> cache = new();

	private static readonly (string skill, Func<Sprite?> source)[] sources =
	{
		(Sailing.SkillName, () => PieceIcon("VikingShip") ?? PieceIcon("Karve")),
		(Ranching.SkillName, () => Rendered("Boar", new Vector3(1f, 0.4f, 0.45f))),
		(Mining.SkillName, () => ItemIcon("CopperOre")),
		("Landscaping", () => ItemIcon("Hoe")),
	};

	[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
	[HarmonyPostfix]
	private static void OnSpawned(Player __instance)
	{
		if (__instance == Player.m_localPlayer)
		{
			Apply(__instance);
		}
	}

	[HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
	[HarmonyPrefix]
	private static void BeforeSkillsDialog(Player player) => Apply(player);

	private static void Apply(Player? player)
	{
		if (player == null || player.GetSkills() is not { } skills || ObjectDB.instance == null || ZNetScene.instance == null)
		{
			return;
		}
		foreach ((string name, Func<Sprite?> source) in sources)
		{
			if (!cache.TryGetValue(name, out Sprite? icon))
			{
				icon = source();
				cache[name] = icon;
				SnakeSkillsPlugin.Log($"Skill icon for {name}: {(icon != null ? "from the game" : "built-in fallback")}");
			}
			if (icon == null)
			{
				continue;
			}
			Skills.SkillType type = Skill.fromName(name);
			foreach (Skills.SkillDef def in skills.m_skills)
			{
				if (def.m_skill == type)
				{
					def.m_icon = icon;
				}
			}
			foreach (Skills.Skill skill in skills.GetSkillList())
			{
				if (skill.m_info?.m_skill == type)
				{
					skill.m_info.m_icon = icon;
				}
			}
		}
	}

	private static Sprite? ItemIcon(string item)
	{
		Sprite[]? icons = ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
		return icons is { Length: > 0 } ? icons[0] : null;
	}

	private static Sprite? PieceIcon(string prefab) => ZNetScene.instance.GetPrefab(prefab)?.GetComponent<Piece>()?.m_icon;

	private static Sprite? Rendered(string prefab, Vector3 view) => ZNetScene.instance.GetPrefab(prefab) is { } go ? IconRenderer.RenderPrefab(go, view) : null;
}
