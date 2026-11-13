using HarmonyLib;
using SkillManager;
using UnityEngine;

namespace SnakeSkills;

/// <summary>
/// Sailing, Ranching and Mining often run on a machine that isn't the skilled player's: the ship,
/// animal or rock belongs to whoever owns its ZDO. So every player writes their levels (0-1) and
/// their explosive-mining choice to their own player ZDO, and experience is sent back to the
/// player's own client by RPC.
/// </summary>
internal static class SkillShare
{
	private const string RaiseRpc = "SnakeSkills RaiseSkill";
	private const string ExplosiveKey = "SnakeSkills ExplosiveMining";
	private static readonly string[] Shared = { Sailing.SkillName, Ranching.SkillName, Mining.SkillName };

	private static string LevelKey(string skill) => "SnakeSkills " + skill;

	/// <summary>The player's level in a SnakeSkills skill, 0 to 1, read from any machine.</summary>
	internal static float Level(Player? player, string skill)
	{
		if (player == null)
		{
			return 0f;
		}

		if (player == Player.m_localPlayer)
		{
			return player.GetSkillFactor(skill);
		}

		return player.m_nview != null && player.m_nview.GetZDO() is { } zdo ? zdo.GetFloat(LevelKey(skill)) : 0f;
	}

	internal static bool ExplosiveMining(Player player) => player == Player.m_localPlayer ? Mining.ExplosiveOn : player.m_nview != null && player.m_nview.GetZDO() is { } zdo && zdo.GetBool(ExplosiveKey, true);

	/// <summary>Gives the player experience, wherever this runs.</summary>
	internal static void Raise(Player player, string skill, float amount)
	{
		if (player == Player.m_localPlayer)
		{
			player.RaiseSkill(skill, amount);
		}
		else if (player.m_nview != null && player.m_nview.IsValid())
		{
			player.m_nview.InvokeRPC(RaiseRpc, skill, amount);
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	private static class RegisterRaise
	{
		private static void Postfix(Player __instance)
		{
			if (__instance.m_nview == null)
			{
				return;
			}

			Player player = __instance;
			player.m_nview.Register<string, float>(RaiseRpc, (_, skill, amount) =>
			{
				if (player == Player.m_localPlayer)
				{
					player.RaiseSkill(skill, amount);
				}
			});
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	private static class Publish
	{
		private static float next;

		private static void Postfix(Player __instance)
		{
			if (__instance != Player.m_localPlayer || Time.time < next || __instance.m_nview.GetZDO() is not { } zdo)
			{
				return;
			}

			next = Time.time + 0.5f;
			foreach (string skill in Shared)
			{
				zdo.Set(LevelKey(skill), __instance.GetSkillFactor(skill));
			}
			zdo.Set(ExplosiveKey, Mining.ExplosiveOn);
		}
	}
}
