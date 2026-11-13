using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Random = UnityEngine.Random;

namespace SnakeSkills;

// Mining: more pickaxe damage and more ore with skill, and from a set level a small chance that a
// hit breaks the whole deposit at once (players can switch that off for themselves).
internal static class Mining
{
	internal const string SkillName = "Mining";
	private const string Section = "5 - Mining";

	private static Skill skill = null!;
	private static ConfigEntry<float> damageBonus = null!;
	private static ConfigEntry<float> yieldBonus = null!;
	private static ConfigEntry<int> shatterLevel = null!;
	private static ConfigEntry<float> shatterChance = null!;
	private static ConfigEntry<KeyboardShortcut> shatterToggleKey = null!;
	private static ConfigEntry<float> xpFactor = null!;
	private static ConfigEntry<int> xpLoss = null!;

	/// <summary>This player's choice: may their hits break whole deposits.</summary>
	internal static bool ExplosiveOn = true;

	public static void Init(SnakeSkillsPlugin mod)
	{
		skill = new Skill(SkillName, "mining.png");
		skill.Description.English("Your pickaxe hits harder and rocks and ore deposits give more.");
		skill.Configurable = false;

		damageBonus = mod.config(Section, "Mining Damage Factor", 3f, new ConfigDescription("Pickaxe damage at Mining 100, as a multiple of normal (3 = three times). Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 10f)), MiningGUID, "2 - Mining");
		yieldBonus = mod.config(Section, "Mining Yield Factor", 2f, new ConfigDescription("What rocks and deposits drop at Mining 100, as a multiple of normal (2 = double). Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 5f)), MiningGUID, "2 - Mining");
		shatterLevel = mod.config(Section, "Mining Explosion Level Requirement", 50, new ConfigDescription("Mining level from which a pickaxe hit can break the whole deposit at once. 0 turns it off.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { ShowRangeAsPercent = false }), MiningGUID, "2 - Mining");
		shatterChance = mod.config(Section, "Mining Explosion Chance", 1f, new ConfigDescription("Percent chance per hit at Mining 100 that the whole deposit breaks. The chance grows from nothing just below the level requirement up to this.", new AcceptableValueRange<float>(0f, 100f)), MiningGUID, "2 - Mining");
		shatterToggleKey = mod.config(Section, "Toggle Explosive Mining Hotkey", new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl), new ConfigDescription("Switches whole-deposit breaks off and on for you."), MiningGUID, "2 - Mining", false);
		xpFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Multiplies all Mining experience.", new AcceptableValueRange<float>(0.01f, 5f)), MiningGUID, "3 - Other");
		xpFactor.SettingChanged += (_, _) => skill.SkillGainFactor = xpFactor.Value;
		skill.SkillGainFactor = xpFactor.Value;
		xpLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("Mining experience lost on death.", new AcceptableValueRange<int>(0, 100)), MiningGUID, "3 - Other");
		xpLoss.SettingChanged += (_, _) => skill.SkillLoss = xpLoss.Value;
		skill.SkillLoss = xpLoss.Value;
	}

	/// <summary>Pickaxe damage multiplier from the Mining skill. Ember Blast uses it too, since its
	/// hits don't pass through <see cref="SEMan.ModifyAttack"/>.</summary>
	internal static float DamageMultiplier(Player player) => 1f + player.GetSkillFactor(SkillName) * (damageBonus.Value - 1f);

	internal static void Update()
	{
		if (Player.m_localPlayer == null || !Player.m_localPlayer.TakeInput() || !shatterToggleKey.Value.IsDown())
		{
			return;
		}

		ExplosiveOn = !ExplosiveOn;
		Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, ExplosiveOn ? "Whole-deposit breaks on" : "Whole-deposit breaks off");
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAttack))]
	private static class HitHarder
	{
		private static void Prefix(SEMan __instance, ref HitData hitData)
		{
			if (__instance.m_character is Player player)
			{
				hitData.m_damage.m_pickaxe *= DamageMultiplier(player);
			}
		}
	}

	// The miner's level (0-1) while a player's hit on rock is being handled, else -1. Drops made
	// during that hit get the yield bonus.
	private static float minerLevel = -1f;

	/// <summary>
	/// Shared start of a player's hit on rock: remembers the miner for the yield bonus, gives
	/// experience, and rolls for a whole-deposit break. Returns false when the deposit was broken
	/// (the original hit is then skipped). <paramref name="started"/> tells the finalizer to clear
	/// the miner again; hits made while breaking a deposit run as part of the first one.
	/// </summary>
	private static bool StartHit(HitData hit, int minToolTier, ZNetView nview, Action breakAll, out bool started)
	{
		started = false;
		if (minerLevel >= 0f || hit.GetAttacker() is not Player miner)
		{
			return true;
		}

		started = true;
		float level = SkillShare.Level(miner, SkillName);
		minerLevel = level;

		if (hit.m_toolTier < minToolTier || hit.m_damage.m_pickaxe <= 0f || nview == null || !nview.IsValid() || !nview.IsOwner())
		{
			return true;
		}

		SkillShare.Raise(miner, SkillName, 1f);

		if (!SkillShare.ExplosiveMining(miner) || !RollShatter(level))
		{
			return true;
		}

		breakAll();
		return false;
	}

	private static bool RollShatter(float level)
	{
		if (shatterLevel.Value <= 0 || level * 100f < shatterLevel.Value)
		{
			return false;
		}

		// Ramps up linearly from 10 levels below the requirement to the full chance at 100.
		float start = (shatterLevel.Value - 10) / 100f;
		float ramp = Mathf.Clamp01((level - start) / Mathf.Max(0.01f, 1f - start));
		return Random.value * 100f < ramp * shatterChance.Value;
	}

	private static HitData BreakHit(HitData cause, Vector3 point, float damage) => new()
	{
		m_damage = { m_damage = damage },
		m_point = point,
		m_toolTier = 1000,
		m_attacker = cause.m_attacker,
	};

	[HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_Damage))]
	private static class HitMineRock5
	{
		private static bool Prefix(MineRock5 __instance, HitData hit, out bool __state) => StartHit(hit, __instance.m_minToolTier, __instance.m_nview, () =>
		{
			for (int i = 0; i < __instance.m_hitAreas.Count; ++i)
			{
				MineRock5.HitArea area = __instance.m_hitAreas[i];
				if (area.m_health > 0f && area.m_collider != null)
				{
					__instance.DamageArea(i, BreakHit(hit, area.m_collider.bounds.center, area.m_health * 10f + 1000f));
				}
			}
		}, out __state);

		private static void Finalizer(bool __state) => EndHit(__state);
	}

	[HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
	private static class HitMineRock
	{
		private static bool Prefix(MineRock __instance, HitData hit, out bool __state) => StartHit(hit, __instance.m_minToolTier, __instance.m_nview, () =>
		{
			Collider[] areas = __instance.m_hitAreas;
			ZDO zdo = __instance.m_nview.GetZDO();
			for (int i = 0; i < areas.Length; ++i)
			{
				float health = zdo.GetFloat("Health" + i, __instance.m_health);
				if (health > 0f && areas[i] != null)
				{
					__instance.RPC_Hit(0L, BreakHit(hit, areas[i].bounds.center, health * 10f + 1000f), i);
				}
			}
		}, out __state);

		private static void Finalizer(bool __state) => EndHit(__state);
	}

	// Rock-like destructibles: a pickaxe hurts them and an axe can't (not trees or stumps).
	internal static bool IsRock(Destructible destructible) => destructible.m_damages.m_pickaxe != HitData.DamageModifier.Immune && destructible.m_damages.m_chop == HitData.DamageModifier.Immune && destructible.m_destructibleType != DestructibleType.Tree;

	[HarmonyPatch(typeof(Destructible), nameof(Destructible.RPC_Damage))]
	private static class HitRockDestructible
	{
		private static bool Prefix(Destructible __instance, HitData hit, out bool __state)
		{
			__state = false;
			return !IsRock(__instance) || StartHit(hit, __instance.m_minToolTier, __instance.m_nview, () => __instance.RPC_Damage(0L, BreakHit(hit, hit.m_point, __instance.m_health * 10f + 1000f)), out __state);
		}

		private static void Finalizer(bool __state) => EndHit(__state);
	}

	private static void EndHit(bool started)
	{
		if (started)
		{
			minerLevel = -1f;
		}
	}

	[HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), typeof(int))]
	private static class MoreOre
	{
		[HarmonyPriority(Priority.VeryLow)]
		private static void Postfix(ref List<GameObject> __result)
		{
			if (minerLevel < 0f || __result.Count == 0)
			{
				return;
			}

			// Each item turns into the multiple's whole part, plus one more by chance for the fraction.
			float multiple = 1f + minerLevel * (yieldBonus.Value - 1f);
			List<GameObject> drops = new(__result.Count * Mathf.CeilToInt(multiple));
			foreach (GameObject item in __result)
			{
				for (int n = Mathf.FloorToInt(multiple + Random.value); n > 0; --n)
				{
					drops.Add(item);
				}
			}
			__result = drops;
		}
	}
}
