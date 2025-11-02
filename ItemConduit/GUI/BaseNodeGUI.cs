using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Config;
using Jotunn.GUI;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using Logger = Jotunn.Logger;
using JotunnGUI = Jotunn.Managers.GUIManager;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Base class for all node GUI windows with wireframe-based design
	/// Matches the size and position of Valheim's Crafting panel
	/// </summary>
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		#region Fields

		protected GameObject uiRoot;
		protected GameObject panel;
		protected RectTransform panelRect;
		protected bool isVisible = false;

		// Constants for item grid (8 columns x 7 rows = 56 items visible)
		protected const int GRID_COLUMNS = 8;
		//protected const int GRID_ROWS = 7;
		protected const int ITEM_SLOT_SIZE = 64;

		// Item management
		protected List<ItemDrop.ItemData> allItems = new List<ItemDrop.ItemData>();
		protected List<ItemDrop.ItemData> filteredItems = new List<ItemDrop.ItemData>();

		// Category management
		protected enum Category
		{
			CurrentlyFiltered,
			All,
			Weapons,
			Armors,
			Foods,
			Materials,
			Consumables,
			Tools,
			Trophies,
			Misc
		}
		protected Category currentCategory = Category.All;

		// Input field tracking for focus management
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

			// Track input field focus
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

		protected abstract Vector2 GetPanelSize();

		#endregion

		#region Show/Hide

		public virtual void Show()
		{
			if (uiRoot != null)
			{
				uiRoot.SetActive(true);
				isVisible = true;

				GUIController.Instance.RegisterGUI(this);
				RegisterInputFieldEvents();
			}
		}

		public virtual void Hide()
		{
			if (uiRoot != null)
			{
				uiRoot.SetActive(false);
				isVisible = false;

				UnregisterInputFieldEvents();
				GUIController.Instance.UnregisterGUI(this);
			}
		}

		public bool IsVisible()
		{
			return isVisible;
		}

		#endregion

		#region Input Field Tracking

		protected void RegisterInputFieldEvents()
		{
			trackedInputFields.Clear();
			foreach (InputField inputField in panel.GetComponentsInChildren<InputField>(true))
			{
				trackedInputFields.Add(inputField);
			}
		}

		protected void UnregisterInputFieldEvents()
		{
			trackedInputFields.Clear();
		}

		#endregion

		#region UI Creation Helpers

		protected GameObject CreateInputField(Transform parent, string placeholder, float width)
		{
			GameObject inputObj = new GameObject("InputField");
			inputObj.transform.SetParent(parent, false);

			Image bg = inputObj.AddComponent<Image>();
			bg.color = new Color(0, 0, 0, 0.5f);

			InputField inputField = inputObj.AddComponent<InputField>();
			inputField.textComponent = CreateTextComponent(inputObj.transform, "Text");

			GameObject placeholderObj = new GameObject("Placeholder");
			placeholderObj.transform.SetParent(inputObj.transform, false);
			Text placeholderText = CreateTextComponent(placeholderObj.transform, "Placeholder");
			placeholderText.text = placeholder;
			placeholderText.color = new Color(1, 1, 1, 0.5f);
			inputField.placeholder = placeholderText;

			LayoutElement layout = inputObj.AddComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = 30;

			return inputObj;
		}

		protected Text CreateTextComponent(Transform parent, string name)
		{
			GameObject textObj = new GameObject(name);
			textObj.transform.SetParent(parent, false);

			RectTransform rect = textObj.AddComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(5, 2);
			rect.offsetMax = new Vector2(-5, -2);

			Text text = textObj.AddComponent<Text>();
			text.alignment = TextAnchor.MiddleLeft;
			text.color = Color.white;

			return text;
		}

		protected GameObject CreateToggle(Transform parent, string label, bool defaultValue)
		{
			GameObject toggleObj = new GameObject("Toggle");
			toggleObj.transform.SetParent(parent, false);

			Toggle toggle = toggleObj.AddComponent<Toggle>();
			toggle.isOn = defaultValue;

			// Background
			GameObject bgObj = new GameObject("Background");
			bgObj.transform.SetParent(toggleObj.transform, false);
			Image bgImage = bgObj.AddComponent<Image>();
			bgImage.color = new Color(0.2f, 0.2f, 0.2f);
			RectTransform bgRect = bgObj.GetComponent<RectTransform>();
			bgRect.sizeDelta = new Vector2(20, 20);

			// Checkmark
			GameObject checkObj = new GameObject("Checkmark");
			checkObj.transform.SetParent(bgObj.transform, false);
			Image checkImage = checkObj.AddComponent<Image>();
			checkImage.color = Color.white;
			toggle.graphic = checkImage;
			RectTransform checkRect = checkObj.GetComponent<RectTransform>();
			checkRect.anchorMin = Vector2.zero;
			checkRect.anchorMax = Vector2.one;
			checkRect.sizeDelta = new Vector2(-6, -6);

			toggle.targetGraphic = bgImage;

			// Label
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(toggleObj.transform, false);
			Text labelText = labelObj.AddComponent<Text>();
			labelText.text = label;
			labelText.alignment = TextAnchor.MiddleLeft;
			labelText.color = Color.white;
			RectTransform labelRect = labelObj.GetComponent<RectTransform>();
			labelRect.anchorMin = new Vector2(0, 0);
			labelRect.anchorMax = new Vector2(1, 1);
			labelRect.offsetMin = new Vector2(28, 0);
			labelRect.offsetMax = Vector2.zero;

			HorizontalLayoutGroup hLayout = toggleObj.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 8;
			hLayout.childForceExpandWidth = false;

			LayoutElement layout = toggleObj.AddComponent<LayoutElement>();
			layout.preferredHeight = 30;

			return toggleObj;
		}

		protected Button CreateButton(Transform parent, string text, float width, float height)
		{
			GameObject buttonObj = new GameObject("Button");
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
			layout.preferredHeight = height;

			return button;
		}

		protected GameObject CreateSpacer(Transform parent, float height)
		{
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(parent, false);

			LayoutElement layout = spacer.AddComponent<LayoutElement>();
			layout.preferredHeight = height;

			return spacer;
		}

		#endregion

		#region Item Management

		protected void LoadItemDatabase()
		{
			allItems.Clear();

			if (ObjectDB.instance == null) return;

			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
				if (itemDrop != null && itemDrop.m_itemData != null)
				{
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
						if (DebugConfig.showDebug.Value)
						{
							Logger.LogWarning($"[ItemConduit] Skipping item with invalid icon: {prefab.name}");
						}
					}
				}
			}
		}

		protected bool MatchesCategory(ItemDrop.ItemData item, Category category)
		{
			if (category == Category.All) return true;

			ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;

			switch (category)
			{
				case Category.Weapons:
					return itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
						   itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
						   itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft ||
						   itemType == ItemDrop.ItemData.ItemType.Bow ||
						   itemType == ItemDrop.ItemData.ItemType.Torch;
				case Category.Armors:
					return itemType == ItemDrop.ItemData.ItemType.Helmet ||
						   itemType == ItemDrop.ItemData.ItemType.Chest ||
						   itemType == ItemDrop.ItemData.ItemType.Legs ||
						   itemType == ItemDrop.ItemData.ItemType.Shoulder ||
						   itemType == ItemDrop.ItemData.ItemType.Hands ||
						   itemType == ItemDrop.ItemData.ItemType.Shield;
				case Category.Foods:
					return itemType == ItemDrop.ItemData.ItemType.Consumable && item.m_shared.m_food > 0;
				case Category.Materials:
					return itemType == ItemDrop.ItemData.ItemType.Material;
				case Category.Consumables:
					return itemType == ItemDrop.ItemData.ItemType.Consumable;
				case Category.Tools:
					return itemType == ItemDrop.ItemData.ItemType.Tool;
				case Category.Trophies:
					return itemType == ItemDrop.ItemData.ItemType.Trophy;
				case Category.Misc:
					return itemType == ItemDrop.ItemData.ItemType.Misc;
				default:
					return false;
			}
		}

		protected string GetCategoryDisplayName(Category category)
		{
			switch (category)
			{
				case Category.CurrentlyFiltered: return "Currently Filtered";
				case Category.All: return "All";
				case Category.Weapons: return "Weapons";
				case Category.Armors: return "Armors";
				case Category.Foods: return "Foods";
				case Category.Materials: return "Materials";
				case Category.Consumables: return "Consumables";
				case Category.Tools: return "Tools";
				case Category.Trophies: return "Trophies";
				case Category.Misc: return "Misc";
				default: return "Unknown";
			}
		}

		#endregion

		#region Styling

		protected void ApplyJotunnStyling(GameObject root)
		{
			// Apply text styling
			foreach (Text text in root.GetComponentsInChildren<Text>(true))
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
				else
				{
					JotunnGUI.Instance.ApplyTextStyle(
						text,
						JotunnGUI.Instance.AveriaSerif,
						Color.white,
						16,
						false
					);
				}
			}

			// Apply input field styling
			foreach (InputField inputField in root.GetComponentsInChildren<InputField>(true))
			{
				JotunnGUI.Instance.ApplyInputFieldStyle(inputField, 16);
			}

			// Apply toggle styling
			foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
			{
				JotunnGUI.Instance.ApplyToogleStyle(toggle);
			}

			// Apply button styling
			foreach (Button button in root.GetComponentsInChildren<Button>(true))
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
			public Image highlightBorder;
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
				if (highlightBorder != null)
				{
					highlightBorder.enabled = highlight;
				}
			}

			public ItemDrop.ItemData GetCurrentItem()
			{
				return currentItem;
			}
		}

		#endregion
	}
}