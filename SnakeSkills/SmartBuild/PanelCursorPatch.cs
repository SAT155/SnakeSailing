#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
internal static class PanelCursorPatch
{
	private static void Postfix()
	{
		if (ControlPanel.IsOpen || DesignerPrompt.IsOpen || BlueprintList.IsOpen)
		{
			ZCursor.LockState = CursorLockMode.None;
			ZCursor.Show();
		}
	}
}
