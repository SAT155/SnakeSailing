using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;

namespace SnakeSkills;

// Sailing skill, from blaxxun-boop's Sailing 1.1.10. Only the config layout changed.
internal static class Sailing
{
	private const string Section = "2 - Sailing";
	private const string SpeedSection = "3 - Sailing Ship Speed";

	private static ConfigEntry<float> explorationRadiusIncrease = null!;
	private static ConfigEntry<float> shipHealthIncrease = null!;
	private static ConfigEntry<float> experienceGainedFactor = null!;
	private static ConfigEntry<int> experienceLoss = null!;
	private static ConfigEntry<KeyboardShortcut> shipNudgeModifierkey = null!;
	private static ConfigEntry<Toggle> allowShipNudge = null!;
	private static ConfigEntry<float> nudgeForce = null!;

	private static readonly Dictionary<string, ConfigEntry<float>> shipSpeedIncrease = new();
	private static readonly Dictionary<string, ConfigEntry<int>> shipPaddleRequirement = new();
	private static readonly Dictionary<string, ConfigEntry<int>> shipHalfRequirement = new();
	private static readonly Dictionary<string, ConfigEntry<int>> shipFullRequirement = new();

	private static Skill sailing = null!;

	public static void Init(SnakeSkillsPlugin mod)
	{
		sailing = new Skill("Sailing", "sailing.png");
		sailing.Description.English("Increases the health of ships built by you, sailing speed of ships commanded by you and your exploration radius while on a ship.");
		sailing.Name.German("Segeln");
		sailing.Description.German("Erhöht die Lebenspunkte von dir gebauter Schiffe, erhöht die Geschwindigkeit von Schiffen, die du steuerst und erhöht deinen Erkundungsradius, wenn du dich auf einem Schiff befindest.");
		sailing.Configurable = false;

		shipNudgeModifierkey = mod.config(Section, "Ship Nudge Modifier Key", new KeyboardShortcut(KeyCode.LeftShift), new ConfigDescription("Modifier key to hold, to nudge your ship. Clear value to disable this."), SailingGUID, "1 - General", false);
		explorationRadiusIncrease = mod.config(Section, "Exploration Radius Factor", 5f, new ConfigDescription("Exploration radius factor while on ships at skill level 100.", new AcceptableValueRange<float>(1f, 10f)), SailingGUID, "3 - Other");
		shipHealthIncrease = mod.config(Section, "Ship Health Factor", 2f, new ConfigDescription("Health factor for ships at skill level 100.", new AcceptableValueRange<float>(1f, 10f)), SailingGUID, "3 - Other");
		experienceGainedFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Factor for experience gained for the sailing skill.", new AcceptableValueRange<float>(0.01f, 5f)), SailingGUID, "3 - Other");
		experienceGainedFactor.SettingChanged += (_, _) => sailing.SkillGainFactor = experienceGainedFactor.Value;
		sailing.SkillGainFactor = experienceGainedFactor.Value;
		experienceLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("How much experience to lose in the sailing skill on death.", new AcceptableValueRange<int>(0, 100)), SailingGUID, "3 - Other");
		experienceLoss.SettingChanged += (_, _) => sailing.SkillLoss = experienceLoss.Value;
		sailing.SkillLoss = experienceLoss.Value;
		allowShipNudge = mod.config(Section, "Allow Ship Nudge", Toggle.On, new ConfigDescription("If on, players can press a hotkey, to give their ship a nudge, if it is stuck."), SailingGUID, "3 - Other");
		nudgeForce = mod.config(Section, "Ship Nudge Force", 10f, new ConfigDescription("Impulse force for ship nudge."), SailingGUID, "3 - Other");
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnPlaced))]
	private class AddZDO
	{
		[UsedImplicitly]
		private static void Postfix(WearNTear __instance)
		{
			if (__instance.GetComponent<Ship>())
			{
				__instance.GetComponent<ZNetView>().GetZDO().Set("Sailing Skill Level", Player.m_localPlayer.GetSkillFactor("Sailing"));
				__instance.m_health *= 1 + __instance.GetComponent<ZNetView>().GetZDO().GetFloat("Sailing Skill Level") * shipHealthIncrease.Value;
				Player.m_localPlayer.RaiseSkill("Sailing", 35f);
			}
		}
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
	private class FetchShipPiecesObjectDB
	{
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(ObjectDB __instance)
		{
			if (__instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces is { } pieces)
			{
				FetchShipPieces(pieces.m_pieces);
			}
		}
	}

	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	private class FetchShipPiecesZNetScene
	{
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(ZNetScene __instance)
		{
			FetchShipPieces(__instance.m_prefabs);
		}
	}

	private static void FetchShipPieces(IEnumerable<GameObject> prefabs)
	{
		Localization english = new();
		english.SetupLanguage("English");

		Regex regex = new("['[\"\\]]");

		List<string> shipOrder = new() { "$ship_longship_ashlands", "$ship_longship", "$ship_karve", "$ship_raft" };
		foreach (Ship ship in prefabs.Select(p => p.GetComponent<Ship>()).Where(s => s?.GetComponent<Piece>() != null).OrderByDescending(s => shipOrder.IndexOf(s.GetComponent<Piece>().m_name)))
		{
			int order = -shipSpeedIncrease.Count * 4;

			string pieceName = ship.GetComponent<Piece>().m_name;

			if (shipSpeedIncrease.ContainsKey(pieceName))
			{
				continue;
			}

			string key = regex.Replace(english.Localize(pieceName), "");
			shipSpeedIncrease[pieceName] = instance.config(SpeedSection, $"{key} Speed Factor", 1.5f, new ConfigDescription($"Speed factor for {english.Localize(pieceName)} at skill level 100.", new AcceptableValueRange<float>(1f, 3f), new ConfigurationManagerAttributes { Order = --order, DispName = $"{Localization.instance.Localize(pieceName)} Speed Factor", Description = $"Speed factor for {Localization.instance.Localize(pieceName)} at skill level 100." }), SailingGUID, "2 - Ship Speed");
			shipPaddleRequirement[pieceName] = instance.config(SpeedSection, $"{key} Paddle Requirement", 0, new ConfigDescription($"Required sailing skill level to be able to paddle a {english.Localize(pieceName)}.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order, ShowRangeAsPercent = false, DispName = $"{Localization.instance.Localize(pieceName)} Paddle Requirement", Description = $"Required sailing skill level to be able to paddle a {Localization.instance.Localize(pieceName)}." }), SailingGUID, "2 - Ship Speed");
			shipHalfRequirement[pieceName] = instance.config(SpeedSection, $"{key} Half Requirement", 0, new ConfigDescription($"Required sailing skill level to be able to sail a {english.Localize(pieceName)} with reduced sail.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order, ShowRangeAsPercent = false, DispName = $"{Localization.instance.Localize(pieceName)} Half Requirement", Description = $"Required sailing skill level to be able to sail a {Localization.instance.Localize(pieceName)} with reduced sail." }), SailingGUID, "2 - Ship Speed");
			shipFullRequirement[pieceName] = instance.config(SpeedSection, $"{key} Full Requirement", 0, new ConfigDescription($"Required sailing skill level to be able to sail a {english.Localize(pieceName)} with full sail.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order, ShowRangeAsPercent = false, DispName = $"{Localization.instance.Localize(pieceName)} Full Requirement", Description = $"Required sailing skill level to be able to sail a {Localization.instance.Localize(pieceName)} with full sail." }), SailingGUID, "2 - Ship Speed");
		}
	}

	[HarmonyPatch(typeof(Ladder), nameof(Ladder.Interact))]
	private static class NudgeShip
	{
		private static DateTime lastNudge = DateTime.UtcNow;

		private static bool Prefix(Ladder __instance)
		{
			if (allowShipNudge.Value == Toggle.Off || Player.m_localPlayer.IsSwimming())
			{
				return true;
			}

			if (Input.GetKey(shipNudgeModifierkey.Value.MainKey) && shipNudgeModifierkey.Value.Modifiers.All(Input.GetKey))
			{
				if ((DateTime.UtcNow - lastNudge).TotalSeconds < 1)
				{
					return false;
				}

				lastNudge = DateTime.UtcNow;

				Rigidbody ship = __instance.GetComponentInParent<Rigidbody>();
				if (Math.Abs(Vector3.SignedAngle(ship.transform.position - Player.m_localPlayer.transform.position, Player.m_localPlayer.transform.forward, Player.m_localPlayer.transform.up)) < 90)
				{
					ship.AddForce(Player.m_localPlayer.transform.forward * ship.mass * nudgeForce.Value, ForceMode.Impulse);
				}
				else
				{
					Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Please don't nudge your ship with your butt.");
				}

				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(Ladder), nameof(Ladder.GetHoverText))]
	private class OverrideHoverText
	{
		public static void Postfix(ref string __result)
		{
			if (allowShipNudge.Value == Toggle.Off || Player.m_localPlayer.IsSwimming())
			{
				return;
			}

			__result += Localization.instance.Localize($"\n[<b><color=yellow>{shipNudgeModifierkey.Value}</color> + <color=yellow>$KEY_Use</color></b>] Push ship");
		}
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
	private class IncreaseHealth
	{
		[UsedImplicitly]
		private static void Prefix(WearNTear __instance)
		{
			__instance.m_health *= 1 + (__instance.GetComponent<ZNetView>().GetZDO()?.GetFloat("Sailing Skill Level") ?? (__instance.GetComponent<Ship>() ? Player.m_localPlayer.GetSkillFactor("Sailing") : 1)) * shipHealthIncrease.Value;
		}
	}

	[HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
	private class IncreaseExplorationRadius
	{
		[UsedImplicitly]
		private static void Prefix(Minimap __instance, ref float radius)
		{
			if (Player.m_localPlayer is { m_attached: true, m_attachedToShip: true } player)
			{
				radius *= 1 + player.GetSkillFactor("Sailing") * explorationRadiusIncrease.Value;
			}
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
	private class ChangeShipSpeed
	{
		[UsedImplicitly]
		private class Timer
		{
			public float UpdateDelta = 0;
			public float lastUpdate = Time.fixedTime;
		}

		private static readonly ConditionalWeakTable<Ship, Timer> timers = new();

		private static void Postfix(Ship __instance, ref Vector3 __result)
		{
			Timer timer = timers.GetOrCreateValue(__instance);
			if (Player.s_players.FirstOrDefault(p => p.GetPlayerID() == __instance.m_shipControlls.GetUser()) is { } sailor)
			{
				if (shipSpeedIncrease.TryGetValue(__instance.GetComponent<Piece>().m_name, out ConfigEntry<float> speedIncrease))
				{
					__result *= 1 + sailor.m_nview.GetZDO().GetFloat("Sailing Skill") * speedIncrease.Value;
				}

				if (__instance.m_speed is not Ship.Speed.Stop)
				{
					timer.UpdateDelta += Time.fixedTime - timer.lastUpdate;
					if (timer.UpdateDelta > 1)
					{
						sailor.m_nview.InvokeRPC("Sailing Skill Increase", 0.5f);
						timer.UpdateDelta -= 1;
					}
				}
			}

			timer.lastUpdate = Time.fixedTime;
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.Forward))]
	private class PreventShipSpeeds
	{
		private static bool Prefix(Ship __instance)
		{
			if (__instance.m_speed is Ship.Speed.Slow && shipHalfRequirement.TryGetValue(__instance.GetComponent<Piece>().m_name, out ConfigEntry<int> requiredHalfLevel) && requiredHalfLevel.Value > Mathf.RoundToInt(Player.m_localPlayer.GetSkillFactor("Sailing") * 100))
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Your sailing skill is too low to sail this ship with reduced sail.");

				return false;
			}

			if (__instance.m_speed is Ship.Speed.Half && shipFullRequirement.TryGetValue(__instance.GetComponent<Piece>().m_name, out ConfigEntry<int> requiredFullLevel) && requiredFullLevel.Value > Mathf.RoundToInt(Player.m_localPlayer.GetSkillFactor("Sailing") * 100))
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Your sailing skill is too low to sail this ship with full sail.");

				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.Interact))]
	private class BlockSailing
	{
		private static bool Prefix(ShipControlls __instance)
		{
			if (!shipPaddleRequirement.TryGetValue(__instance.m_ship.GetComponent<Piece>().m_name, out ConfigEntry<int> requiredPaddleLevel) || requiredPaddleLevel.Value <= Mathf.RoundToInt(Player.m_localPlayer.GetSkillFactor("Sailing") * 100))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Your sailing skill is too low to command this ship.");
			return false;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	private class ExposeSailingSkill
	{
		private static void Postfix(Player __instance)
		{
			__instance.m_nview.Register("Sailing Skill Increase", (long _, float amount) => __instance.RaiseSkill("Sailing", amount));
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	public class PlayerUpdate
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				__instance.m_nview.GetZDO().Set("Sailing Skill", __instance.GetSkillFactor("Sailing"));
			}
		}
	}
}
