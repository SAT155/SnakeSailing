#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SnakeSmartHammer;

internal static class Keys
{
	private static readonly Dictionary<KeyCode, Key> Mapped = new Dictionary<KeyCode, Key>();

	private static bool _resolved;

	private static MethodInfo _zGetKey;

	private static MethodInfo _zGetKeyDown;

	private static void Resolve()
	{
		if (!_resolved)
		{
			_resolved = true;
			Type type = AccessTools.TypeByName("ZInput");
			if (!(type == null))
			{
				_zGetKey = FindKeyMethod(type, "GetKey");
				_zGetKeyDown = FindKeyMethod(type, "GetKeyDown");
			}
		}
	}

	private static MethodInfo FindKeyMethod(Type type, string name)
	{
		MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			ParameterInfo[] parameters = methodInfo.GetParameters();
			if (methodInfo.Name == name && parameters.Length >= 1 && parameters[0].ParameterType == typeof(KeyCode))
			{
				return methodInfo;
			}
		}
		return null;
	}

	private static bool Call(MethodInfo method, KeyCode key)
	{
		ParameterInfo[] parameters = method.GetParameters();
		object[] array = new object[parameters.Length];
		array[0] = key;
		for (int i = 1; i < parameters.Length; i++)
		{
			array[i] = ((parameters[i].ParameterType == typeof(bool)) ? ((object)false) : parameters[i].DefaultValue);
		}
		return (bool)method.Invoke(null, array);
	}

	private static bool TryMap(KeyCode code, out Key key)
	{
		if (Mapped.TryGetValue(code, out key))
		{
			return key != Key.None;
		}
		switch (code)
		{
		case KeyCode.LeftControl:
			key = Key.LeftCtrl;
			break;
		case KeyCode.RightControl:
			key = Key.RightCtrl;
			break;
		case KeyCode.Return:
			key = Key.Enter;
			break;
		case KeyCode.Alpha0:
			key = Key.Digit0;
			break;
		case KeyCode.Alpha1:
			key = Key.Digit1;
			break;
		case KeyCode.Alpha2:
			key = Key.Digit2;
			break;
		case KeyCode.Alpha3:
			key = Key.Digit3;
			break;
		case KeyCode.Alpha4:
			key = Key.Digit4;
			break;
		case KeyCode.Alpha5:
			key = Key.Digit5;
			break;
		case KeyCode.Alpha6:
			key = Key.Digit6;
			break;
		case KeyCode.Alpha7:
			key = Key.Digit7;
			break;
		case KeyCode.Alpha8:
			key = Key.Digit8;
			break;
		case KeyCode.Alpha9:
			key = Key.Digit9;
			break;
		default:
			if (!Enum.TryParse<Key>(code.ToString(), out key))
			{
				key = Key.None;
			}
			break;
		}
		Mapped[code] = key;
		return key != Key.None;
	}

	public static bool Held(KeyCode key)
	{
		try
		{
			return key switch
			{
				KeyCode.LeftControl => HeldRaw(KeyCode.LeftControl) || HeldRaw(KeyCode.RightControl), 
				KeyCode.LeftShift => HeldRaw(KeyCode.LeftShift) || HeldRaw(KeyCode.RightShift), 
				_ => HeldRaw(key), 
			};
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool HeldRaw(KeyCode key)
	{
		Keyboard current = Keyboard.current;
		if (current != null && TryMap(key, out var key2))
		{
			return current[key2].isPressed;
		}
		Resolve();
		if (!(_zGetKey != null))
		{
			return Input.GetKey(key);
		}
		return Call(_zGetKey, key);
	}

	public static bool AnyOtherThanCtrlHeld()
	{
		try
		{
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				foreach (KeyControl allKey in current.allKeys)
				{
					if (allKey != null && allKey.isPressed && allKey.keyCode != Key.LeftCtrl && allKey.keyCode != Key.RightCtrl)
					{
						return true;
					}
				}
			}
			Mouse current3 = Mouse.current;
			return current3 != null && (current3.leftButton.isPressed || current3.rightButton.isPressed || current3.middleButton.isPressed);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool Down(KeyCode key)
	{
		try
		{
			Keyboard current = Keyboard.current;
			if (current != null && TryMap(key, out var key2))
			{
				return current[key2].wasPressedThisFrame;
			}
			Resolve();
			return (_zGetKeyDown != null) ? Call(_zGetKeyDown, key) : Input.GetKeyDown(key);
		}
		catch (Exception)
		{
			return false;
		}
	}
}
