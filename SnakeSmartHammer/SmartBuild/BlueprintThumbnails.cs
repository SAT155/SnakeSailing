#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

/// <summary>
/// SnakeSkills: small preview pictures for the blueprint list. Each blueprint's stub (the visual
/// copy the build ghost uses) is cloned far below the world for one frame and photographed by a
/// temporary camera that only sees a spare layer, then destroyed. One picture per frame while the
/// list is open; cached for the session, redrawn when a blueprint is saved again.
/// </summary>
internal static class BlueprintThumbnails
{
	private const int Size = 128;

	// Unity's last user layer; Valheim doesn't use it (checked by name below, with a fallback).
	private const int FallbackLayer = 31;

	private static readonly Vector3 StagePosition = new Vector3(0f, -5000f, 0f);

	private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> Failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static Camera _camera;

	private static Light _light;

	private static RenderTexture _target;

	private static int _layer = -1;

	public static Texture2D Get(string name)
	{
		return Cache.TryGetValue(name, out Texture2D texture) && texture != null ? texture : null;
	}

	/// <summary>Drops a cached picture (the blueprint was saved again or deleted).</summary>
	public static void Forget(string name)
	{
		if (Cache.TryGetValue(name, out Texture2D texture) && texture != null)
		{
			UnityEngine.Object.Destroy(texture);
		}
		Cache.Remove(name);
		if (Sprites.TryGetValue(name, out Sprite sprite) && sprite != null)
		{
			UnityEngine.Object.Destroy(sprite);
		}
		Sprites.Remove(name);
		Failed.Remove(name);
	}

	/// <summary>Renders at most one missing picture. Called every frame while the blueprint list or
	/// the build menu is open.</summary>
	public static void Tick()
	{
		if (ModConfig.ListThumbnails == null || !ModConfig.ListThumbnails.Value)
		{
			return;
		}
		foreach (Blueprint blueprint in BlueprintLibrary.All)
		{
			if (Cache.ContainsKey(blueprint.Name) || Failed.Contains(blueprint.Name))
			{
				continue;
			}
			Texture2D texture = null;
			try
			{
				texture = Render(blueprint);
			}
			catch (Exception ex)
			{
				Plugin.LogOnce("thumbnail", "Could not draw a blueprint preview: " + ex);
			}
			if (texture != null)
			{
				Cache[blueprint.Name] = texture;
				UseAsIcon(blueprint.Name, texture);
			}
			else
			{
				Failed.Add(blueprint.Name);
			}
			return;
		}
	}

	/// <summary>The picture also becomes the blueprint's icon in the build menu.</summary>
	private static void UseAsIcon(string name, Texture2D texture)
	{
		try
		{
			Piece stub = BlueprintPieces.StubPiece(name);
			if (stub == null)
			{
				return;
			}
			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
			sprite.name = "SnakeSkills_Blueprint_" + name;
			Sprites[name] = sprite;
			stub.m_icon = sprite;
			BuildMenuTab.RefreshIcon(stub);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("thumbnail-icon", "Could not use a blueprint picture as its icon: " + ex);
		}
	}

	private static Texture2D Render(Blueprint blueprint)
	{
		Piece stub = BlueprintPieces.StubPiece(blueprint.Name);
		if (stub == null)
		{
			return null;
		}
		EnsureRig();
		// Like the game's own placement ghost: no network registration while the copy exists.
		bool forceDisableInit = ZNetView.m_forceDisableInit;
		ZNetView.m_forceDisableInit = true;
		GameObject clone;
		try
		{
			clone = UnityEngine.Object.Instantiate(stub.gameObject, StagePosition, Quaternion.identity);
		}
		finally
		{
			ZNetView.m_forceDisableInit = forceDisableInit;
		}
		try
		{
			Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(includeInactive: false);
			if (renderers.Length == 0)
			{
				return null;
			}
			Bounds bounds = default;
			bool first = true;
			foreach (Renderer renderer in renderers)
			{
				renderer.gameObject.layer = _layer;
				if (renderer is ParticleSystemRenderer)
				{
					renderer.enabled = false;
					continue;
				}
				if (first)
				{
					bounds = renderer.bounds;
					first = false;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
			foreach (LODGroup lod in clone.GetComponentsInChildren<LODGroup>(includeInactive: true))
			{
				lod.ForceLOD(0);
			}
			if (first)
			{
				return null;
			}
			float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
			Vector3 view = new Vector3(-1f, 0.75f, -1f).normalized;
			float distance = radius / Mathf.Sin(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
			_camera.transform.position = bounds.center + view * distance;
			_camera.transform.LookAt(bounds.center);
			_camera.nearClipPlane = Mathf.Max(0.05f, distance - radius * 2f);
			_camera.farClipPlane = distance + radius * 2f;
			_light.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, -1f, 0.8f));
			_camera.targetTexture = _target;
			_light.enabled = true;
			_camera.Render();
			_light.enabled = false;
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = _target;
			Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGB24, mipChain: false);
			texture.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
			texture.Apply();
			RenderTexture.active = previous;
			return texture;
		}
		finally
		{
			// 1.24: nothing of the photo rig outlives the render. 1.23 kept the camera and its
			// directional light around, and the extra light upset the game's own lighting (black
			// player, washed-out plants, blurry ground) and could end in a crash.
			UnityEngine.Object.DestroyImmediate(clone);
			DestroyRig();
		}
	}

	private static void EnsureRig()
	{
		if (_layer < 0)
		{
			_layer = FallbackLayer;
			for (int i = 31; i >= 8; i--)
			{
				if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
				{
					_layer = i;
					break;
				}
			}
		}
		if (_target == null)
		{
			_target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
			_target.Create();
		}
		if (_camera == null)
		{
			GameObject rig = new GameObject("SnakeSkills_BlueprintThumbnailCamera");
						_camera = rig.AddComponent<Camera>();
			_camera.enabled = false;
			_camera.cullingMask = 1 << _layer;
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = new Color(0.16f, 0.13f, 0.1f, 1f);
			_camera.fieldOfView = 30f;
			_camera.allowHDR = false;
			_camera.allowMSAA = false;
			GameObject lightObject = new GameObject("SnakeSkills_BlueprintThumbnailLight");
			lightObject.transform.SetParent(rig.transform, worldPositionStays: false);
			_light = lightObject.AddComponent<Light>();
			_light.type = LightType.Directional;
			_light.intensity = 1.2f;
			_light.color = new Color(1f, 0.96f, 0.9f);
			_light.cullingMask = 1 << _layer;
			_light.shadows = LightShadows.None;
			_light.enabled = false;
		}
	}

	private static void DestroyRig()
	{
		if (_camera != null)
		{
			_camera.targetTexture = null;
			UnityEngine.Object.DestroyImmediate(_camera.gameObject);
		}
		_camera = null;
		_light = null;
	}
}
