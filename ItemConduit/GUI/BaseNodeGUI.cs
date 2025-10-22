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
	/// Base class for all node GUI windows using Jötunn GUIManager
	/// </summary>
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		#region Fields

		protected GameObject panel;
		protected RectTransform panelRect;
		protected bool isVisible = false;

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
			// Create wood panel using Jötunn
			panel = JotunnGUI.Instance.CreateWoodpanel(
				parent: JotunnGUI.CustomGUIFront.transform,
				anchorMin: new Vector2(0.5f, 0.5f),
				anchorMax: new Vector2(0.5f, 0.5f),
				position: new Vector2(0, 0),
				width: GetPanelSize().x,
				height: GetPanelSize().y,
				draggable: true
			);

			panel.name = "ItemConduitPanel";
			panelRect = panel.GetComponent<RectTransform>();

			panel.SetActive(false);
		}

		#endregion

		#region Show/Hide

		public virtual void Show()
		{
			if (panel != null)
			{
				panel.SetActive(true);
				isVisible = true;

				// Register with GUIController
				GUIController.Instance.RegisterGUI(this);

				// Find and track all input fields
				RegisterInputFieldEvents();
			}
		}

		public virtual void Hide()
		{
			if (panel != null)
			{
				panel.SetActive(false);
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

		private void RegisterInputFieldEvents()
		{
			if (panel == null) return;

			trackedInputFields.Clear();
			wasAnyInputFieldFocused = false;

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

		private void UnregisterInputFieldEvents()
		{
			trackedInputFields.Clear();
			wasAnyInputFieldFocused = false;
			GUIController.Instance.OnInputFieldUnfocused();
		}

		#endregion

		#region UI Helper Methods - Using Jötunn GUIManager

		/// <summary>
		/// Create a text element using Jötunn
		/// </summary>
		protected Text CreateJotunnText(Transform parent, string text, int fontSize = 16,
			TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false, float width = 100, float height = 30)
		{
			GameObject textObj = new GameObject("Text_" + text);
			textObj.transform.SetParent(parent, false);

			Text textComponent = textObj.AddComponent<Text>();
			textComponent.text = text;
			textComponent.alignment = alignment;

			// Apply Jötunn text styling
			JotunnGUI.Instance.ApplyTextStyle(
				textComponent,
				bold ? JotunnGUI.Instance.AveriaSerifBold : JotunnGUI.Instance.AveriaSerif,
				Color.white,
				fontSize,
				bold
			);

			// Add layout element for sizing
			LayoutElement layout = textObj.AddComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = height;

			return textComponent;
		}

		/// <summary>
		/// Create an input field using Jötunn
		/// </summary>
		protected InputField CreateJotunnInputField(Transform parent, string placeholder,
			float width = 150, float height = 30)
		{
			GameObject inputObj = JotunnGUI.Instance.CreateInputField(
				parent: parent,
				anchorMin: Vector2.zero,
				anchorMax: Vector2.one,
				position: Vector2.zero,
				contentType: InputField.ContentType.Standard,
				placeholderText: placeholder,
				fontSize: 16,
				width: width,
				height: height
			);

			InputField inputField = inputObj.GetComponent<InputField>();

			// Add layout element
			if (!inputObj.GetComponent<LayoutElement>())
			{
				LayoutElement layout = inputObj.AddComponent<LayoutElement>();
				layout.preferredWidth = width;
				layout.preferredHeight = height;
			}

			return inputField;
		}

		/// <summary>
		/// Create a toggle using Jötunn
		/// </summary>
		protected Toggle CreateJotunnToggle(Transform parent, string label, bool defaultValue = true,
			float width = 150, float height = 30)
		{
			GameObject toggleObj = JotunnGUI.Instance.CreateToggle(
				parent: parent,
				width: width,
				height: height
			);

			Toggle toggle = toggleObj.GetComponent<Toggle>();
			toggle.isOn = defaultValue;

			// Add label
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(toggleObj.transform, false);

			Text labelText = labelObj.AddComponent<Text>();
			labelText.text = label;
			labelText.alignment = TextAnchor.MiddleLeft;

			JotunnGUI.Instance.ApplyTextStyle(labelText, JotunnGUI.Instance.AveriaSerif, Color.white, 16);

			RectTransform labelRect = labelObj.GetComponent<RectTransform>();
			labelRect.anchorMin = new Vector2(0, 0);
			labelRect.anchorMax = new Vector2(1, 1);
			labelRect.offsetMin = new Vector2(25, 0);
			labelRect.offsetMax = Vector2.zero;

			// Add layout element
			LayoutElement layout = toggleObj.AddComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = height;

			return toggle;
		}

		/// <summary>
		/// Create a button using Jötunn
		/// </summary>
		protected Button CreateJotunnButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick,
			float width = 150, float height = 40)
		{
			GameObject buttonObj = JotunnGUI.Instance.CreateButton(
				text: text,
				parent: parent,
				anchorMin: Vector2.zero,
				anchorMax: Vector2.one,
				position: Vector2.zero,
				width: width,
				height: height
			);

			Button button = buttonObj.GetComponent<Button>();
			if (onClick != null)
			{
				button.onClick.AddListener(onClick);
			}

			// Add layout element
			if (!buttonObj.GetComponent<LayoutElement>())
			{
				LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
				layout.preferredWidth = width;
				layout.preferredHeight = height;
			}

			return button;
		}

		/// <summary>
		/// Create a horizontal layout group
		/// </summary>
		protected GameObject CreateHorizontalGroup(Transform parent, float spacing = 10,
			float height = 30, TextAnchor alignment = TextAnchor.MiddleCenter)
		{
			GameObject group = new GameObject("HorizontalGroup");
			group.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = spacing;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.childAlignment = alignment;
			layout.childControlWidth = false;
			layout.childControlHeight = false;

			LayoutElement layoutElement = group.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = height;
			layoutElement.flexibleWidth = 1;

			return group;
		}

		/// <summary>
		/// Create a vertical layout group
		/// </summary>
		protected GameObject CreateVerticalGroup(Transform parent, float spacing = 10,
			TextAnchor alignment = TextAnchor.UpperCenter)
		{
			GameObject group = new GameObject("VerticalGroup");
			group.transform.SetParent(parent, false);

			VerticalLayoutGroup layout = group.AddComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			layout.childAlignment = alignment;
			layout.childControlHeight = false;

			return group;
		}

		/// <summary>
		/// Create a spacer
		/// </summary>
		protected void CreateSpacer(Transform parent, float height)
		{
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(parent, false);

			LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = height;
		}

		/// <summary>
		/// Create a scroll rect for item grid
		/// </summary>
		protected ScrollRect CreateJotunnScrollRect(Transform parent, float width, float height)
		{
			GameObject scrollObj = new GameObject("ScrollView");
			scrollObj.transform.SetParent(parent, false);

			RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
			scrollRect.sizeDelta = new Vector2(width, height);

			Image scrollBg = scrollObj.AddComponent<Image>();
			scrollBg.color = new Color(0, 0, 0, 0.3f);

			ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;

			// Create viewport
			GameObject viewport = new GameObject("Viewport");
			viewport.transform.SetParent(scrollObj.transform, false);

			RectTransform viewportRect = viewport.AddComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.sizeDelta = Vector2.zero;

			viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.1f);
			viewport.AddComponent<Mask>().showMaskGraphic = false;

			scroll.viewport = viewportRect;

			// Create content
			GameObject content = new GameObject("Content");
			content.transform.SetParent(viewport.transform, false);

			RectTransform contentRect = content.AddComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0, 1);
			contentRect.anchorMax = new Vector2(1, 1);
			contentRect.pivot = new Vector2(0.5f, 1);
			contentRect.sizeDelta = new Vector2(0, height);

			scroll.content = contentRect;

			// Add layout element
			LayoutElement layout = scrollObj.AddComponent<LayoutElement>();
			layout.preferredWidth = width;
			layout.preferredHeight = height;
			layout.flexibleHeight = 1;

			return scroll;
		}

		#endregion

		#region Item Management - Common Logic

		protected void LoadItemDatabase()
		{
			allItems.Clear();

			if (ObjectDB.instance == null)
			{
				Logger.LogWarning("[ItemConduit] ObjectDB not ready yet");
				return;
			}

			foreach (GameObject itemPrefab in ObjectDB.instance.m_items)
			{
				ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
				if (itemDrop != null && itemDrop.m_itemData != null)
				{
					allItems.Add(itemDrop.m_itemData);
				}
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Loaded {allItems.Count} items from database");
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