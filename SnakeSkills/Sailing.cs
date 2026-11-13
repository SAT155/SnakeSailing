using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;

namespace SnakeSkills;

// Sailing: tougher ships for skilled builders, faster ships for skilled captains, a wider map
// reveal at sea, optional skill gates per ship type, and a push for stuck ships.
internal static class Sailing
{
	internal const string SkillName = "Sailing";
	private const string Section = "2 - Sailing";
	private const string SpeedSection = "3 - Sailing Ship Speed";

	// The builder's Sailing level (0-1), saved on the ship when it is placed. SnakeSkills 1.28 and
	// older used the same key, so ships built before keep their extra health.
	private const string BuilderLevelKey = "Sailing Skill Level";

	private static Skill skill = null!;
	private static ConfigEntry<float> healthBonus = null!;
	private static ConfigEntry<float> revealBonus = null!;
	private static ConfigEntry<float> xpFactor = null!;
	private static ConfigEntry<int> xpLoss = null!;
	private static ConfigEntry<Toggle> pushAllowed = null!;
	private static ConfigEntry<KeyboardShortcut> pushKey = null!;
	private static ConfigEntry<float> pushStrength = null!;

	private sealed class ShipType
	{
		public ConfigEntry<float> Speed = null!;
		public ConfigEntry<int> Paddle = null!;
		public ConfigEntry<int> HalfSail = null!;
		public ConfigEntry<int> FullSail = null!;
	}

	private static readonly Dictionary<string, ShipType> shipTypes = new();

	public static void Init(SnakeSkillsPlugin mod)
	{
		skill = new Skill(SkillName, "sailing.png");
		skill.Description.English("Ships you build are sturdier, ships you steer are faster, and you reveal more of the map while aboard.");
		skill.Configurable = false;

		pushKey = mod.config(Section, "Ship Nudge Modifier Key", new KeyboardShortcut(KeyCode.LeftShift), new ConfigDescription("Hold this and use a ship's ladder to push the ship the way you face. Leave empty to turn pushing off for yourself."), SailingGUID, "1 - General", false);
		revealBonus = mod.config(Section, "Exploration Radius Factor", 5f, new ConfigDescription("Extra map reveal radius on a ship at Sailing 100, as a multiple of the normal radius added on top (5 = six times the radius). Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 10f)), SailingGUID, "3 - Other");
		healthBonus = mod.config(Section, "Ship Health Factor", 2f, new ConfigDescription("Extra health for a ship built at Sailing 100, as a multiple of its normal health added on top (2 = three times the health). Lower levels get a matching share. The builder's level is saved on the ship.", new AcceptableValueRange<float>(1f, 10f)), SailingGUID, "3 - Other");
		xpFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Multiplies all Sailing experience.", new AcceptableValueRange<float>(0.01f, 5f)), SailingGUID, "3 - Other");
		xpFactor.SettingChanged += (_, _) => skill.SkillGainFactor = xpFactor.Value;
		skill.SkillGainFactor = xpFactor.Value;
		xpLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("Sailing experience lost on death.", new AcceptableValueRange<int>(0, 100)), SailingGUID, "3 - Other");
		xpLoss.SettingChanged += (_, _) => skill.SkillLoss = xpLoss.Value;
		skill.SkillLoss = xpLoss.Value;
		pushAllowed = mod.config(Section, "Allow Ship Nudge", Toggle.On, new ConfigDescription("If on, players can push a stuck ship from its ladder (modifier key + use)."), SailingGUID, "3 - Other");
		pushStrength = mod.config(Section, "Ship Nudge Force", 10f, new ConfigDescription("How hard one push is: the speed change in m/s it gives the ship."), SailingGUID, "3 - Other");
	}

	private static int LocalLevel => Mathf.RoundToInt(Player.m_localPlayer.GetSkillFactor(SkillName) * 100f);

	private static ShipType? TypeOf(Ship ship) => ship.GetComponent<Piece>() is { } piece && shipTypes.TryGetValue(piece.m_name, out ShipType type) ? type : null;

	// Ship types are known once the prefabs are loaded (vanilla and modded ships alike).
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	private static class AddShipTypes
	{
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(ZNetScene __instance)
		{
			Localization english = new();
			english.SetupLanguage("English");
			foreach (Piece piece in __instance.m_prefabs.Where(p => p != null && p.GetComponent<Ship>() != null).Select(p => p.GetComponent<Piece>()).Where(p => p != null).OrderBy(p => p.m_name))
			{
				if (shipTypes.ContainsKey(piece.m_name))
				{
					continue;
				}

				string label = english.Localize(piece.m_name);
				string key = CleanKey(label);
				shipTypes[piece.m_name] = new ShipType
				{
					Speed = instance.config(SpeedSection, $"{key} Speed Factor", 1.5f, new ConfigDescription($"Extra sailing speed of a {label} steered at Sailing 100, as a multiple of its normal speed added on top. Lower levels get a matching share.", new AcceptableValueRange<float>(1f, 3f)), SailingGUID, "2 - Ship Speed"),
					Paddle = instance.config(SpeedSection, $"{key} Paddle Requirement", 0, new ConfigDescription($"Sailing level needed to take the helm of a {label}. 0: anyone.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { ShowRangeAsPercent = false }), SailingGUID, "2 - Ship Speed"),
					HalfSail = instance.config(SpeedSection, $"{key} Half Requirement", 0, new ConfigDescription($"Sailing level needed to raise a {label}'s sail halfway. 0: anyone.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { ShowRangeAsPercent = false }), SailingGUID, "2 - Ship Speed"),
					FullSail = instance.config(SpeedSection, $"{key} Full Requirement", 0, new ConfigDescription($"Sailing level needed to raise a {label}'s sail fully. 0: anyone.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { ShowRangeAsPercent = false }), SailingGUID, "2 - Ship Speed"),
				};
			}
		}

		// BepInEx keys can't hold quotes, brackets, '=', '\' or line breaks.
		private static string CleanKey(string name)
		{
			StringBuilder key = new();
			foreach (char c in name)
			{
				if (c is not ('"' or '\'' or '[' or ']' or '=' or '\\' or '\n' or '\r' or '\t'))
				{
					key.Append(c);
				}
			}
			return key.ToString().Trim();
		}
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
	private static class LoadShipHealth
	{
		private static void Prefix(WearNTear __instance)
		{
			if (__instance.GetComponent<Ship>() != null && __instance.GetComponent<ZNetView>()?.GetZDO() is { } zdo)
			{
				__instance.m_health *= 1f + zdo.GetFloat(BuilderLevelKey) * healthBonus.Value;
			}
		}
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnPlaced))]
	private static class BuildShip
	{
		private static void Postfix(WearNTear __instance)
		{
			Player builder = Player.m_localPlayer;
			if (builder == null || __instance.GetComponent<Ship>() == null || __instance.GetComponent<ZNetView>()?.GetZDO() is not { } zdo)
			{
				return;
			}

			float level = builder.GetSkillFactor(SkillName);
			zdo.Set(BuilderLevelKey, level);
			__instance.m_health *= 1f + level * healthBonus.Value;
			builder.RaiseSkill(SkillName, 35f);
		}
	}

	private static Player? Captain(Ship ship)
	{
		if (ship.m_shipControlls == null)
		{
			return null;
		}

		long id = ship.m_shipControlls.GetUser();
		return id == 0 ? null : Player.s_players.FirstOrDefault(p => p.GetPlayerID() == id);
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
	private static class SailFaster
	{
		private static readonly ConditionalWeakTable<Ship, StrongBox<float>> timeUnderSail = new();

		private static void Postfix(Ship __instance, ref Vector3 __result)
		{
			if (Captain(__instance) is not { } captain)
			{
				return;
			}

			if (TypeOf(__instance) is { } type)
			{
				__result *= 1f + SkillShare.Level(captain, SkillName) * type.Speed.Value;
			}

			if (__instance.m_speed == Ship.Speed.Stop)
			{
				return;
			}

			// Half a point of experience per second under way.
			StrongBox<float> time = timeUnderSail.GetValue(__instance, _ => new StrongBox<float>());
			time.Value += Time.fixedDeltaTime;
			if (time.Value >= 1f)
			{
				time.Value -= 1f;
				SkillShare.Raise(captain, SkillName, 0.5f);
			}
		}
	}

	[HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.Interact))]
	private static class HelmRequirement
	{
		private static bool Prefix(ShipControlls __instance)
		{
			if (Player.m_localPlayer == null || __instance.m_ship == null || TypeOf(__instance.m_ship) is not { } type || LocalLevel >= type.Paddle.Value)
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"You need Sailing {type.Paddle.Value} to steer this ship.");
			return false;
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.Forward))]
	private static class SailRequirement
	{
		private static bool Prefix(Ship __instance)
		{
			if (Player.m_localPlayer == null || TypeOf(__instance) is not { } type)
			{
				return true;
			}

			// Forward raises the sail one step: Slow to Half, Half to Full.
			int needed = __instance.m_speed switch
			{
				Ship.Speed.Slow => type.HalfSail.Value,
				Ship.Speed.Half => type.FullSail.Value,
				_ => 0,
			};
			if (LocalLevel >= needed)
			{
				return true;
			}

			string sail = __instance.m_speed == Ship.Speed.Slow ? "halfway" : "fully";
			Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"You need Sailing {needed} to raise this sail {sail}.");
			return false;
		}
	}

	[HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
	private static class RevealMore
	{
		private static void Prefix(ref float radius)
		{
			if (Player.m_localPlayer is { m_attached: true, m_attachedToShip: true } player)
			{
				radius *= 1f + player.GetSkillFactor(SkillName) * revealBonus.Value;
			}
		}
	}

	private static bool CanPush(Player? player) => player != null && pushAllowed.Value == Toggle.On && pushKey.Value.MainKey != KeyCode.None && !player.IsSwimming();

	[HarmonyPatch(typeof(Ladder), nameof(Ladder.Interact))]
	private static class PushShip
	{
		private static float nextPush;

		private static bool Prefix(Ladder __instance)
		{
			Player player = Player.m_localPlayer;
			if (!CanPush(player) || !pushKey.Value.IsPressed() || __instance.GetComponentInParent<Ship>() is not { } ship || ship.GetComponent<Rigidbody>() is not { } body)
			{
				return true;
			}

			if (Time.time < nextPush)
			{
				return false;
			}
			nextPush = Time.time + 1f;

			Vector3 facing = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
			if (Vector3.Dot(ship.transform.position - player.transform.position, facing) <= 0f)
			{
				player.Message(MessageHud.MessageType.Center, "Face the ship to push it.");
				return false;
			}

			body.AddForce(facing * pushStrength.Value, ForceMode.VelocityChange);
			return false;
		}
	}

	[HarmonyPatch(typeof(Ladder), nameof(Ladder.GetHoverText))]
	private static class PushHint
	{
		private static void Postfix(Ladder __instance, ref string __result)
		{
			if (CanPush(Player.m_localPlayer) && __instance.GetComponentInParent<Ship>() != null)
			{
				__result += Localization.instance.Localize($"\n[<color=yellow><b>{pushKey.Value} + $KEY_Use</b></color>] Push ship");
			}
		}
	}
}
