using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Nodes;
using ItemConduit.Config;
using Jotunn.GUI;
using Jotunn.Managers;
using Logger = Jotunn.Logger;
// Use alias to distinguish between our GUIController and Jötunn's GUIManager
using JotunnGUI = Jotunn.Managers.GUIManager;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Base class for all node GUI windows with Jötunn integration
	/// Matches ValheimHopper's GUI style and approach
	/// </summary>
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		#region Fields

		// UI Panel components
		protected GameObject uiRoot;
		protected GameObject panel;
		protected RectTransform panelRect;

		// Panel state
		protected bool isVisible = false;

		// Color scheme from ValheimHopper
		protected static readonly Color WhiteShade = new Color(219f / 255f, 219f / 255f, 219f / 255f);

		#endregion

		#region Lifecycle

		protected virtual void Awake()
		{
			// Keep the GUI object alive across scenes
			DontDestroyOnLoad(gameObject);
		}

		protected virtual void Update()
		{
			// Force cursor to stay visible while GUI is open
			if (isVisible)
			{
				Cursor.visible = true;
				Cursor.lockState = CursorLockMode.None;
			}
		}

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize the UI with Jötunn integration
		/// </summary>
		protected virtual void InitializeBaseNodeUI()
		{
			CreateJotunnPanel();
		}

		/// <summary>
		/// Create panel using Jötunn's GUI system
		/// </summary>
		protected virtual void CreateJotunnPanel()
		{
			// Create UI root as child of Jötunn's CustomGUIFront (like ValheimHopper)
			uiRoot = new GameObject("ItemConduitUI");
			uiRoot.transform.SetParent(JotunnGUI.CustomGUIFront.transform, false);

			Canvas canvas = uiRoot.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 100;

			CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);

			uiRoot.AddComponent<GraphicRaycaster>();
			uiRoot.AddComponent<CanvasGroup>();

			// Create the main panel
			panel = CreateStyledPanel();
			panel.transform.SetParent(uiRoot.transform, false);

			// Add drag functionality (like ValheimHopper)
			panel.AddComponent<DragWindowCntrl>();

			// Position panel in center of screen
			panelRect = panel.GetComponent<RectTransform>();
			panelRect.anchorMin = new Vector2(0.5f, 0.5f);
			panelRect.anchorMax = new Vector2(0.5f, 0.5f);
			panelRect.pivot = new Vector2(0.5f, 0.5f);
			panelRect.anchoredPosition = Vector2.zero;
			panelRect.sizeDelta = GetPanelSize();

			// Initially hidden
			uiRoot.SetActive(false);
		}

		/// <summary>
		/// Create styled panel with background
		/// </summary>
		protected GameObject CreateStyledPanel()
		{
			GameObject panel = new GameObject("Panel");
			RectTransform rect = panel.AddComponent<RectTransform>();

			// Add background image
			Image background = panel.AddComponent<Image>();
			background.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
			background.sprite = GetPanelSprite();
			background.type = Image.Type.Sliced;

			// Add border
			CreateBorder(panel);

			return panel;
		}

		/// <summary>
		/// Create border frame around panel
		/// </summary>
		protected GameObject CreateBorder(GameObject parent)
		{
			GameObject border = new GameObject("Border");
			border.transform.SetParent(parent.transform, false);

			Image borderImage = border.AddComponent<Image>();
			borderImage.color = new Color(0.5f, 0.35f, 0.15f, 1f);
			borderImage.sprite = GetBorderSprite();
			borderImage.type = Image.Type.Sliced;
			borderImage.fillCenter = false;

			RectTransform borderRect = border.GetComponent<RectTransform>();
			borderRect.anchorMin = Vector2.zero;
			borderRect.anchorMax = Vector2.one;
			borderRect.offsetMin = new Vector2(-10, -10);
			borderRect.offsetMax = new Vector2(10, 10);

			return border;
		}

		/// <summary>
		/// Get panel sprite from Valheim UI or create fallback
		/// </summary>
		protected Sprite GetPanelSprite()
		{
			if (InventoryGui.instance != null)
			{
				Image[] images = InventoryGui.instance.GetComponentsInChildren<Image>();
				foreach (var img in images)
				{
					if (img.sprite != null && img.name.ToLower().Contains("bkg"))
					{
						return img.sprite;
					}
				}
			}

			// Fallback sprite
			Texture2D tex = new Texture2D(32, 32);
			Color fillColor = new Color(0.15f, 0.12f, 0.1f);
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					tex.SetPixel(i, j, fillColor);
				}
			}
			tex.Apply();
			return Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 100f, 1, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
		}

		/// <summary>
		/// Get border sprite from Valheim UI or fallback to panel sprite
		/// </summary>
		protected Sprite GetBorderSprite()
		{
			if (InventoryGui.instance != null)
			{
				Image[] images = InventoryGui.instance.GetComponentsInChildren<Image>();
				foreach (var img in images)
				{
					if (img.sprite != null && img.name.ToLower().Contains("border"))
					{
						return img.sprite;
					}
				}
			}
			return GetPanelSprite();
		}

		#endregion

		#region Jötunn Styling

		/// <summary>
		/// Apply Jötunn styling to all components (like ValheimHopper does)
		/// </summary>
		protected void ApplyJotunnStyling(GameObject root)
		{
			// Apply text styling
			foreach (Text text in root.GetComponentsInChildren<Text>())
			{
				// Check if this is a title by name (no tags needed)
				if (text.name == "Title" || text.name.Contains("Title"))
				{
					// Title style: Bold font, Valheim Orange color, size 20
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerifBold,
						JotunnGUI.Instance.ValheimOrange,
						20
					);
				}
				else if (text.name.Contains("Header"))
				{
					// Section header style
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerifBold,
						WhiteShade,
						18
					);
				}
				else
				{
					// Body text style: Regular font, WhiteShade color, size 16
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerif,
						WhiteShade,
						16,
						false
					);
				}
			}

			// Apply input field styling
			foreach (InputField inputField in root.GetComponentsInChildren<InputField>())
			{
				JotunnGUI.Instance.ApplyInputFieldStyle(inputField, 16);
			}

			// Apply toggle styling
			foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>())
			{
				JotunnGUI.Instance.ApplyToogleStyle(toggle);
			}

			// Apply button styling
			foreach (Button button in root.GetComponentsInChildren<Button>())
			{
				JotunnGUI.Instance.ApplyButtonStyle(button);
			}
		}

		/// <summary>
		/// Apply localization to all text components
		/// </summary>
		protected void ApplyLocalization()
		{
			foreach (Text text in uiRoot.GetComponentsInChildren<Text>())
			{
				text.text = Localization.instance.Localize(text.text);
			}
		}

		#endregion

		#region Show/Hide

		/// <summary>
		/// Show the GUI window
		/// </summary>
		public virtual void Show()
		{
			if (uiRoot == null)
			{
				InitializeBaseNodeUI();
			}

			isVisible = true;
			uiRoot.SetActive(true);

			// Register with our GUIController (not Jötunn's GUIManager)
			GUIController.Instance?.RegisterGUI(this);

			// FORCE cursor to show - Valheim fights this, so we're aggressive
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;

			// Disable GameCamera mouse capture using reflection (like old code did)
			if (GameCamera.instance != null)
			{
				var mouseCaptureField = typeof(GameCamera).GetField("m_mouseCapture",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (mouseCaptureField != null)
				{
					mouseCaptureField.SetValue(GameCamera.instance, false);
				}
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Showing GUI: {GetType().Name}");
			}
		}

		/// <summary>
		/// Hide the GUI window
		/// </summary>
		public virtual void Hide()
		{
			if (uiRoot != null)
			{
				isVisible = false;
				uiRoot.SetActive(false);

				// Unregister from our GUIController (not Jötunn's GUIManager)
				GUIController.Instance?.UnregisterGUI(this);

				// Restore GameCamera mouse capture
				if (GameCamera.instance != null)
				{
					var mouseCaptureField = typeof(GameCamera).GetField("m_mouseCapture",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					if (mouseCaptureField != null)
					{
						mouseCaptureField.SetValue(GameCamera.instance, true);
					}
				}

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Hiding GUI: {GetType().Name}");
				}
			}
		}

		/// <summary>
		/// Check if GUI is currently visible
		/// </summary>
		public bool IsVisible()
		{
			return isVisible;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Create scroll view container
		/// </summary>
		protected GameObject CreateScrollView(GameObject parent)
		{
			GameObject scrollView = new GameObject("ScrollView");
			scrollView.transform.SetParent(parent.transform, false);

			ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
			Image scrollBg = scrollView.AddComponent<Image>();
			scrollBg.color = new Color(0, 0, 0, 0.3f);

			RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
			scrollRectTransform.anchorMin = Vector2.zero;
			scrollRectTransform.anchorMax = Vector2.one;
			scrollRectTransform.offsetMin = new Vector2(20, 20);
			scrollRectTransform.offsetMax = new Vector2(-20, -20);

			// Viewport
			GameObject viewport = new GameObject("Viewport");
			viewport.transform.SetParent(scrollView.transform, false);

			Image viewportImage = viewport.AddComponent<Image>();
			viewportImage.color = Color.clear;
			Mask viewportMask = viewport.AddComponent<Mask>();
			viewportMask.showMaskGraphic = false;

			RectTransform viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;

			// Content
			GameObject content = new GameObject("Content");
			content.transform.SetParent(viewport.transform, false);

			VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
			contentLayout.padding = new RectOffset(10, 10, 10, 10);
			contentLayout.spacing = 5;
			contentLayout.childForceExpandWidth = true;
			contentLayout.childForceExpandHeight = false;

			ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0, 1);
			contentRect.anchorMax = new Vector2(1, 1);
			contentRect.pivot = new Vector2(0.5f, 1);
			contentRect.anchoredPosition = Vector2.zero;

			scrollRect.content = contentRect;
			scrollRect.viewport = viewportRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.scrollSensitivity = 30;

			return scrollView;
		}

		/// <summary>
		/// Create title text (will be styled with ValheimOrange)
		/// Name it "Title" so ApplyJotunnStyling recognizes it
		/// </summary>
		protected Text CreateTitle(Transform parent, string text)
		{
			GameObject titleObj = new GameObject("Title");
			titleObj.transform.SetParent(parent, false);
			// NO TAG - we identify by GameObject name instead

			Text titleText = titleObj.AddComponent<Text>();
			titleText.text = text;
			titleText.alignment = TextAnchor.MiddleCenter;

			LayoutElement layoutElement = titleObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 40;

			return titleText;
		}

		/// <summary>
		/// Create section header
		/// </summary>
		protected Text CreateSectionHeader(Transform parent, string text)
		{
			GameObject headerObj = new GameObject("Header_" + text);
			headerObj.transform.SetParent(parent, false);

			Text headerText = headerObj.AddComponent<Text>();
			headerText.text = text;
			headerText.fontStyle = FontStyle.Bold;

			LayoutElement layoutElement = headerObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 30;

			return headerText;
		}

		/// <summary>
		/// Create info/help text
		/// </summary>
		protected Text CreateInfoText(Transform parent, string text, float spacing = 0)
		{
			GameObject infoObj = new GameObject("Info");
			infoObj.transform.SetParent(parent, false);

			Text infoText = infoObj.AddComponent<Text>();
			infoText.text = text;
			infoText.fontStyle = FontStyle.Italic;
			infoText.color = new Color(0.7f, 0.7f, 0.6f);

			LayoutElement layoutElement = infoObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 20;

			if (spacing > 0)
			{
				CreateSpacer(parent, spacing);
			}

			return infoText;
		}

		/// <summary>
		/// Create spacer for layout
		/// </summary>
		protected void CreateSpacer(Transform parent, float height)
		{
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(parent, false);

			LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = height;
		}

		/// <summary>
		/// Create horizontal layout group
		/// </summary>
		protected GameObject CreateHorizontalGroup(Transform parent)
		{
			GameObject group = new GameObject("HorizontalGroup");
			group.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.childAlignment = TextAnchor.MiddleCenter;

			LayoutElement layoutElement = group.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 30;

			return group;
		}

		#endregion

		#region Abstract Methods

		/// <summary>
		/// Get the size of this panel (must be implemented by derived classes)
		/// </summary>
		protected abstract Vector2 GetPanelSize();

		#endregion
	}
}