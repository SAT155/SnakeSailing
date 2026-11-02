#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeSkills.SmartBuild;

internal static class DesignerPrompt
{
	private const float Width = 640f;

	private const float Height = 170f;

	private const float ButtonWidth = 110f;

	private const float ButtonHeight = 36f;

	private static GameObject _window;

	public static int ClosedFrame { get; private set; } = -1;


	public static bool IsOpen
	{
		get
		{
			if (_window != null)
			{
				return _window.activeSelf;
			}
			return false;
		}
	}

	public static void Show()
	{
		try
		{
			if (_window == null)
			{
				Build();
			}
			if (!(_window == null))
			{
				ControlPanel.Close();
				_window.transform.SetAsLastSibling();
				_window.SetActive(value: true);
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-prompt", "Could not open the designer box prompt: " + ex);
		}
	}

	public static void Close()
	{
		if (IsOpen)
		{
			_window.SetActive(value: false);
			ClosedFrame = Time.frameCount;
		}
	}

	private static void Build()
	{
		GUIManager instance = GUIManager.Instance;
		if (instance != null && !(GUIManager.CustomGUIFront == null))
		{
			Transform transform = GUIManager.CustomGUIFront.transform;
			Vector2 vector = new Vector2(0.5f, 0.5f);
			_window = instance.CreateWoodpanel(transform, vector, vector, Vector2.zero, 640f, 170f);
			_window.name = "SmartBuildDesignerPrompt";
			_window.SetActive(value: false);
			instance.CreateText("Designer box", _window.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), instance.AveriaSerifBold, 20, instance.ValheimOrange, true, Color.black, 600f, 30f, false).GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
			instance.CreateText("Save what is selected as a blueprint, discard it, turn the box off and keep the selection, or resize the box where it stands?", _window.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), instance.AveriaSerif, 15, Color.white, true, Color.black, 600f, 40f, false).GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
			float gap = 15f;
			AddButton(0, gap, "Save...", delegate
			{
				Close();
				BlueprintSavePrompt.Show(Player.m_localPlayer);
			});
			AddButton(1, gap, "Discard", delegate
			{
				Close();
				DesignerSession.Discard(Player.m_localPlayer);
			});
			AddButton(2, gap, "Turn off", delegate
			{
				Close();
				DesignerSession.TurnOff(Player.m_localPlayer);
			});
			AddButton(3, gap, "Resize", delegate
			{
				Close();
				DesignerSession.StartResize(Player.m_localPlayer);
			});
			AddButton(4, gap, "Cancel", Close);
		}
	}

	private static void AddButton(int index, float gap, string text, Action onClick)
	{
		float x = gap + (float)index * (110f + gap) + 55f;
		GUIManager.Instance.CreateButton(text, _window.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 38f), 110f, 36f).GetComponent<Button>().onClick.AddListener(delegate
		{
			onClick();
		});
	}
}
