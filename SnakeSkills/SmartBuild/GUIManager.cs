#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace SnakeSkills.SmartBuild;

/// <summary>
/// Stand-in for the few Jotunn GUIManager calls SmartBuild's panels make, so SnakeSkills doesn't
/// need Jotunn. Widgets come from Unity's DefaultControls with the game's own sprites and fonts
/// (the same way Jotunn builds them). Driven from Plugin.Update: a front canvas is created under
/// the game's GUI root once a world is loaded, and OnCustomGUIAvailable fires each time.
/// </summary>
internal sealed class GUIManager
{
	public static GUIManager Instance { get; } = new GUIManager();

	public static GameObject CustomGUIFront { get; private set; }

	public static event Action OnCustomGUIAvailable;

	public Color ValheimOrange = new Color(1f, 0.631f, 0.235f, 1f);

	public Font AveriaSerif { get; private set; }

	public Font AveriaSerifBold { get; private set; }

	private DefaultControls.Resources _resources;

	private SpriteAtlas _uiAtlas;

	private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

	private bool _assetsLoaded;

	private static readonly ColorBlock ButtonColors = new ColorBlock
	{
		normalColor = new Color(0.824f, 0.824f, 0.824f, 1f),
		highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f),
		pressedColor = new Color(0.537f, 0.556f, 0.556f, 1f),
		selectedColor = new Color(0.824f, 0.824f, 0.824f, 1f),
		disabledColor = new Color(0.566f, 0.566f, 0.566f, 0.502f),
		colorMultiplier = 1f,
		fadeDuration = 0.1f
	};

	private static readonly ColorBlock ToggleColors = new ColorBlock
	{
		normalColor = new Color(0.61f, 0.61f, 0.61f, 1f),
		highlightedColor = new Color(1f, 1f, 1f, 1f),
		pressedColor = new Color(0.784f, 0.784f, 0.784f, 1f),
		selectedColor = new Color(1f, 1f, 1f, 1f),
		disabledColor = new Color(0.784f, 0.784f, 0.784f, 0.502f),
		colorMultiplier = 1f,
		fadeDuration = 0.1f
	};

	/// <summary>Creates the front canvas when a world's GUI exists and there is none yet.</summary>
	public static void Tick()
	{
		if (CustomGUIFront != null || Hud.instance == null)
		{
			return;
		}
		try
		{
			Transform root = FindGUIRoot();
			if (root == null)
			{
				return;
			}
			Instance.LoadAssets();
			CustomGUIFront = CreateCanvas("SnakeSkillsGUIFront", 2000, root);
			CustomGUIFront.transform.SetAsLastSibling();
			OnCustomGUIAvailable?.Invoke();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("gui-root", "Could not set up the Smart Hammer's windows: " + ex);
		}
	}

	private static Transform FindGUIRoot()
	{
		foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
		{
			if (go.name == "GuiRoot")
			{
				return go.transform.Find("GUI") ?? go.transform;
			}
		}
		return Hud.instance != null ? Hud.instance.transform.parent : null;
	}

	private static GameObject CreateCanvas(string name, int sortingOrder, Transform parent)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		go.layer = LayerMask.NameToLayer("UI");
		go.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		Canvas canvas = go.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = sortingOrder;
		go.GetComponent<CanvasScaler>().referencePixelsPerUnit = 50f;
		return go;
	}

	private void LoadAssets()
	{
		if (_assetsLoaded && AveriaSerif != null)
		{
			return;
		}
		_assetsLoaded = true;
		foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
		{
			if (font.name == "AveriaSerifLibre-Regular")
			{
				AveriaSerif = font;
			}
			else if (font.name == "AveriaSerifLibre-Bold")
			{
				AveriaSerifBold = font;
			}
		}
		Font fallback = AveriaSerif ?? AveriaSerifBold ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		AveriaSerif ??= fallback;
		AveriaSerifBold ??= fallback;
		foreach (SpriteAtlas atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
		{
			if (atlas.name == "UIAtlas")
			{
				_uiAtlas = atlas;
			}
		}
		_resources = new DefaultControls.Resources
		{
			standard = GetSprite("button"),
			background = GetSprite("text_field"),
			inputField = GetSprite("text_field"),
			knob = GetSprite("checkbox_marker"),
			checkmark = GetSprite("checkbox_marker"),
			dropdown = GetSprite("checkbox_marker")
		};
	}

	public Sprite GetSprite(string name)
	{
		if (_sprites.TryGetValue(name, out Sprite cached) && cached != null)
		{
			return cached;
		}
		Sprite sprite = null;
		try
		{
			sprite = _uiAtlas != null ? _uiAtlas.GetSprite(name) : null;
		}
		catch (Exception)
		{
		}
		if (sprite == null)
		{
			foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
			{
				if (s.name == name)
				{
					sprite = s;
					break;
				}
			}
		}
		_sprites[name] = sprite;
		return sprite;
	}

	private static void SetSize(RectTransform rect, float width, float height)
	{
		if (width > 0f)
		{
			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
		}
		if (height > 0f)
		{
			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
		}
	}

	public GameObject CreateWoodpanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, float width = 0f, float height = 0f)
	{
		LoadAssets();
		GameObject panel = DefaultControls.CreatePanel(_resources);
		panel.transform.SetParent(parent, worldPositionStays: false);
		Image image = panel.GetComponent<Image>();
		Sprite wood = GetSprite("woodpanel_trophys");
		if (wood != null)
		{
			image.sprite = wood;
			image.type = Image.Type.Sliced;
			image.color = Color.white;
		}
		else
		{
			image.sprite = null;
			image.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);
		}
		RectTransform rect = (RectTransform)panel.transform;
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.anchoredPosition = position;
		SetSize(rect, width, height);
		return panel;
	}

	public GameObject CreateText(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Font font, int fontSize, Color color, bool outline, Color outlineColor, float width, float height, bool addContentSizeFitter)
	{
		LoadAssets();
		GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.anchoredPosition = position;
		if (addContentSizeFitter)
		{
			go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		}
		else
		{
			SetSize(rect, width, height);
		}
		Text txt = go.GetComponent<Text>();
		txt.text = text;
		ApplyTextStyle(txt, font, color, fontSize, outline);
		if (outline && go.GetComponent<Outline>() is { } o)
		{
			o.effectColor = outlineColor;
		}
		go.transform.SetParent(parent, worldPositionStays: false);
		return go;
	}

	public void ApplyTextStyle(Text text, Font font, Color color, int fontSize = 16, bool createOutline = true)
	{
		text.font = font ?? AveriaSerifBold;
		text.fontSize = fontSize;
		text.color = color;
		if (createOutline)
		{
			Outline outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
			outline.effectColor = Color.black;
		}
	}

	public GameObject CreateButton(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, float width = 0f, float height = 0f)
	{
		LoadAssets();
		GameObject go = DefaultControls.CreateButton(_resources);
		go.transform.SetParent(parent, worldPositionStays: false);
		Button button = go.GetComponent<Button>();
		Image image = go.GetComponent<Image>();
		if (image != null)
		{
			image.type = Image.Type.Sliced;
			button.image = image;
		}
		button.colors = ButtonColors;
		Text txt = go.GetComponentInChildren<Text>(includeInactive: true);
		if (txt != null)
		{
			txt.text = text;
			ApplyTextStyle(txt, AveriaSerifBold, ValheimOrange, 16);
			txt.alignment = TextAnchor.MiddleCenter;
		}
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.anchoredPosition = position;
		SetSize(rect, width, height);
		return go;
	}

	public GameObject CreateInputField(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, InputField.ContentType contentType = InputField.ContentType.Standard, string placeholderText = null, int fontSize = 16, float width = 0f, float height = 0f)
	{
		LoadAssets();
		GameObject go = DefaultControls.CreateInputField(_resources);
		go.transform.SetParent(parent, worldPositionStays: false);
		InputField fieldItem = go.GetComponent<InputField>();
		if (fieldItem.targetGraphic is Image image)
		{
			image.color = Color.white;
		}
		if (fieldItem.placeholder is Text placeholder)
		{
			placeholder.font = AveriaSerifBold;
			placeholder.color = Color.grey;
			placeholder.fontSize = fontSize;
			placeholder.text = placeholderText ?? "";
		}
		if (fieldItem.textComponent != null)
		{
			ApplyTextStyle(fieldItem.textComponent, AveriaSerifBold, Color.white, fontSize);
		}
		fieldItem.contentType = contentType;
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.anchoredPosition = position;
		SetSize(rect, width, height);
		return go;
	}

	public GameObject CreateToggle(Transform parent, float width, float height)
	{
		LoadAssets();
		GameObject go = DefaultControls.CreateToggle(_resources);
		go.transform.SetParent(parent, worldPositionStays: false);
		Toggle toggle = go.GetComponent<Toggle>();
		toggle.toggleTransition = Toggle.ToggleTransition.Fade;
		toggle.colors = ToggleColors;
		if (toggle.targetGraphic is Image background && GetSprite("checkbox") is { } box)
		{
			background.sprite = box;
		}
		if (toggle.graphic is Image check)
		{
			check.color = new Color(1f, 0.678f, 0.103f, 1f);
		}
		SetSize((RectTransform)go.transform, width, height);
		if (go.transform.Find("Background") is RectTransform bg)
		{
			SetSize(bg, width, height);
			if (bg.Find("Checkmark") is RectTransform mark)
			{
				SetSize(mark, width, height);
			}
		}
		return go;
	}
}
