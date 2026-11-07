using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;

namespace SnakeSkills;

// Bubble shield autocast (Sam's ask, 2026-11-07): with the Staff of Protection in hand, the staff
// recasts its bubble by itself when ALL of these hold: standing still for more than 5 s, not in
// combat, 5 s or less left on the current bubble (or no bubble), full health and full eitr.
// The autocast flag is armed when the conditions become true, cleared once the cast starts, and only
// armed again after the conditions have been false and then true again, so it casts once per window.
// The cast is the staff's own attack (Humanoid.StartAttack), so it costs and trains like a normal cast.
// The bubble is an SE_Shield status effect; its time left is StatusEffect.GetRemaningTime() (m_ttl - m_time).
internal static class BubbleShield
{
	private const string Section = "11 - Bubble Shield";
	private const string StaffPrefab = "StaffShield";

	// "In combat": you attacked or were hit in the last few seconds, or an alerted enemy is near.
	private const float CombatSeconds = 10f;
	private const float EnemyRange = 30f;

	private static ConfigEntry<Toggle> autocast = null!;
	private static ConfigEntry<float> stillSeconds = null!;
	private static ConfigEntry<float> secondsLeft = null!;

	private static float stillSince = -1f;
	private static float lastHitTime = -999f;
	private static float nextTry;
	private static bool wasMet;
	private static bool armed;

	public static void Init(SnakeSkillsPlugin mod)
	{
		autocast = mod.config(Section, "Autocast Bubble Shield", Toggle.Off, "If on, the Staff of Protection (in your hands) recasts its bubble by itself when you have stood still long enough, are not in combat, have full health and full eitr, and the current bubble is about to run out (or there is none). It casts once each time those conditions come together.", synchronizedSetting: false);
		stillSeconds = mod.config(Section, "Standing Still Seconds", 5f, new ConfigDescription("How long you must stand still before the autocast can fire.", new AcceptableValueRange<float>(0f, 60f)), synchronizedSetting: false);
		secondsLeft = mod.config(Section, "Seconds Left On Bubble", 5f, new ConfigDescription("The autocast fires when the current bubble has this many seconds left or fewer (or there is no bubble).", new AcceptableValueRange<float>(0f, 120f)), synchronizedSetting: false);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.OnDamaged))]
	private static class TrackHits
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				lastHitTime = Time.time;
			}
		}
	}

	public static void Update()
	{
		Player player = Player.m_localPlayer;
		if (autocast.Value == Toggle.Off || player == null || player.IsDead())
		{
			stillSince = -1f;
			wasMet = false;
			armed = false;
			return;
		}

		bool still = player.IsOnGround() && player.m_moveDir.sqrMagnitude < 0.0001f && (player.m_body == null || player.m_body.linearVelocity.sqrMagnitude < 0.04f) && !player.InAttack() && !player.IsAttached();
		if (!still)
		{
			stillSince = -1f;
		}
		else if (stillSince < 0f)
		{
			stillSince = Time.time;
		}

		bool met = HoldingStaff(player)
			&& stillSince >= 0f && Time.time - stillSince > stillSeconds.Value
			&& !InCombat(player)
			&& BubbleTimeLeft(player) <= secondsLeft.Value
			&& player.GetHealth() >= player.GetMaxHealth() - 0.5f
			&& player.GetMaxEitr() > 0f && player.GetEitr() >= player.GetMaxEitr() - 0.5f;

		if (met && !wasMet)
		{
			armed = true;
		}
		wasMet = met;

		if (!armed || !met || Time.time < nextTry || UiBusy())
		{
			return;
		}
		nextTry = Time.time + 1f;
		if (player.StartAttack(null, false))
		{
			armed = false;
		}
	}

	private static bool HoldingStaff(Player player)
	{
		ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
		return weapon?.m_dropPrefab != null && weapon.m_dropPrefab.name == StaffPrefab;
	}

	private static float BubbleTimeLeft(Player player)
	{
		float left = 0f;
		foreach (StatusEffect se in player.GetSEMan().GetStatusEffects())
		{
			if (se is SE_Shield)
			{
				left = Mathf.Max(left, se.GetRemaningTime());
			}
		}
		return left;
	}

	private static bool InCombat(Player player)
	{
		if (player.m_lastCombatTimer < CombatSeconds || Time.time - lastHitTime < CombatSeconds)
		{
			return true;
		}
		Vector3 pos = player.transform.position;
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == player || c.IsDead() || (c.transform.position - pos).sqrMagnitude > EnemyRange * EnemyRange)
			{
				continue;
			}
			BaseAI? ai = c.GetBaseAI();
			if (ai != null && ai.IsAlerted() && BaseAI.IsEnemy(c, player))
			{
				return true;
			}
		}
		return false;
	}

	private static bool UiBusy()
	{
		try
		{
			return InventoryGui.IsVisible() || Menu.IsVisible() || Console.IsVisible() || TextInput.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()) || Minimap.IsOpen() || StoreGui.IsVisible();
		}
		catch
		{
			return false;
		}
	}
}
