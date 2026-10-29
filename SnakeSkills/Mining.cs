using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Random = UnityEngine.Random;

namespace SnakeSkills;

// Mining skill, from blaxxun-boop's Mining 1.1.8. Only the config layout changed.
internal static class Mining
{
	private const string Section = "5 - Mining";

	private static ConfigEntry<float> miningDamageFactor = null!;
	private static ConfigEntry<float> miningItemYieldFactor = null!;
	private static ConfigEntry<KeyboardShortcut> explosionToggleHotkey = null!;
	private static ConfigEntry<int> explosionMinimumLevel = null!;
	private static ConfigEntry<float> explosionChance = null!;
	private static ConfigEntry<float> experienceGainedFactor = null!;
	private static ConfigEntry<int> experienceLoss = null!;

	private static bool explosiveMining = true;

	private static Skill mining = null!;

	public static void Init(SnakeSkillsPlugin mod)
	{
		mining = new Skill("Mining", "mining.png");
		mining.Description.English("Increases damage done while mining and item yield from ore deposits.");
		mining.Name.German("Bergbau");
		mining.Description.German("Erhöht den Schaden während Bergbau-Aktivitäten und erhöht die Ausbeute von Erzvorkommen.");
		mining.Configurable = false;

		miningDamageFactor = mod.config(Section, "Mining Damage Factor", 3f, new ConfigDescription("Mining damage factor at skill level 100.", new AcceptableValueRange<float>(1f, 10f)), MiningGUID, "2 - Mining");
		miningItemYieldFactor = mod.config(Section, "Mining Yield Factor", 2f, new ConfigDescription("Mining yield factor at skill level 100.", new AcceptableValueRange<float>(1f, 5f)), MiningGUID, "2 - Mining");
		explosionMinimumLevel = mod.config(Section, "Mining Explosion Level Requirement", 50, new ConfigDescription("Minimum required skill level to have a chance for the deposit to explode. 0 is disabled.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { ShowRangeAsPercent = false }), MiningGUID, "2 - Mining");
		explosionChance = mod.config(Section, "Mining Explosion Chance", 1f, new ConfigDescription("Mining explosion chance at skill level 100.", new AcceptableValueRange<float>(0, 100)), MiningGUID, "2 - Mining");
		explosionToggleHotkey = mod.config(Section, "Toggle Explosive Mining Hotkey", new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl), new ConfigDescription("Shortcut to press to toggle explosion chance off and on. Please note that you have to stand still, to toggle this."), MiningGUID, "2 - Mining", false);
		experienceGainedFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Factor for experience gained for the mining skill.", new AcceptableValueRange<float>(0.01f, 5f)), MiningGUID, "3 - Other");
		experienceGainedFactor.SettingChanged += (_, _) => mining.SkillGainFactor = experienceGainedFactor.Value;
		mining.SkillGainFactor = experienceGainedFactor.Value;
		experienceLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("How much experience to lose in the mining skill on death.", new AcceptableValueRange<int>(0, 100)), MiningGUID, "3 - Other");
		experienceLoss.SettingChanged += (_, _) => mining.SkillLoss = experienceLoss.Value;
		mining.SkillLoss = experienceLoss.Value;
	}

	internal static void Update()
	{
		if (explosionToggleHotkey.Value.IsDown())
		{
			explosiveMining = !explosiveMining;
			Debug.Log($"ExplosiveMining: {explosiveMining}");
			Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Explosive Mining: {explosiveMining}");
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	public class PlayerAwake
	{
		private static void Postfix(Player __instance)
		{
			__instance.m_nview.Register("Mining IncreaseSkill", (long _, int factor) => __instance.RaiseSkill("Mining", factor));
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	public class PlayerUpdate
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				__instance.m_nview.GetZDO().Set("Mining Skill Factor", __instance.GetSkillFactor("Mining"));
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAttack))]
	private class IncreaseDamageDone
	{
		private static void Prefix(SEMan __instance, ref HitData hitData)
		{
			if (__instance.m_character is Player player)
			{
				hitData.m_damage.m_pickaxe *= 1 + player.GetSkillFactor("Mining") * (miningDamageFactor.Value - 1);
			}
		}
	}

	[HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), typeof(int))]
	private class IncreaseItemYield
	{
		[UsedImplicitly]
		[HarmonyPriority(Priority.VeryLow)]
		private static void Postfix(ref List<GameObject> __result)
		{
			if (!SetMiningFlag.IsMining)
			{
				return;
			}

			List<GameObject> tmp = new();
			foreach (GameObject item in __result)
			{
				float amount = 1 + SetMiningFlag.MiningFactor * (miningItemYieldFactor.Value - 1);

				for (int num = Mathf.FloorToInt(amount + Random.Range(0f, 1f)); num > 0; --num)
				{
					tmp.Add(item);
				}
			}

			__result = tmp;
		}
	}

	[HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_Damage))]
	public static class SetMiningFlag
	{
		public static bool IsMining = false;
		public static float MiningFactor;

		private static bool Prefix(MineRock5 __instance, HitData hit) => HandleMining(hit, () =>
		{
			for (int i = 0; i < __instance.m_hitAreas.Count; ++i)
			{
				MineRock5.HitArea hitArea = __instance.m_hitAreas[i];
				if (hitArea.m_health > 0)
				{
					__instance.DamageArea(i, new HitData
					{
						m_damage = { m_damage = __instance.m_health },
						m_point = hitArea.m_collider.bounds.center,
						m_toolTier = 100,
						m_attacker = hit.m_attacker,
					});
				}
			}
		}, __instance.m_minToolTier, __instance.m_nview);

		public static bool HandleMining(HitData hit, Action explode, int minToolTier, ZNetView netView)
		{
			if (hit.GetAttacker() is not Player player)
			{
				return true;
			}

			IsMining = true;
			MiningFactor = player.m_nview.GetZDO()?.GetFloat("Mining Skill Factor") ?? 0;

			if (hit.m_toolTier >= minToolTier && hit.m_damage.m_pickaxe > 0 && netView.IsValid() && netView.IsOwner())
			{
				player.m_nview.InvokeRPC("Mining IncreaseSkill", 1);

				if (explosiveMining && explosionMinimumLevel.Value > 0 && MiningFactor >= explosionMinimumLevel.Value / 100f && Random.Range(0f, 1f) <= (MiningFactor - (explosionMinimumLevel.Value - 10) / 100f) / (1 - (explosionMinimumLevel.Value - 10) / 100f) * explosionChance.Value / 100f)
				{
					explode();
					return false;
				}
			}

			return true;
		}

		private static void Finalizer() => IsMining = false;
	}

	[HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
	public static class SetMiningFlagRock
	{
		private static bool Prefix(MineRock __instance, HitData hit) => SetMiningFlag.IsMining || SetMiningFlag.HandleMining(hit, () =>
		{
			for (int i = 0; i < __instance.m_hitAreas.Length; ++i)
			{
				Collider hitArea = __instance.m_hitAreas[i];
				float health = __instance.m_nview.GetZDO().GetFloat($"Health{i}", __instance.m_health);
				if (health > 0)
				{
					__instance.RPC_Hit(0, new HitData
					{
						m_damage = { m_damage = health },
						m_point = hitArea.bounds.center,
						m_toolTier = 100,
						m_attacker = hit.m_attacker,
					}, i);
				}
			}

			__instance.m_hitAreas = Array.Empty<Collider>();
		}, __instance.m_minToolTier, __instance.m_nview);

		private static void Finalizer() => SetMiningFlag.IsMining = false;
	}

	[HarmonyPatch(typeof(Destructible), nameof(Destructible.RPC_Damage))]
	public static class SetMiningFlagDestructible
	{
		private static bool Prefix(Destructible __instance, HitData hit)
		{
			if (!SetMiningFlag.IsMining && __instance.m_damages.m_pickaxe != HitData.DamageModifier.Immune && __instance.m_damages.m_chop == HitData.DamageModifier.Immune && __instance.m_destructibleType != DestructibleType.Tree)
			{
				return SetMiningFlag.HandleMining(hit, () =>
				{
					__instance.Damage(new HitData
					{
						m_damage = { m_damage = __instance.m_health },
						m_point = hit.m_point,
						m_toolTier = 100,
						m_attacker = hit.m_attacker,
					});
				}, __instance.m_minToolTier, __instance.m_nview);
			}

			return true;
		}

		private static void Finalizer() => SetMiningFlag.IsMining = false;
	}
}