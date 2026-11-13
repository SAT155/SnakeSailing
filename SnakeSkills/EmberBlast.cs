using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// Ember Blast: the Staff of Embers' middle click throws its fireball as a mining charge. The staff
// has no secondary attack in vanilla (HaveSecondaryAttack = empty animation), so one is made from a
// clone of the primary: same animation and the same staff_fireball_projectile, which unmodded
// players see as a normal fireball. Projectiles from that attack get no damage, no AoE, no status
// effect and nothing spawned on hit, so a direct hit on a creature does nothing; instead their
// m_onHit callback (run on the projectile's owner, the caster) damages rocks around the impact with
// pickaxe damage. Rocks only: MineRock5 (one hit with m_radius damages every area in the sphere),
// MineRock (each area collider), and Destructibles that take pickaxe but not chop damage (the same
// filter Mining uses). Creatures, buildings and trees are never touched.
internal static class EmberBlast
{
	private const string Section = "9 - Ember Blast";

	// Vanilla prefab of the Staff of Embers.
	private const string StaffPrefab = "StaffFireball";

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<float> blastRadius = null!;
	private static ConfigEntry<float> blastDamage = null!;
	private static ConfigEntry<int> blastToolTier = null!;
	private static ConfigEntry<float> eitrCostFactor = null!;
	private static ConfigEntry<string> extraEffect = null!;
	private static ConfigEntry<Toggle> tooltip = null!;

	public static void Init(SnakeSkillsPlugin mod)
	{
		enabled = mod.config(Section, "Ember Blast", Toggle.On, "If on, middle click (secondary attack) with the Staff of Embers fires a blasting charge: the fireball breaks rocks and ore deposits around where it lands, like dynamite, but does no damage to creatures, players or buildings.");
		blastRadius = mod.config(Section, "Blast Radius", 4f, new ConfigDescription("Radius in metres around the impact in which rocks take blast damage.", new AcceptableValueRange<float>(1f, 12f)));
		blastDamage = mod.config(Section, "Blast Damage", 150f, new ConfigDescription("Pickaxe damage dealt to every rock piece in the blast radius. The Mining skill raises it like it raises pickaxe damage (Mining Damage Factor at level 100). A copper deposit piece has roughly as much health as a few pickaxe swings.", new AcceptableValueRange<float>(1f, 2000f)));
		blastToolTier = mod.config(Section, "Blast Tool Tier", 4, new ConfigDescription("Pickaxe tier the blast counts as. Rocks and ores that need a better pickaxe than this only show 'too hard'. Vanilla pickaxes: Antler 1, Bronze 2, Iron 3, Black metal 4 and up.", new AcceptableValueRange<int>(0, 10)));
		eitrCostFactor = mod.config(Section, "Eitr Cost Factor", 1.5f, new ConfigDescription("Eitr cost of a blast compared to a normal Staff of Embers fireball. 1.5 = half again as much.", new AcceptableValueRange<float>(0f, 5f)));
		extraEffect = mod.config(Section, "Extra Explosion Effect", "fx_dynamite_explosion", "Vanilla effect prefab played at the impact on top of the fireball's own explosion, for a bigger bang. Empty for none. Prefabs with game logic (anything beyond visuals and sound) are skipped and logged.", synchronizedSetting: false);
		tooltip = mod.config(Section, "Ember Blast Tooltip", Toggle.On, "If on, the Staff of Embers' description says what middle click does.", synchronizedSetting: false);

		enabled.SettingChanged += (_, _) => Apply(ObjectDB.instance);
		eitrCostFactor.SettingChanged += (_, _) => Apply(ObjectDB.instance);
		tooltip.SettingChanged += (_, _) => Apply(ObjectDB.instance);
		blastRadius.SettingChanged += (_, _) => Apply(ObjectDB.instance);
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	private static class ObjectDBAwake
	{
		private static void Postfix(ObjectDB __instance) => Apply(__instance);
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
	private static class ObjectDBCopyOtherDB
	{
		private static void Postfix(ObjectDB __instance) => Apply(__instance);
	}

	// Items share their SharedData with the prefab, so changing the prefab's secondary attack and
	// description changes staffs already in inventories. The originals are kept so turning the
	// blast off (or a config change) puts them back instead of stacking.
	private static readonly Dictionary<ItemDrop.ItemData.SharedData, Attack> originalSecondary = new();
	private static readonly Dictionary<ItemDrop.ItemData.SharedData, string> originalDescription = new();

	private static void Apply(ObjectDB? db)
	{
		if (db == null || db.m_items == null || db.m_items.Count == 0)
		{
			return;
		}

		ItemDrop.ItemData.SharedData? shared = db.GetItemPrefab(StaffPrefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
		if (shared == null)
		{
			Log($"{StaffPrefab} not found in ObjectDB; no Ember Blast.");
			return;
		}

		if (!originalSecondary.TryGetValue(shared, out Attack original))
		{
			original = shared.m_secondaryAttack;
			originalSecondary[shared] = original;
			originalDescription[shared] = shared.m_description;
			if (!string.IsNullOrEmpty(original?.m_attackAnimation))
			{
				Log($"{StaffPrefab} already has a secondary attack ({original}); Ember Blast replaces it while on.");
			}
		}

		if (enabled.Value == Toggle.On && shared.m_attack != null)
		{
			Attack blast = shared.m_attack.Clone();
			blast.m_attackEitr = shared.m_attack.m_attackEitr * eitrCostFactor.Value;
			shared.m_secondaryAttack = blast;
		}
		else
		{
			shared.m_secondaryAttack = original;
		}

		string text = originalDescription[shared];
		if (enabled.Value == Toggle.On && tooltip.Value == Toggle.On)
		{
			text += $"\n<color=orange>Middle click: blasting charge. Breaks rock and ore within {blastRadius.Value:0.#} m of where it lands; harmless to creatures.</color>";
		}
		shared.m_description = text;
	}

	private static bool IsBlastAttack(Attack attack) => enabled.Value == Toggle.On && attack.m_character is Humanoid humanoid && humanoid.m_currentAttack == attack && humanoid.m_currentAttackIsSecondary && attack.m_weapon?.m_dropPrefab != null && attack.m_weapon.m_dropPrefab.name == StaffPrefab;

	// FireProjectileBurst creates the projectile and calls Projectile.Setup; the flag tells the
	// Setup postfix which projectiles are blasts.
	[HarmonyPatch(typeof(Attack), nameof(Attack.FireProjectileBurst))]
	private static class MarkBlastProjectiles
	{
		internal static Player? firing;

		private static void Prefix(Attack __instance)
		{
			firing = IsBlastAttack(__instance) ? __instance.m_character as Player : null;
		}

		private static void Finalizer() => firing = null;
	}

	[HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
	private static class MakeBlastProjectile
	{
		private static void Postfix(Projectile __instance)
		{
			Player? player = MarkBlastProjectiles.firing;
			if (player == null)
			{
				return;
			}

			__instance.m_damage = new HitData.DamageTypes();
			__instance.m_aoe = 0f;
			__instance.m_hitMidFlight = false;
			__instance.m_attackForce = 0f;
			__instance.m_statusEffect = "";
			__instance.m_statusEffectHash = 0;
			__instance.m_healthReturn = 0f;
			__instance.m_eitrAdd = 0f;
			__instance.m_raiseSkillAmount = 0f;
			__instance.m_spawnOnHit = null;
			__instance.m_randomSpawnOnHit = new List<GameObject>();
			__instance.m_spawnItem = null;

			float damage = blastDamage.Value * Mining.DamageMultiplier(player);
			short toolTier = (short)blastToolTier.Value;
			float radius = blastRadius.Value;
			__instance.m_onHit += (_, point, water) =>
			{
				if (!water && player != null)
				{
					Explode(player, point, radius, damage, toolTier);
				}
			};
		}
	}

	private static void Explode(Player player, Vector3 point, float radius, float damage, short toolTier)
	{
		HashSet<IDestructible> done = new();
		int rocks = 0;
		foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
		{
			IDestructible? target = collider.GetComponentInParent<IDestructible>();
			switch (target)
			{
				case MineRock5 rock5 when done.Add(rock5):
					// With m_radius set MineRock5 finds every one of its areas in the sphere itself.
					HitData hit5 = NewHit(player, point, damage, toolTier);
					hit5.m_radius = radius;
					rock5.Damage(hit5);
					++rocks;
					break;
				case MineRock rock when rock.m_hitAreas != null && Array.IndexOf(rock.m_hitAreas, collider) >= 0:
					HitData hit = NewHit(player, collider.bounds.center, damage, toolTier);
					hit.m_hitCollider = collider;
					rock.Damage(hit);
					if (done.Add(rock))
					{
						++rocks;
					}
					break;
				case Destructible destructible when IsRock(destructible) && done.Add(destructible):
					destructible.Damage(NewHit(player, collider.bounds.center, damage, toolTier));
					++rocks;
					break;
			}
		}

		SpawnExtraEffect(point);
		if (GameCamera.instance != null)
		{
			GameCamera.instance.AddShake(point, 25f, 2f, false);
		}
		Log($"Ember Blast at {point}: {rocks} rock(s) in {radius:0.#} m, {damage:0} pickaxe damage, tier {toolTier}.");
	}

	private static bool IsRock(Destructible destructible) => Mining.IsRock(destructible);

	private static HitData NewHit(Player player, Vector3 point, float damage, short toolTier)
	{
		HitData hit = new()
		{
			m_point = point,
			m_toolTier = toolTier,
			m_dir = Vector3.down,
			m_hitType = HitData.HitType.PlayerHit,
			m_skill = Skills.SkillType.Pickaxes,
			m_skillRaiseAmount = 0f,
		};
		hit.m_damage.m_pickaxe = damage;
		hit.SetAttacker(player);
		return hit;
	}

	private static readonly HashSet<string> safeComponents = new()
	{
		"Transform", "ParticleSystem", "ParticleSystemRenderer", "AudioSource", "Light", "MeshRenderer", "MeshFilter", "LineRenderer", "TrailRenderer",
		"ZNetView", "ZSyncTransform", "ZSFX", "TimedDestruction", "LightFlicker", "LightLod", "CamShaker", "EffectFade", "Billboard", "LineConnect", "RandomAnimation",
	};

	private static readonly Dictionary<string, bool> checkedEffects = new();

	private static void SpawnExtraEffect(Vector3 point)
	{
		string name = extraEffect.Value.Trim();
		if (name.Length == 0 || ZNetScene.instance == null)
		{
			return;
		}

		GameObject? prefab = ZNetScene.instance.GetPrefab(name);
		if (prefab == null)
		{
			if (!checkedEffects.ContainsKey(name))
			{
				checkedEffects[name] = false;
				Log($"Extra Explosion Effect '{name}' not found; none played.");
			}
			return;
		}

		if (!checkedEffects.TryGetValue(name, out bool safe))
		{
			safe = true;
			foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
			{
				if (component != null && (component is Collider || !safeComponents.Contains(component.GetType().Name)))
				{
					Log($"Extra Explosion Effect '{name}' has a {component.GetType().Name}, which may carry game logic; skipped.");
					safe = false;
					break;
				}
			}
			checkedEffects[name] = safe;
		}

		if (safe)
		{
			Object.Instantiate(prefab, point, Quaternion.identity);
		}
	}
}
