using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;

namespace SnakeSkills;

// Fishing XP by fish. Vanilla Fishing only rises while reeling (FishingFloat.FixedUpdate raises it
// once per second of pulling, twice as often with a fish on), the same for every fish, and
// nothing for the catch. This adds a reward per fish caught and scales the reeling XP by how hard
// the fish on the line is. Fishing is a vanilla skill, so its level is saved by vanilla as usual.
internal static class Fishing
{
	private const string Section = "6 - Fishing";

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<string> fishXp = null!;
	private static ConfigEntry<float> unlistedFishXp = null!;
	private static ConfigEntry<float> catchFactor = null!;
	private static ConfigEntry<float> reelFactor = null!;
	private static ConfigEntry<float> starBonus = null!;

	private static ConfigEntry<float> skillYield = null!;
	private static ConfigEntry<string> skillYieldRecipes = null!;
	private static ConfigEntry<float> starYield = null!;
	private static ConfigEntry<string> starYieldRecipes = null!;

	private static readonly Dictionary<string, float> xpByFish = new();
	private static readonly HashSet<string> skillRecipes = new();
	private static readonly HashSet<string> starRecipes = new();

	// Hardest fish get the most. Prefab names (English names in the config description).
	private const string DefaultFishXp = "Fish1:10, Fish2:15, Fish5:25, Fish3:30, Fish4_cave:40, Fish6:50, Fish7:60, Fish8:75, Fish12:90, Fish9:110, Fish10:130, Fish11:150";

	public static void Init(SnakeSkillsPlugin mod)
	{
		enabled = mod.config(Section, "Fishing XP By Fish", Toggle.On, "If on, catching a fish gives Fishing XP based on the kind of fish, and reeling in a hard fish trains Fishing faster, and crafting with fish gives more with skill and stars (see the Yield settings). Off is vanilla.");
		fishXp = mod.config(Section, "Fish XP", DefaultFishXp, "Fishing XP for catching each kind of fish, as Prefab:XP pairs separated by commas. 1 XP is what vanilla gives for about half a second of reeling in a fish. Prefabs: Fish1 Perch, Fish2 Pike, Fish5 Trollfish, Fish3 Tuna, Fish4_cave Tetra, Fish6 Giant herring, Fish7 Grouper, Fish8 Coral cod, Fish12 Pufferfish, Fish9 Anglerfish, Fish10 Northern salmon, Fish11 Magmafish. A fish not in the list gets Unlisted Fish XP; the log names it when caught.");
		unlistedFishXp = mod.config(Section, "Unlisted Fish XP", 60f, new ConfigDescription("Fishing XP for catching a fish that is not in the Fish XP list (for example fish added by a game update).", new AcceptableValueRange<float>(0f, 1000f)));
		catchFactor = mod.config(Section, "Catch XP Factor", 1f, new ConfigDescription("Multiplies every fish's catch XP. 0 turns the catch reward off.", new AcceptableValueRange<float>(0f, 10f)));
		reelFactor = mod.config(Section, "Reeling XP Per Fish XP", 0.02f, new ConfigDescription("Reeling in a hooked fish trains Fishing (1 + fish XP x this) times as fast as vanilla. With 0.02: Perch 1.2x, Coral cod 2.5x, Magmafish 4x. 0 keeps vanilla reeling XP.", new AcceptableValueRange<float>(0f, 0.2f)));
		starBonus = mod.config(Section, "Bonus Per Star", 0.5f, new ConfigDescription("Extra catch and reeling XP for each star above one on the fish (0.5 = a 3-star fish gives twice the XP).", new AcceptableValueRange<float>(0f, 5f)));

		skillYield = mod.config(Section, "Extra Yield At Fishing 100", 1f, new ConfigDescription("Crafting fish into the recipes in Skill Yield Recipes gives this much more at Fishing 100 (1 = twice as much), scaling with the skill. Rounded to whole items.", new AcceptableValueRange<float>(0f, 5f)));
		skillYieldRecipes = mod.config(Section, "Skill Yield Recipes", "FishRaw, FishAndBreadUncooked", "Prefab names of the crafted items that the Fishing skill makes more of. FishRaw is Raw fish (fillets), FishAndBreadUncooked is Uncooked Fish 'n' Bread.");
		starYield = mod.config(Section, "Yield Per Fish Star", 1f, new ConfigDescription("For the recipes in Star Yield Recipes, each star on the fish used adds this many times the recipe's normal amount (1 = a 2-star Anglerfish makes 2, a 3-star 3, like vanilla fillets). Applied before the skill bonus.", new AcceptableValueRange<float>(0f, 5f)));
		starYieldRecipes = mod.config(Section, "Star Yield Recipes", "FishAndBreadUncooked", "Prefab names of crafted items whose amount grows with the stars of the fish used. Vanilla already does this for fillets (FishRaw) but not for recipes that need other ingredients too, like Fish 'n' Bread.");

		ParseFishXp();
		fishXp.SettingChanged += (_, _) => ParseFishXp();
		ParseRecipes();
		skillYieldRecipes.SettingChanged += (_, _) => ParseRecipes();
		starYieldRecipes.SettingChanged += (_, _) => ParseRecipes();
	}

	private static void ParseRecipes()
	{
		skillRecipes.Clear();
		starRecipes.Clear();
		foreach (string name in skillYieldRecipes.Value.Split(','))
		{
			if (name.Trim().Length > 0)
			{
				skillRecipes.Add(name.Trim());
			}
		}
		foreach (string name in starYieldRecipes.Value.Split(','))
		{
			if (name.Trim().Length > 0)
			{
				starRecipes.Add(name.Trim());
			}
		}
	}

	private static void ParseFishXp()
	{
		xpByFish.Clear();
		foreach (string pair in fishXp.Value.Split(','))
		{
			string[] parts = pair.Split(':');
			if (parts.Length == 2 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float xp))
			{
				xpByFish[parts[0].Trim()] = xp;
			}
			else if (pair.Trim().Length > 0)
			{
				Log($"Fish XP: can't read '{pair.Trim()}', expected Prefab:XP.");
			}
		}
	}

	private static float FishXp(Fish fish, bool logUnlisted)
	{
		string prefab = Utils.GetPrefabName(fish.gameObject);
		if (!xpByFish.TryGetValue(prefab, out float xp))
		{
			xp = unlistedFishXp.Value;
			if (logUnlisted)
			{
				Log($"Caught '{prefab}' ({fish.m_name}), which is not in the Fish XP list; gave Unlisted Fish XP ({xp}).");
			}
		}

		int stars = fish.GetComponent<ItemDrop>() is { } itemDrop ? itemDrop.m_itemData.m_quality : 1;
		return xp * (1 + starBonus.Value * Mathf.Max(0, stars - 1));
	}

	// The fish on the line while FishingFloat.FixedUpdate runs, so its RaiseSkill can be scaled.
	private static Fish? reeling;

	[HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
	private static class TrackReeledFish
	{
		private static void Prefix(FishingFloat __instance)
		{
			reeling = enabled.Value == Toggle.On && __instance.m_nview.IsValid() && __instance.m_nview.IsOwner() ? __instance.GetCatch() : null;
		}

		private static void Finalizer() => reeling = null;
	}

	[HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill), typeof(Skills.SkillType), typeof(float))]
	private static class ScaleReelingXp
	{
		private static void Prefix(Skills.SkillType skill, ref float value)
		{
			if (reeling != null && skill == Skills.SkillType.Fishing)
			{
				value *= 1 + FishXp(reeling, false) * reelFactor.Value;
			}
		}
	}

	[HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
	private static class RewardCatch
	{
		private static void Postfix(Fish fish, Character owner)
		{
			if (enabled.Value == Toggle.Off || owner != Player.m_localPlayer || fish == null)
			{
				return;
			}

			// Read the fish before the reeling flag goes, so the catch itself isn't scaled again.
			Fish? caught = reeling;
			reeling = null;
			float xp = FishXp(fish, true) * catchFactor.Value;
			Log($"Caught {Utils.GetPrefabName(fish.gameObject)}: +{xp:0.#} Fishing XP.");

			// One RaiseSkill can only gain one level (the rest is dropped), so a big reward goes in
			// steps of 1 XP. Each step goes through vanilla's multipliers and level-up message.
			Player player = (Player)owner;
			for (float left = xp; left > 0.001f; left -= 1f)
			{
				player.RaiseSkill(Skills.SkillType.Fishing, Mathf.Min(1f, left));
			}

			reeling = caught;
		}
	}

	// Crafted amount. Recipe.GetAmount is what InventoryGui.DoCrafting adds to the inventory (and
	// checks space for). Vanilla only scales by the fish's stars for one-ingredient recipes like
	// fillets: + (stars - 1) x amount. For other fish recipes the fish taken is the first of that
	// name in the inventory (Inventory.RemoveItem with any quality), so its stars count here.
	[HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
	private static class MoreFromFish
	{
		private static void Postfix(Recipe __instance, int craftMultiplier, ref int __result)
		{
			if (enabled.Value == Toggle.Off || __instance.m_item == null || Player.m_localPlayer is not { } player)
			{
				return;
			}

			string item = __instance.m_item.gameObject.name;
			bool stars = starRecipes.Contains(item) && !__instance.m_requireOnlyOneIngredient;
			bool skill = skillRecipes.Contains(item);
			if (!stars && !skill)
			{
				return;
			}

			float amount = __result;
			if (stars && FirstFish(player, __instance) is { } fish && fish.m_quality > 1)
			{
				amount += (fish.m_quality - 1) * __instance.m_amount * starYield.Value * craftMultiplier;
			}
			if (skill)
			{
				amount *= 1 + player.GetSkillFactor(Skills.SkillType.Fishing) * skillYield.Value;
			}
			__result = Mathf.Max(__result, Mathf.FloorToInt(amount + 0.5f));
		}

		private static ItemDrop.ItemData? FirstFish(Player player, Recipe recipe)
		{
			foreach (Piece.Requirement req in recipe.m_resources)
			{
				if (req.m_resItem == null || req.m_resItem.GetComponent<Fish>() == null)
				{
					continue;
				}
				string name = req.m_resItem.m_itemData.m_shared.m_name;
				foreach (ItemDrop.ItemData item in player.GetInventory().m_inventory)
				{
					if (item.m_shared.m_name == name && item.m_worldLevel >= Game.m_worldLevel)
					{
						return item;
					}
				}
			}
			return null;
		}
	}
}
