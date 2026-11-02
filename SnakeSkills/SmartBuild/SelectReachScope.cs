#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
namespace SnakeSkills.SmartBuild;

internal static class SelectReachScope
{
	public static float Raise(Player player)
	{
		if (player != Player.m_localPlayer || !SmartHammer.Equipped(player))
		{
			return -1f;
		}
		float value = ModConfig.SelectReach.Value;
		if (value <= player.m_maxPlaceDistance)
		{
			return -1f;
		}
		float maxPlaceDistance = player.m_maxPlaceDistance;
		player.m_maxPlaceDistance = value;
		return maxPlaceDistance;
	}

	public static void Restore(Player player, float previous)
	{
		if (previous >= 0f)
		{
			player.m_maxPlaceDistance = previous;
		}
	}
}
