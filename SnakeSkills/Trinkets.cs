using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using static SnakeSkills.SnakeSkillsPlugin;

namespace SnakeSkills;

// Pulsating Earrings: a permanent eitr bonus while worn, on top of the trinket's vanilla adrenaline
// effect (which is left alone, so ViableTrinkets still works). Max eitr is the sum of food eitr:
// Player.UpdateFood calls GetTotalFoodValue about once a second and hands the eitr total to
// SetMaxEitr, so adding to it raises the cap like food does and drops it within a second of
// taking the earrings off. Regen goes through SEMan.ModifyEitrRegen, where status effects add theirs.
internal static class Trinkets
{
	private const string Section = "8 - Trinkets";

	// Vanilla prefab of the Pulsating Earrings (Mistlands carapace trinket).
	private const string PulsatingEarrings = "TrinketCarapaceEitr";

	private static ConfigEntry<Toggle> earringsEnabled = null!;
	private static ConfigEntry<float> earringsMaxEitr = null!;
	private static ConfigEntry<float> earringsEitrRegen = null!;
	private static ConfigEntry<Toggle> earringsTooltip = null!;

	public static void Init(SnakeSkillsPlugin mod)
	{
		earringsEnabled = mod.config(Section, "Pulsating Earrings Eitr Bonus", Toggle.On, "If on, the Pulsating Earrings give a permanent eitr bonus for as long as they are worn, on top of their normal adrenaline effect.");
		earringsMaxEitr = mod.config(Section, "Pulsating Earrings Max Eitr", 25f, new ConfigDescription("Extra maximum eitr while the earrings are worn, added to what your food gives. Works even with no eitr food, so the earrings alone let you cast a little magic.", new AcceptableValueRange<float>(0f, 200f)));
		earringsEitrRegen = mod.config(Section, "Pulsating Earrings Eitr Regen %", 0f, new ConfigDescription("Extra eitr regeneration while the earrings are worn, always on (not only during the adrenaline effect). 20 = +20% eitr regen. 0 is off.", new AcceptableValueRange<float>(0f, 200f)));
		earringsTooltip = mod.config(Section, "Pulsating Earrings Tooltip", Toggle.On, "If on, the earrings' item description shows the permanent bonus.", synchronizedSetting: false);

		earringsEnabled.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
		earringsMaxEitr.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
		earringsEitrRegen.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
		earringsTooltip.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
	}

	private static bool WearsEarrings(Player player)
	{
		ItemDrop.ItemData? trinket = player.m_trinketItem;
		return earringsEnabled.Value == Toggle.On && trinket?.m_dropPrefab != null && trinket.m_dropPrefab.name == PulsatingEarrings;
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
	private static class AddMaxEitr
	{
		private static void Postfix(Player __instance, ref float eitr)
		{
			if (WearsEarrings(__instance))
			{
				eitr += earringsMaxEitr.Value;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyEitrRegen))]
	private static class AddEitrRegen
	{
		private static void Postfix(SEMan __instance, ref float eitrMultiplier)
		{
			if (__instance.m_character is Player player && WearsEarrings(player))
			{
				eitrMultiplier += earringsEitrRegen.Value / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	private static class ObjectDBAwake
	{
		private static void Postfix(ObjectDB __instance) => ApplyDescription(__instance);
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
	private static class ObjectDBCopyOtherDB
	{
		private static void Postfix(ObjectDB __instance) => ApplyDescription(__instance);
	}

	// Items share their SharedData with the prefab they came from, so editing the prefab's
	// description updates earrings already in inventories too. The original text is kept per
	// SharedData so changing the config never stacks lines.
	private static readonly Dictionary<ItemDrop.ItemData.SharedData, string> originalDescription = new();

	private static void ApplyDescription(ObjectDB? db)
	{
		if (db == null || db.m_items == null || db.m_items.Count == 0)
		{
			return;
		}

		ItemDrop.ItemData.SharedData? shared = db.GetItemPrefab(PulsatingEarrings)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
		if (shared == null)
		{
			Log($"{PulsatingEarrings} not found in ObjectDB; no earrings tooltip line added.");
			return;
		}

		if (!originalDescription.TryGetValue(shared, out string original))
		{
			original = shared.m_description;
			originalDescription[shared] = original;
		}

		string text = original;
		if (earringsEnabled.Value == Toggle.On && earringsTooltip.Value == Toggle.On)
		{
			if (earringsMaxEitr.Value > 0f)
			{
				text += $"\n<color=orange>+{earringsMaxEitr.Value:0.#} max eitr while worn</color>";
			}
			if (earringsEitrRegen.Value > 0f)
			{
				text += $"\n<color=orange>+{earringsEitrRegen.Value:0.#}% eitr regen while worn</color>";
			}
		}
		shared.m_description = text;
	}
}
