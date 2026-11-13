using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Random = UnityEngine.Random;

namespace SnakeSkills;

// Ranching: faster taming and more drops from tamed animals, and at higher levels timers for
// hunger and pregnancy, calm animals while you tame them, and a chance of a starred offspring.
internal static class Ranching
{
	internal const string SkillName = "Ranching";
	private const string Section = "4 - Ranching";

	private static Skill skill = null!;
	private static ConfigEntry<float> dropBonus = null!;
	private static ConfigEntry<float> tamingBonus = null!;
	private static ConfigEntry<int> hungerLevel = null!;
	private static ConfigEntry<int> calmLevel = null!;
	private static ConfigEntry<int> breedingLevel = null!;
	private static ConfigEntry<int> starLevel = null!;
	private static ConfigEntry<int> starChance = null!;
	private static ConfigEntry<float> xpFactor = null!;
	private static ConfigEntry<int> xpLoss = null!;

	public static void Init(SnakeSkillsPlugin mod)
	{
		skill = new Skill(SkillName, "ranching.png");
		skill.Description.English("Tame animals faster and get more from your tamed animals.");
		skill.Configurable = false;

		ConfigurationManagerAttributes level = new() { ShowRangeAsPercent = false };
		dropBonus = mod.config(Section, "Item Drop Factor", 2f, new ConfigDescription("What a tamed animal drops when killed near you at Ranching 100, as a multiple of its normal drops (2 = double). Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 10f)), RanchingGUID, "2 - Ranching");
		tamingBonus = mod.config(Section, "Taming Factor", 2f, new ConfigDescription("How much faster animals tame near you at Ranching 100 (2 = twice as fast). Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 10f)), RanchingGUID, "2 - Ranching");
		hungerLevel = mod.config(Section, "Food Level Requirement", 10, new ConfigDescription("Ranching level that shows how long until a fed tamed animal is hungry again. 0 turns it off.", new AcceptableValueRange<int>(0, 100), level), RanchingGUID, "2 - Ranching");
		calmLevel = mod.config(Section, "Calming Level Requirement", 20, new ConfigDescription("Ranching level from which animals being tamed don't notice you. 0 turns it off.", new AcceptableValueRange<int>(0, 100), level), RanchingGUID, "2 - Ranching");
		breedingLevel = mod.config(Section, "Pregnancy Level Requirement", 40, new ConfigDescription("Ranching level that shows breeding progress and time until birth on tamed animals. 0 turns it off.", new AcceptableValueRange<int>(0, 100), level), RanchingGUID, "2 - Ranching");
		starLevel = mod.config(Section, "Level Up Level Requirement", 50, new ConfigDescription("Ranching level (of anyone nearby) from which offspring can be born one star above their parent. 0 turns it off.", new AcceptableValueRange<int>(0, 100), level), RanchingGUID, "2 - Ranching");
		starChance = mod.config(Section, "Level Up Chance", 5, new ConfigDescription("Percent chance per birth of the extra star, once the level is reached.", new AcceptableValueRange<int>(0, 100)), RanchingGUID, "2 - Ranching");
		xpFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Multiplies all Ranching experience.", new AcceptableValueRange<float>(0.01f, 5f)), RanchingGUID, "3 - Other");
		xpFactor.SettingChanged += (_, _) => skill.SkillGainFactor = xpFactor.Value;
		skill.SkillGainFactor = xpFactor.Value;
		xpLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("Ranching experience lost on death.", new AcceptableValueRange<int>(0, 100)), RanchingGUID, "3 - Other");
		xpLoss.SettingChanged += (_, _) => skill.SkillLoss = xpLoss.Value;
		skill.SkillLoss = xpLoss.Value;
	}

	private static bool Reached(float levelFactor, ConfigEntry<int> requirement) => requirement.Value > 0 && levelFactor * 100f >= requirement.Value - 0.001f;

	[HarmonyPatch(typeof(Tameable), nameof(Tameable.DecreaseRemainingTime))]
	private static class TameFaster
	{
		private static void Prefix(Tameable __instance, ref float time)
		{
			if (Player.GetClosestPlayer(__instance.transform.position, 10f) is not { } tamer)
			{
				return;
			}

			time *= 1f + SkillShare.Level(tamer, SkillName) * (tamingBonus.Value - 1f);
			if (Random.value < 0.1f)
			{
				SkillShare.Raise(tamer, SkillName, 7f);
			}
		}
	}

	[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
	private static class MoreDrops
	{
		private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
		{
			if (__instance.m_character == null || !__instance.m_character.IsTamed() || Player.GetClosestPlayer(__instance.transform.position, 50f) is not { } rancher)
			{
				return;
			}

			// Whole extra sets of drops, plus a chance of one more for the fraction.
			float extra = SkillShare.Level(rancher, SkillName) * (dropBonus.Value - 1f);
			int sets = 1 + Mathf.FloorToInt(extra) + (Random.value < extra - Mathf.Floor(extra) ? 1 : 0);
			for (int i = 0; i < __result.Count; ++i)
			{
				__result[i] = new KeyValuePair<GameObject, int>(__result[i].Key, __result[i].Value * sets);
			}

			SkillShare.Raise(rancher, SkillName, 50f);
		}
	}

	// While an animal that is partly tamed thinks, players with enough Ranching are invisible to it.
	private static bool thinkingTamingAnimal;

	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	private static class NoteTamingAnimal
	{
		private static void Prefix(MonsterAI __instance)
		{
			thinkingTamingAnimal = __instance.m_character != null && __instance.m_character.GetComponent<Tameable>() is { } tameable && !__instance.m_character.IsTamed() && tameable.GetTameness() > 0;
		}

		private static void Finalizer() => thinkingTamingAnimal = false;
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetStealthFactor))]
	private static class CalmAnimals
	{
		private static bool Prefix(Player __instance, ref float __result)
		{
			if (!thinkingTamingAnimal || !Reached(SkillShare.Level(__instance, SkillName), calmLevel))
			{
				return true;
			}

			__result = 0f;
			return false;
		}
	}

	[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
	private static class ShowTimers
	{
		private static void Postfix(Tameable __instance, ref string __result)
		{
			if (Player.m_localPlayer == null || ZNet.instance == null || __instance.m_character == null || !__instance.m_character.IsTamed() || __instance.m_nview.GetZDO() is not { } zdo)
			{
				return;
			}

			float level = Player.m_localPlayer.GetSkillFactor(SkillName);
			DateTime now = ZNet.instance.GetTime();

			if (Reached(level, breedingLevel) && __instance.GetComponent<Procreation>() is { } procreation)
			{
				if (procreation.IsPregnant())
				{
					// Birth comes at the animal's first breeding check after this time.
					DateTime due = new DateTime(zdo.GetLong(ZDOVars.s_pregnant)).AddSeconds(procreation.m_pregnancyDuration);
					__result += due > now ? $", Birth in about {Clock(due - now)}" : ", Birth any moment";
				}
				else if (procreation.m_requiredLovePoints > 0)
				{
					int love = Mathf.Clamp(Mathf.RoundToInt(100f * zdo.GetInt(ZDOVars.s_lovePoints) / procreation.m_requiredLovePoints), 0, 100);
					__result += $", Breeding {love}%";
				}
			}

			if (Reached(level, hungerLevel) && !__instance.IsHungry())
			{
				DateTime hungry = new DateTime(zdo.GetLong(ZDOVars.s_tameLastFeeding)).AddSeconds(__instance.m_fedDuration);
				if (hungry > now)
				{
					__result += $", Hungry in {Clock(hungry - now)}";
				}
			}
		}

		private static string Clock(TimeSpan time) => time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
	}

	[HarmonyPatch(typeof(Procreation), nameof(Procreation.Procreate))]
	private static class StarredOffspring
	{
		private static void Prefix(Procreation __instance, out int __state)
		{
			__state = -1;
			if (starLevel.Value <= 0 || Random.value * 100f >= starChance.Value || !Player.s_players.Any(p => Reached(SkillShare.Level(p, SkillName), starLevel)))
			{
				return;
			}

			int parent = __instance.m_character != null ? __instance.m_character.GetLevel() : Math.Max(1, __instance.m_minOffspringLevel);
			__state = __instance.m_minOffspringLevel;
			__instance.m_minOffspringLevel = Math.Max(__instance.m_minOffspringLevel, Math.Min(MaxCreatureLevel(), parent + 1));
		}

		private static void Finalizer(Procreation __instance, int __state)
		{
			if (__state >= 0)
			{
				__instance.m_minOffspringLevel = __state;
			}
		}
	}

	// Vanilla creatures go up to two stars (level 3). Creature Level & Loot Control raises that.
	private static Func<int>? cllcMaxLevel;
	private static bool cllcChecked;

	private static int MaxCreatureLevel()
	{
		if (!cllcChecked)
		{
			cllcChecked = true;
			if (AccessTools.TypeByName("CreatureLevelControl.API")?.GetMethod("GetMaxLevel", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) is { ReturnType: var type } method && type == typeof(int))
			{
				cllcMaxLevel = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), method);
			}
		}

		try
		{
			return cllcMaxLevel?.Invoke() ?? 3;
		}
		catch
		{
			return 3;
		}
	}
}
