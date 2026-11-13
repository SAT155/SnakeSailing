#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

internal static class Wheel
{
	private static float _pending;

	private static float _accumulated;

	private static int _capturedFrame = -1;

	public static bool Claimed()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer != null && SmartHammer.Equipped(localPlayer) && !ControlPanel.IsOpen && localPlayer.InPlaceMode())
		{
			if (!Plugin.AxisKeyHeld() && !Keys.Held(ModConfig.KeyRotation.Value))
			{
				return DesignerSession.FreeAiming;
			}
			return true;
		}
		return false;
	}

	public static void Intercept(ref float value)
	{
		if (Time.frameCount != _capturedFrame)
		{
			_capturedFrame = Time.frameCount;
			_pending += value;
		}
		if (Claimed())
		{
			value = 0f;
		}
	}

	public static int Take(float threshold)
	{
		_accumulated += _pending;
		_pending = 0f;
		if (_accumulated > threshold)
		{
			_accumulated = 0f;
			return 1;
		}
		if (_accumulated < 0f - threshold)
		{
			_accumulated = 0f;
			return -1;
		}
		return 0;
	}

	public static void Discard()
	{
		_pending = 0f;
		_accumulated = 0f;
	}
}
