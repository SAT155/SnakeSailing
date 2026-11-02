#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "TakeInput")]
internal static class PanelInputPatch
{
	private static void Postfix(Player __instance, ref bool __result)
	{
		if ((ControlPanel.CapturingInput || DesignerPrompt.IsOpen || BlueprintList.IsOpen) && __instance == Player.m_localPlayer)
		{
			__result = false;
		}
	}
}
