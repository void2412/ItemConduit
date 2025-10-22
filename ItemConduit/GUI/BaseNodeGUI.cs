using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Config;
using Jotunn.GUI;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using Logger = Jotunn.Logger;
using JotunnGUI = Jotunn.Managers.GUIManager;
using System;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Base class for all node GUI windows with Jötunn integration and common UI helpers
	/// </summary>
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		#region Fields

		protected GameObject uiRoot;
		protected GameObject panel;
		protected RectTransform panelRect;
		protected bool isVisible = false;

		protected static readonly Color WhiteShade = new Color(219f / 255f, 219f / 255f, 219f / 255f);

		// Constants for item grid
		protected const int GRID_COLUMNS = 8;
		protected const int GRID_ROWS = 7;
		protected const int ITEM_SLOT_SIZE = 70;

		// Item management
		protected List<ItemDrop.ItemData> allItems = new List<ItemDrop.ItemData>();
		protected List<ItemDrop.ItemData> filteredItems = new List<ItemDrop.ItemData>();

		private List<InputField> trackedInputFields = new List<InputField>();
		private bool wasAnyInputFieldFocused = false;

		#endregion

		#region Lifecycle

		protected virtual void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}

		protected virtual void Update()
		{
			if (!isVisible) return;

			// Check if any input field currently has focus
			bool anyInputFieldFocused = false;
			foreach (var inputField in trackedInputFields)
			{
				if (inputField != null && inputField.isFocused)
				{
					anyInputFieldFocused = true;
					break;
				}
			}

			// Notify GUIController when focus state changes
			if (anyInputFieldFocused != wasAnyInputFieldFocused)
			{
				if (anyInputFieldFocused)
				{
					GUIController.Instance.OnInputFieldFocused();
				}
				else
				{
					GUIController.Instance.OnInputFieldUnfocused();
				}
				wasAnyInputFieldFocused = anyInputFieldFocused;
			}

			// Handle Escape key
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				// If any input field has focus, Escape unfocuses it (Unity does this automatically)
				// If no input field has focus, close the GUI
				if (!anyInputFieldFocused)
				{
					Hide();
				}
			}
		}

		#endregion

		#region Initialization

		protected virtual void InitializeBaseNodeUI()
		{
			CreateJotunnPanel();
		}

		protected virtual void CreateJotunnPanel()
		{
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

			panel = CreateStyledPanel();
			panel.transform.SetParent(uiRoot.transform, false);
			panel.AddComponent<DragWindowCntrl>();

			panelRect = panel.GetComponent<RectTransform>();
			panelRect.anchorMin = new Vector2(0.5f, 0.5f);
			panelRect.anchorMax = new Vector2(0.5f, 0.5f);
			panelRect.pivot = new Vector2(0.5f, 0.5f);
			panelRect.anchoredPosition = Vector2.zero;
			panelRect.sizeDelta = GetPanelSize();

			uiRoot.SetActive(false);
		}

		protected GameObject CreateStyledPanel()
		{
			GameObject panel = new GameObject("Panel");
			RectTransform rect = panel.AddComponent<RectTransform>();

			Image background = panel.AddComponent<Image>();
			background.color = GUIManager.Instance.ValheimOrange;

			return panel;
		}

		#endregion

		#region Show/Hide

		public virtual void Show()
		{
			if (uiRoot != null)
			{
				uiRoot.SetActive(true);
				isVisible = true;

				// Register with GUIController
				GUIController.Instance.RegisterGUI(this);

				// Find and track all input fields
				RegisterInputFieldEvents();
			}
		}

		public virtual void Hide()
		{
			if (uiRoot != null)
			{
				uiRoot.SetActive(false);
				isVisible = false;

				// Unregister input field tracking
				UnregisterInputFieldEvents();

				// Unregister from GUIController
				GUIController.Instance.UnregisterGUI(this);
			}
		}

		public bool IsVisible()
		{
			return isVisible;
		}

		#endregion

		#region Input Field Focus Tracking

		/// <summary>
		/// Find and register all input fields in the GUI for focus tracking
		/// </summary>
		private void RegisterInputFieldEvents()
		{
			if (panel == null) return;

			// Clear previous tracked fields
			trackedInputFields.Clear();
			wasAnyInputFieldFocused = false;

			// Find all input fields in the panel
			var inputFields = panel.GetComponentsInChildren<InputField>(true);

			foreach (InputField inputField in inputFields)
			{
				if (inputField != null)
				{
					trackedInputFields.Add(inputField);
				}
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Registered {trackedInputFields.Count} input fields for focus tracking");
			}
		}

		/// <summary>
		/// Clear input field tracking
		/// </summary>
		private void UnregisterInputFieldEvents()
		{
			trackedInputFields.Clear();
			wasAnyInputFieldFocused = false;

			// Make sure to notify GUIController that no input field has focus
			GUIController.Instance.OnInputFieldUnfocused();
		}

		#endregion

		#region UI Helper Methods - Common to all Node GUIs

		protected GameObject CreateStandardInputField(Transform parent, string placeholder)
		{
			GameObject inputObj = new GameObject("InputField");
			inputObj.transform.SetParent(parent, false);

			Image bg = inputObj.AddComponent<Image>();
			bg.color = new Color(0, 0, 0, 0.5f);

			InputField input = inputObj.AddComponent<InputField>();
			input.transition = Selectable.Transition.ColorTint;

			// Text
			GameObject textArea = new GameObject("Text");
			textArea.transform.SetParent(inputObj.transform, false);
			RectTransform textRect = textArea.AddComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = new Vector2(-10, 0);

			Text text = textArea.AddComponent<Text>();
			text.supportRichText = false;
			text.alignment = TextAnchor.MiddleLeft;
			text.color = Color.white;
			input.textComponent = text;

			// Placeholder
			GameObject placeholderObj = new GameObject("Placeholder");
			placeholderObj.transform.SetParent(inputObj.transform, false);
			RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
			placeholderRect.anchorMin = Vector2.zero;
			placeholderRect.anchorMax = Vector2.one;
			placeholderRect.sizeDelta = new Vector2(-10, 0);

			Text placeholderText = placeholderObj.AddComponent<Text>();
			placeholderText.text = placeholder;
			placeholderText.fontStyle = FontStyle.Italic;
			placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
			placeholderText.alignment = TextAnchor.MiddleLeft;
			input.placeholder = placeholderText;

			LayoutElement layout = inputObj.AddComponent<LayoutElement>();
			layout.preferredHeight = 30;
			layout.flexibleWidth = 1;

			return inputObj;
		}

		protected GameObject CreateStandardToggle(Transform parent, string label, bool defaultValue)
		{
			GameObject toggleObj = new GameObject("Toggle");
			toggleObj.transform.SetParent(parent, false);

			Toggle toggle = toggleObj.AddComponent<Toggle>();
			toggle.isOn = defaultValue;

			// Background
			GameObject bg = new GameObject("Background");
			bg.transform.SetParent(toggleObj.transform, false);
			RectTransform bgRect = bg.AddComponent<RectTransform>();
			bgRect.sizeDelta = new Vector2(20, 20);
			Image bgImage = bg.AddComponent<Image>();
			bgImage.color = new Color(0.2f, 0.2f, 0.2f);

			// Checkmark
			GameObject checkmark = new GameObject("Checkmark");
			checkmark.transform.SetParent(bg.transform, false);
			RectTransform checkRect = checkmark.AddComponent<RectTransform>();
			checkRect.anchorMin = Vector2.zero;
			checkRect.anchorMax = Vector2.one;
			checkRect.sizeDelta = new Vector2(-4, -4);
			Image checkImage = checkmark.AddComponent<Image>();
			checkImage.color = new Color(0.2f, 0.8f, 0.2f);

			toggle.targetGraphic = bgImage;
			toggle.graphic = checkImage;

			// Label
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(toggleObj.transform, false);
			Text labelText = labelObj.AddComponent<Text>();
			labelText.text = label;
			labelText.alignment = TextAnchor.MiddleLeft;
			RectTransform labelRect = labelObj.GetComponent<RectTransform>();
			labelRect.anchorMin = new Vector2(0, 0);
			labelRect.anchorMax = new Vector2(1, 1);
			labelRect.offsetMin = new Vector2(25, 0);
			labelRect.offsetMax = Vector2.zero;

			HorizontalLayoutGroup hLayout = toggleObj.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 5;
			hLayout.childForceExpandWidth = false;
			hLayout.childAlignment = TextAnchor.MiddleLeft;

			LayoutElement layout = toggleObj.AddComponent<LayoutElement>();
			layout.preferredHeight = 30;

			return toggleObj;
		}

		protected Button CreateStandardButton(Transform parent, string text, float width)
		{
			GameObject buttonObj = new GameObject("Button_" + text);
			buttonObj.transform.SetParent(parent, false);

			Image bg = buttonObj.AddComponent<Image>();
			bg.color = new Color(0.3f, 0.25f, 0.2f);

			Button button = buttonObj.AddComponent<Button>();
			button.targetGraphic = bg;

			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(buttonObj.transform, false);
			RectTransform textRect = textObj.AddComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = Vector2.zero;

			Text buttonText = textObj.AddComponent<Text>();
			buttonText.text = text;
			buttonText.alignment = TextAnchor.MiddleCenter;
			buttonText.color = Color.white;

			LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = 40;

			return button;
		}

		protected void CreateSpacer(Transform parent, float height)
		{
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(parent, false);

			LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = height;
		}

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


		#region Item Management - Common Logic

		protected void LoadItemDatabase()
		{
			allItems.Clear();

			if (ObjectDB.instance == null) return;

			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
				if (itemDrop != null && itemDrop.m_itemData != null)
				{
					// Only add items that have valid icons
					try
					{
						Sprite icon = itemDrop.m_itemData.GetIcon();
						if (icon != null)
						{
							allItems.Add(itemDrop.m_itemData);
						}
					}
					catch
					{
						// Skip items with invalid icon data
						if (DebugConfig.showDebug.Value)
						{
							Logger.LogWarning($"[ItemConduit] Skipping item with invalid icon: {prefab.name}");
						}
					}
				}
			}
		}

		protected bool MatchesCategory(ItemDrop.ItemData item, string category)
		{
			if (category == "All") return true;

			ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;

			switch (category)
			{
				case "Weapons":
					return itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
						   itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
						   itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft ||
						   itemType == ItemDrop.ItemData.ItemType.Bow ||
						   itemType == ItemDrop.ItemData.ItemType.Torch;
				case "Armors":
					return itemType == ItemDrop.ItemData.ItemType.Helmet ||
						   itemType == ItemDrop.ItemData.ItemType.Chest ||
						   itemType == ItemDrop.ItemData.ItemType.Legs ||
						   itemType == ItemDrop.ItemData.ItemType.Shoulder ||
						   itemType == ItemDrop.ItemData.ItemType.Hands ||
						   itemType == ItemDrop.ItemData.ItemType.Shield;
				case "Foods":
					return itemType == ItemDrop.ItemData.ItemType.Consumable && item.m_shared.m_food > 0;
				case "Materials":
					return itemType == ItemDrop.ItemData.ItemType.Material;
				case "Consumables":
					return itemType == ItemDrop.ItemData.ItemType.Consumable;
				case "Tools":
					return itemType == ItemDrop.ItemData.ItemType.Tool;
				case "Trophies":
					return itemType == ItemDrop.ItemData.ItemType.Trophy;
				case "Misc":
					return itemType == ItemDrop.ItemData.ItemType.Misc;
				default:
					return false;
			}
		}

		#endregion

		#region Styling

		protected void ApplyJotunnStyling(GameObject root)
		{
			// Apply text styling
			foreach (Text text in root.GetComponentsInChildren<Text>())
			{
				if (text.name == "Title" || text.name.Contains("Title"))
				{
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerifBold,
						JotunnGUI.Instance.ValheimOrange,
						20
					);
				}
				else if (text.name.Contains("Header"))
				{
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerifBold,
						WhiteShade,
						18
					);
				}
				else
				{
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

		#endregion

		#region Item Slot Helper Class

		protected class BaseItemSlot : MonoBehaviour
		{
			public Image background;
			public Image icon;
			public Button button;
			protected ItemDrop.ItemData currentItem;

			public virtual void SetItem(ItemDrop.ItemData item)
			{
				currentItem = item;

				if (item == null)
				{
					Clear();
					return;
				}

				try
				{
					Sprite itemIcon = item.GetIcon();
					if (itemIcon != null)
					{
						icon.sprite = itemIcon;
						icon.color = Color.white;
						icon.enabled = true;
					}
					else
					{
						Clear();
					}
				}
				catch (System.Exception ex)
				{
					if (DebugConfig.showDebug.Value)
					{
						Logger.LogWarning($"[ItemConduit] Failed to get icon for item: {ex.Message}");
					}
					Clear();
				}
			}

			public virtual void Clear()
			{
				currentItem = null;
				icon.enabled = false;
			}

			public virtual void SetHighlight(bool highlight)
			{
				background.color = highlight ? new Color(1f, 0.8f, 0.3f, 1f) : Color.white;
			}

			public ItemDrop.ItemData GetCurrentItem()
			{
				return currentItem;
			}
		}

		#endregion

		#region Abstract Methods

		protected abstract Vector2 GetPanelSize();

		#endregion
	}
}