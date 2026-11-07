using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// Dead Raiser skeleton choice: with the Dead Raiser out, middle click opens a small menu of skeleton
// types. Vanilla's cast fires staff_skeleton_spawn (a SpawnAbility used as the attack projectile),
// which spawns Skeleton_Friendly; that prefab gets a random weapon (m_randomWeapon: 4 swords, 1 bow)
// and a random shield (wood, bronze buckler or none) in Humanoid.GiveDefaultItems, which runs in
// Start on every client, every time the skeleton loads, seeded from the ZDO. So the choice is stored
// on the skeleton's ZDO right after it is created (SpawnAbility calls Character.SetLevel then, before
// Start), and a GiveDefaultItems prefix narrows the random lists to the chosen weapon and shield
// whenever that ZDO says so. Picking "Random" leaves everything vanilla.
// Eitr: Attack.GetAttackEitr is base x (1 - 0.33 x Blood Magic factor). Each type has its own cost at
// Blood Magic 0 and 100 (as a multiple of the staff's base cost); cheap types fall faster, so from
// mid Blood Magic they cost less than a random raise.
internal static class DeadRaiser
{
	private const string Section = "10 - Dead Raiser";
	private const string StaffPrefab = "StaffSkeleton";
	private const string SkeletonPrefab = "Skeleton_Friendly";
	private const string ZdoKey = "SnakeSkills_SkeletonType";
	private const string SaveKey = "SnakeSkills_DeadRaiserType";

	private sealed class SkeletonType
	{
		public readonly string Id;
		public readonly string Name;
		public readonly string Info;
		public readonly string? Weapon;
		public readonly string? Shield;
		public readonly float DefaultCost0;
		public readonly float DefaultCost100;
		public ConfigEntry<float>? Cost0;
		public ConfigEntry<float>? Cost100;

		public SkeletonType(string id, string name, string info, string? weapon, string? shield, float cost0, float cost100)
		{
			Id = id;
			Name = name;
			Info = info;
			Weapon = weapon;
			Shield = shield;
			DefaultCost0 = cost0;
			DefaultCost100 = cost100;
		}

		public bool IsRandom => Weapon == null;
	}

	// Random first. Weapons are the skeleton weapons the game's own skeletons carry, so they fit the
	// skeleton's animations.
	private static readonly SkeletonType[] types =
	{
		new("Random", "Random", "The game's own raise: usually a swordsman, sometimes an archer, maybe a shield.", null, null, 1f, 0.67f),
		new("Warrior", "Warrior", "Sword, no shield. The raise's usual swordsman.", "skeleton_sword2", null, 1.10f, 0.50f),
		new("Guard", "Guard", "Sword and bronze buckler: blocks hits.", "skeleton_sword2", "ShieldBronzeBuckler", 1.20f, 0.60f),
		new("Archer", "Archer", "Bow: shoots from range. The raise's rare archer.", "skeleton_bow2", null, 1.25f, 0.62f),
		new("Marksman", "Marksman", "Mountain skeleton bow: harder-hitting arrows.", "skeleton_bow_mountains", null, 1.40f, 0.75f),
		new("Brute", "Brute", "Rancid remains' mace: poison and heavy knockback.", "skeleton_mace", null, 1.45f, 0.80f),
		new("FireKnight", "Fire knight", "Brenna's burning sword: slash and fire.", "skeleton_sword_hildir", null, 1.70f, 1.00f),
		new("Champion", "Champion", "Deep North mace and wood shield: big blunt and frost hits.", "skeleton_mace_DeepNorth", "ShieldWood", 1.80f, 1.05f),
	};

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<KeyboardShortcut> menuKey = null!;

	private static SkeletonType selected = types[0];
	private static bool menuOpen;
	private static int closedFrame = -1;

	// Casts whose skeleton hasn't appeared yet (SpawnAbility waits m_preSpawnDelay, 2.5 s).
	private static readonly Queue<(SkeletonType type, float until)> pending = new();

	public static void Init(SnakeSkillsPlugin mod)
	{
		enabled = mod.config(Section, "Skeleton Choice", Toggle.On, "If on, middle click with the Dead Raiser opens a menu to pick which kind of skeleton it raises. Each kind has its own eitr cost; Random is the game's own raise at the game's own cost.");
		menuKey = mod.config(Section, "Menu Key", new KeyboardShortcut(KeyCode.Mouse2), "Key that opens the skeleton menu while the Dead Raiser is in your hands (Mouse2 = middle click; the staff has no middle-click attack of its own).", synchronizedSetting: false);
		enabled.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
		menuKey.SettingChanged += (_, _) => ApplyDescription(ObjectDB.instance);
		int order = 100;
		foreach (SkeletonType type in types)
		{
			if (type.IsRandom)
			{
				continue;
			}
			type.Cost0 = mod.config(Section, $"{type.Name} Cost At Blood Magic 0", type.DefaultCost0, new ConfigDescription($"Eitr cost of raising a {type.Name} at Blood Magic 0, as a multiple of the Dead Raiser's own cost (100 eitr; a random raise is 1.0 at 0 and 0.67 at 100).", new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = order-- }));
			type.Cost100 = mod.config(Section, $"{type.Name} Cost At Blood Magic 100", type.DefaultCost100, new ConfigDescription($"Eitr cost of raising a {type.Name} at Blood Magic 100, same scale. Levels in between are in a straight line between the two.", new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = order-- }));
		}
	}

	private static bool HoldingStaff(Player player)
	{
		ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
		return weapon?.m_dropPrefab != null && weapon.m_dropPrefab.name == StaffPrefab;
	}

	private static float BaseCost(Player player)
	{
		ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
		if (weapon != null && weapon.m_dropPrefab != null && weapon.m_dropPrefab.name == StaffPrefab)
		{
			return weapon.m_shared.m_attack.m_attackEitr;
		}
		return ObjectDB.instance?.GetItemPrefab(StaffPrefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_attack.m_attackEitr ?? 100f;
	}

	private static float CostFactor(SkeletonType type, float skillFactor)
	{
		if (type.IsRandom || type.Cost0 == null || type.Cost100 == null)
		{
			return 1f - 0.33f * skillFactor;
		}
		return Mathf.Lerp(type.Cost0.Value, type.Cost100.Value, skillFactor);
	}

	private static float Cost(Player player, SkeletonType type) => BaseCost(player) * CostFactor(type, player.GetSkillFactor(Skills.SkillType.BloodMagic));

	private static SkeletonType? Find(string id)
	{
		foreach (SkeletonType type in types)
		{
			if (type.Id == id)
			{
				return type;
			}
		}
		return null;
	}

	// ---- eitr cost ----

	[HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackEitr), typeof(Character), typeof(ItemDrop.ItemData))]
	private static class ChosenTypeCost
	{
		private static void Postfix(Character character, ItemDrop.ItemData weapon, ref float __result)
		{
			if (enabled.Value == Toggle.Off || selected.IsRandom || __result <= 0f || character != Player.m_localPlayer || weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != StaffPrefab)
			{
				return;
			}
			__result = weapon.m_shared.m_attack.m_attackEitr * CostFactor(selected, character.GetSkillFactor(Skills.SkillType.BloodMagic));
		}
	}

	// ---- marking the skeleton ----

	// staff_skeleton_spawn's SpawnAbility.Setup starts the spawn coroutine. Only casts by the local
	// player that raise Skeleton_Friendly queue a choice.
	[HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
	private static class QueueCast
	{
		private static void Prefix(SpawnAbility __instance, Character owner)
		{
			if (enabled.Value == Toggle.Off || selected.IsRandom || owner == null || owner != Player.m_localPlayer || __instance.m_spawnPrefab == null)
			{
				return;
			}
			foreach (GameObject prefab in __instance.m_spawnPrefab)
			{
				if (prefab != null && prefab.name == SkeletonPrefab)
				{
					while (pending.Count > 8)
					{
						pending.Dequeue();
					}
					pending.Enqueue((selected, Time.time + __instance.m_initialSpawnDelay + __instance.m_preSpawnDelay + 5f));
					return;
				}
			}
		}
	}

	// SpawnAbility instantiates the skeleton and right away calls SetLevel (Blood Magic level-up
	// settings), all in the same frame and before the skeleton's Start gives it its weapons.
	[HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
	private static class MarkSkeleton
	{
		private static void Prefix(Character __instance)
		{
			if (pending.Count == 0 || __instance is not Humanoid || !IsSkeleton(__instance.gameObject))
			{
				return;
			}
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || nview.GetZDO().GetString(ZdoKey).Length > 0)
			{
				return;
			}
			while (pending.Count > 0)
			{
				(SkeletonType type, float until) = pending.Dequeue();
				if (until >= Time.time)
				{
					nview.GetZDO().Set(ZdoKey, type.Id);
					return;
				}
			}
		}
	}

	private static bool IsSkeleton(GameObject go) => Utils.GetPrefabName(go) == SkeletonPrefab;

	private static readonly Dictionary<string, GameObject?> prefabCache = new();

	private static GameObject? Prefab(string name)
	{
		if (!prefabCache.TryGetValue(name, out GameObject? prefab) || prefab == null)
		{
			prefab = ObjectDB.instance?.GetItemPrefab(name);
			if (prefab == null && ZNetScene.instance != null)
			{
				prefab = ZNetScene.instance.GetPrefab(name);
			}
			if (prefab == null)
			{
				Log($"Dead Raiser: item prefab {name} not found.");
			}
			prefabCache[name] = prefab;
		}
		return prefab;
	}

	// Runs on every client whenever a skeleton starts (spawned or loaded): a marked skeleton gets
	// its chosen weapon and shield instead of random ones. The arrays are per instance.
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
	private static class GiveChosenItems
	{
		private static void Prefix(Humanoid __instance)
		{
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid() || !IsSkeleton(__instance.gameObject))
			{
				return;
			}
			SkeletonType? type = Find(nview.GetZDO().GetString(ZdoKey));
			if (type == null || type.IsRandom)
			{
				return;
			}
			GameObject? weapon = Prefab(type.Weapon!);
			if (weapon == null)
			{
				return;
			}
			__instance.m_randomWeapon = new[] { weapon };
			__instance.m_randomShield = type.Shield != null && Prefab(type.Shield) is { } shield ? new[] { shield } : Array.Empty<GameObject>();
		}
	}

	// ---- selection, saved per character ----

	[HarmonyPatch(typeof(Player), nameof(Player.Load))]
	private static class LoadSelection
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer || Player.m_localPlayer == null)
			{
				selected = __instance.m_customData != null && __instance.m_customData.TryGetValue(SaveKey, out string id) && Find(id) is { } type ? type : types[0];
			}
		}
	}

	private static void Select(Player player, SkeletonType type)
	{
		selected = type;
		if (player.m_customData != null)
		{
			player.m_customData[SaveKey] = type.Id;
		}
		player.Message(MessageHud.MessageType.Center, $"Dead Raiser: {type.Name} ({Mathf.CeilToInt(Cost(player, type))} eitr)");
	}

	// ---- menu ----

	public static void Update()
	{
		Player player = Player.m_localPlayer;
		if (!menuOpen)
		{
			if (enabled.Value == Toggle.On && player != null && !player.IsDead() && HoldingStaff(player) && menuKey.Value.IsDown() && !UiBusy())
			{
				menuOpen = true;
			}
			return;
		}
		if (player == null || player.IsDead() || !HoldingStaff(player) || enabled.Value == Toggle.Off || Input.GetKeyDown(KeyCode.Escape) || menuKey.Value.IsDown() || UiBusy())
		{
			Close();
		}
	}

	private static void Close()
	{
		if (menuOpen)
		{
			menuOpen = false;
			closedFrame = Time.frameCount;
		}
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

	private static GUIStyle? buttonStyle;
	private static GUIStyle? titleStyle;
	private static GUIStyle? smallStyle;

	public static void OnGUI()
	{
		Player player = Player.m_localPlayer;
		if (!menuOpen || player == null)
		{
			return;
		}
		buttonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 15, wordWrap = true, padding = new RectOffset(8, 8, 6, 6) };
		titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.631f, 0.235f, 1f) } };
		smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };

		const float rowHeight = 52f;
		float width = Mathf.Min(520f, Screen.width - 40f);
		float height = 70f + types.Length * (rowHeight + 6f);
		Rect rect = new((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
		GUI.Box(rect, GUIContent.none);
		GUI.Box(rect, GUIContent.none);
		GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 16f));
		float skill = player.GetSkillLevel(Skills.SkillType.BloodMagic);
		GUILayout.Label("Raise which skeleton?", titleStyle);
		GUILayout.Label($"Blood Magic {Mathf.FloorToInt(skill)}. Costs drop as Blood Magic rises; the health cost stays the game's.", smallStyle);
		float randomCost = Cost(player, types[0]);
		foreach (SkeletonType type in types)
		{
			float cost = Cost(player, type);
			string costColor = type.IsRandom ? "#dddddd" : cost < randomCost - 0.5f ? "#9be37b" : "#ffcf6b";
			string mark = type == selected ? "<color=#ffa13c>></color> " : "";
			string label = $"{mark}<b>{type.Name}</b>   <color={costColor}>{Mathf.CeilToInt(cost)} eitr</color>\n<size=12><color=#bbbbbb>{type.Info}</color></size>";
			GUILayout.BeginHorizontal();
			Rect iconRect = GUILayoutUtility.GetRect(rowHeight, rowHeight, GUILayout.Width(rowHeight), GUILayout.Height(rowHeight));
			bool clicked = GUI.Button(iconRect, GUIContent.none);
			DrawIcon(iconRect, type);
			clicked |= GUILayout.Button(label, buttonStyle, GUILayout.Height(rowHeight));
			GUILayout.EndHorizontal();
			GUILayout.Space(4f);
			if (clicked)
			{
				Select(player, type);
				Close();
			}
		}
		GUILayout.EndArea();
	}

	private static Sprite? Icon(string? itemName)
	{
		if (itemName == null)
		{
			return null;
		}
		Sprite[]? icons = Prefab(itemName)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
		return icons != null && icons.Length > 0 && icons[0] != null && icons[0].texture != null ? icons[0] : null;
	}

	private static void DrawSprite(Rect rect, Sprite sprite)
	{
		Rect tr = sprite.textureRect;
		Texture tex = sprite.texture;
		GUI.DrawTextureWithTexCoords(rect, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
	}

	// Monster weapons may have no icon; the skeleton trophy stands in.
	private static void DrawIcon(Rect rect, SkeletonType type)
	{
		Sprite? icon = (type.IsRandom ? null : Icon(type.Weapon)) ?? Icon("TrophySkeleton");
		if (icon != null)
		{
			DrawSprite(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f), icon);
		}
		if (Icon(type.Shield) is { } shield)
		{
			DrawSprite(new Rect(rect.xMax - 24f, rect.yMax - 24f, 21f, 21f), shield);
		}
	}

	internal static bool MenuOpen => menuOpen;

	// While the menu is open: free the mouse, keep the camera still and the player from acting, and
	// don't let the Escape that closes it open the game menu.
	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
	private static class FreeCursor
	{
		private static void Postfix()
		{
			if (menuOpen)
			{
				ZCursor.LockState = CursorLockMode.None;
				ZCursor.Show();
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
	private static class BlockInput
	{
		private static void Postfix(Player __instance, ref bool __result)
		{
			if ((menuOpen || closedFrame == Time.frameCount) && __instance == Player.m_localPlayer)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.LateUpdate))]
	private static class BlockLook
	{
		private static bool Prefix(PlayerController __instance)
		{
			if (!menuOpen || __instance.m_character == null || __instance.m_character != Player.m_localPlayer)
			{
				return true;
			}
			__instance.m_character.SetMouseLook(Vector2.zero);
			return false;
		}
	}

	[HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
	private static class NoMenuOnClose
	{
		private static bool Prefix() => !menuOpen && closedFrame != Time.frameCount;
	}

	// The staff's description mentions the menu.
	private static readonly Dictionary<ItemDrop.ItemData.SharedData, string> originalDescription = new();

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

	private static void ApplyDescription(ObjectDB? db)
	{
		prefabCache.Clear();
		ItemDrop.ItemData.SharedData? shared = db?.GetItemPrefab(StaffPrefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
		if (shared == null)
		{
			return;
		}
		if (!originalDescription.TryGetValue(shared, out string text))
		{
			text = shared.m_description;
			originalDescription[shared] = text;
		}
		if (enabled.Value == Toggle.On)
		{
			string key = menuKey.Value.MainKey == KeyCode.Mouse2 && !menuKey.Value.Modifiers.Any() ? "Middle click" : menuKey.Value.ToString();
			text += $"\n<color=orange>{key}: choose which skeleton to raise. Each kind has its own eitr cost.</color>";
		}
		shared.m_description = text;
	}
}
