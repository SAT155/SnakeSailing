#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Menu), "Show")]
internal static class DesignerPromptMenuPatch
{
	private static bool Prefix()
	{
		if (!DesignerPrompt.IsOpen && !BlueprintList.IsOpen)
		{
			return DesignerPrompt.ClosedFrame != Time.frameCount && BlueprintList.ClosedFrame != Time.frameCount;
		}
		return false;
	}
}
