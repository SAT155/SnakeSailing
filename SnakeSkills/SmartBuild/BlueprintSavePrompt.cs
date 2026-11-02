#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal class BlueprintSavePrompt : TextReceiver
{
	private static readonly BlueprintSavePrompt Instance = new BlueprintSavePrompt();

	private static string _lastText = "";

	public static void Show(Player player)
	{
		if (TextInput.instance == null)
		{
			GameApi.Message(player, "Could not open the naming prompt, see the BepInEx log");
			Plugin.Log.LogError("Could not save a blueprint: TextInput.instance is null");
			return;
		}
		Safe.Run(delegate
		{
			GUI.FocusControl(null);
		}, "GUI.FocusControl");
		TextInput.instance.RequestText(Instance, "Name this blueprint", 40);
	}

	public string GetText()
	{
		return _lastText;
	}

	public void SetText(string text)
	{
		_lastText = text;
		string text2 = BlueprintSession.Save(Player.m_localPlayer, text);
		GameApi.Message(Player.m_localPlayer, text2);
		if (text2.StartsWith("Saved", StringComparison.Ordinal))
		{
			_lastText = "";
		}
	}
}
