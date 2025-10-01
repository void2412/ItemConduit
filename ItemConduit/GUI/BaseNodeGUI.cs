using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Nodes;
using ItemConduit.Config;
using Logger = Jotunn.Logger;
using System.Reflection;

namespace ItemConduit.GUI
{
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		// UI Panel components
		protected GameObject uiRoot;
		protected GameObject panel;
		protected RectTransform panelRect;

		// Valheim UI references
		protected static GameObject valheimGUIRoot;
		protected static Font norseFont;

		// Panel state
		protected bool isVisible = false;

		protected virtual void Awake()
		{
			// Keep the GUI object alive
			DontDestroyOnLoad(gameObject);
		}

		protected virtual void InitializeBaseNodeUI()
		{
			// Get Valheim's GUI root
			if (valheimGUIRoot == null && Hud.instance != null)
			{
				valheimGUIRoot = Hud.instance.gameObject;
			}

			// Get Valheim's font from Text components (not TMP)
			if (norseFont == null && InventoryGui.instance != null)
			{
				// Try to find a Text component with the Norse font
				Text[] texts = InventoryGui.instance.GetComponentsInChildren<Text>();
				if (texts.Length > 0)
				{
					norseFont = texts[0].font;
				}
			}

			CreateValheimPanel();
		}

		protected virtual void CreateValheimPanel()
		{
			// Create UI root
			uiRoot = new GameObject("ItemConduitUI");
			Canvas canvas = uiRoot.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 100; // Render on top

			CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);

			uiRoot.AddComponent<GraphicRaycaster>();

			// Add canvas group for fade in/out
			CanvasGroup canvasGroup = uiRoot.AddComponent<CanvasGroup>();

			// Create the panel using Valheim's style
			panel = CreateStyledPanel();
			panel.transform.SetParent(uiRoot.transform, false);

			// Position panel in center of screen
			panelRect = panel.GetComponent<RectTransform>();
			panelRect.anchorMin = new Vector2(0.5f, 0.5f);
			panelRect.anchorMax = new Vector2(0.5f, 0.5f);
			panelRect.pivot = new Vector2(0.5f, 0.5f);
			panelRect.anchoredPosition = Vector2.zero;
			panelRect.sizeDelta = GetPanelSize();
		}

		protected GameObject CreateStyledPanel()
		{
			GameObject panel = new GameObject("Panel");
			RectTransform rect = panel.AddComponent<RectTransform>();

			// Add background image (dark panel)
			Image background = panel.AddComponent<Image>();
			background.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // Dark background
			background.sprite = GetPanelSprite();
			background.type = Image.Type.Sliced;

			// Add wooden border frame
			GameObject border = CreateBorder(panel);

			return panel;
		}

		protected GameObject CreateBorder(GameObject parent)
		{
			GameObject border = new GameObject("Border");
			border.transform.SetParent(parent.transform, false);

			Image borderImage = border.AddComponent<Image>();
			borderImage.color = new Color(0.5f, 0.35f, 0.15f, 1f); // Wood color
			borderImage.sprite = GetBorderSprite();
			borderImage.type = Image.Type.Sliced;
			borderImage.fillCenter = false; // Only show border, not fill

			RectTransform borderRect = border.GetComponent<RectTransform>();
			borderRect.anchorMin = Vector2.zero;
			borderRect.anchorMax = Vector2.one;
			borderRect.offsetMin = new Vector2(-10, -10);
			borderRect.offsetMax = new Vector2(10, 10);

			return border;
		}

		protected Sprite GetPanelSprite()
		{
			// Try to get a panel sprite from Valheim's UI
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

			// Fallback: create a simple sprite
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

		protected Sprite GetBorderSprite()
		{
			// Try to get a border sprite from Valheim's UI
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

			// Fallback to panel sprite
			return GetPanelSprite();
		}

		protected abstract Vector2 GetPanelSize();

		public virtual void Show()
		{
			if (uiRoot == null)
			{
				InitializeBaseNodeUI();
			}

			isVisible = true;
			uiRoot.SetActive(true);

			// Show cursor like Valheim does
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;

			// Block game input using reflection
			if (GameCamera.instance != null)
			{
				typeof(GameCamera).GetField("m_mouseCapture", BindingFlags.NonPublic | BindingFlags.Instance)
					?.SetValue(GameCamera.instance, false);
			}

			// Play UI sound
			PlayOpenSound();
		}

		public virtual void Hide()
		{
			isVisible = false;
			if (uiRoot != null)
			{
				uiRoot.SetActive(false);
			}

			// Restore game input
			if (GameCamera.instance != null)
			{
				typeof(GameCamera).GetField("m_mouseCapture", BindingFlags.NonPublic | BindingFlags.Instance)
					?.SetValue(GameCamera.instance, true);
			}

			// Play close sound
			PlayCloseSound();
		}

		protected void PlayOpenSound()
		{
			// Find and play inventory open sound
			if (Menu.instance != null)
			{
				var showMethod = typeof(Menu).GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
				if (showMethod != null)
				{
					// The Show method typically plays the UI sound
					AudioSource audioSource = Menu.instance.GetComponent<AudioSource>();
					if (audioSource != null && audioSource.clip != null)
					{
						audioSource.Play();
					}
				}
			}
		}

		protected void PlayCloseSound()
		{
			// Similar approach for close sound
			if (Menu.instance != null)
			{
				AudioSource audioSource = Menu.instance.GetComponent<AudioSource>();
				if (audioSource != null && audioSource.clip != null)
				{
					audioSource.Play();
				}
			}
		}

		protected virtual void Update()
		{
			// Handle ESC key to close
			if (isVisible && Input.GetKeyDown(KeyCode.Escape))
			{
				Hide();
			}
		}
	}
}