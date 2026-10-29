using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using SkillManager;

namespace SnakeSkills;

/// <summary>
/// Keeps the custom skills alive through a session without the mod.
///
/// Vanilla <c>Skills.Load</c> drops every skill id it doesn't know (<c>IsSkillValid</c>), and the
/// next save writes the list without them, so one unmodded login wiped Sailing, Ranching and
/// Mining for good. <c>Player.m_customData</c> is a string dictionary that vanilla loads and saves
/// untouched, so every save also copies the levels there ("SnakeSkills_Sailing" =
/// "level;accumulator"). On load, a skill that is missing from the vanilla skill list is put back
/// from that copy. A skill that is present wins, since it is the newer of the two.
/// </summary>
internal static class SkillBackup
{
	private const string KeyPrefix = "SnakeSkills_";
	internal static readonly string[] SkillNames = { "Sailing", "Ranching", "Mining" };

	private static readonly HashSet<Skills.SkillType> missingAtLoad = new();

	// Skills.Load drops unknown ids, so whatever is missing right after it was never saved or
	// was lost to an unmodded session. Player.Load may read m_customData before or after the
	// skills, so restore here if the backup is already in, and again when Player.Load ends.
	[HarmonyPatch(typeof(Skills), nameof(Skills.Load))]
	private static class NoteMissingSkills
	{
		private static void Postfix(Skills __instance)
		{
			missingAtLoad.Clear();
			foreach (string name in SkillNames)
			{
				Skills.SkillType type = Skill.fromName(name);
				if (!__instance.m_skillData.ContainsKey(type))
				{
					missingAtLoad.Add(type);
				}
			}

			if (__instance.m_player != null)
			{
				Restore(__instance.m_player);
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Load))]
	private static class RestoreMissingSkills
	{
		private static void Postfix(Player __instance)
		{
			Restore(__instance);
			missingAtLoad.Clear();
		}
	}

	private static void Restore(Player player)
	{
		if (player.m_skills == null || player.m_customData == null)
		{
			return;
		}

		foreach (string name in SkillNames)
		{
			Skills.SkillType type = Skill.fromName(name);
			if (!missingAtLoad.Contains(type) || !player.m_customData.TryGetValue(KeyPrefix + name, out string stored))
			{
				continue;
			}

			missingAtLoad.Remove(type);
			string[] parts = stored.Split(';');
			if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float level))
			{
				SnakeSkillsPlugin.Log($"Skill backup for {name} is unreadable ('{stored}'), leaving it alone.");
				continue;
			}

			float accumulator = 0f;
			if (parts.Length > 1)
			{
				float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out accumulator);
			}

			Skills.Skill skill = player.m_skills.GetSkill(type);
			skill.m_level = level;
			skill.m_accumulator = accumulator;
			SnakeSkillsPlugin.Log($"{name} was missing from this character's skills (last played without SnakeSkills?). Restored level {level:0.##} from the backup.");
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Save))]
	private static class BackUpSkills
	{
		private static void Prefix(Player __instance)
		{
			if (__instance.m_skills == null || __instance.m_customData == null)
			{
				return;
			}

			foreach (string name in SkillNames)
			{
				// Only skills the character actually has; a missing one keeps its old backup.
				if (__instance.m_skills.m_skillData.TryGetValue(Skill.fromName(name), out Skills.Skill skill))
				{
					__instance.m_customData[KeyPrefix + name] = skill.m_level.ToString("R", CultureInfo.InvariantCulture) + ";" + skill.m_accumulator.ToString("R", CultureInfo.InvariantCulture);
				}
			}
		}
	}
}
