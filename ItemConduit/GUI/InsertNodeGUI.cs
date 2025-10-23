using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Nodes;
using ItemConduit.Config;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// GUI for configuring Insert Nodes - wireframe-based design
	/// Layout: Channel ID + Priority + Whitelist Toggle | Search Box | Category Sidebar + Item Grid
	/// </summary>
	public class InsertNodeGUI : BaseNodeGUI
	{
		#region Fields

		private InsertNode node;

		// UI Components
		private InputField channelInput;
		private InputField priorityInput;
		private Toggle whitelistToggle;
		private InputField searchInput;

		// Item grid
		private GameObject itemGridContainer;
		private List<ItemSlot> itemSlots = new List<ItemSlot>();

		// Category buttons
		private Dictionary<Category, Button> categoryButtons = new Dictionary<Category, Button>();

		#endregion

		#region Initialization

		public void Initialize(InsertNode insertNode)
		{
			node = insertNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadItemDatabase();
			LoadNodeSettings();
			SelectCategory(Category.All);
		}

		protected override Vector2 GetPanelSize()
		{
			// Match Valheim's crafting panel size
			return new Vector2(850, 600);
		}

		private void BuildUI()
		{
			// Main content container with vertical layout
			GameObject content = new GameObject("Content");
			content.transform.SetParent(panel.transform, false);

			VerticalLayoutGroup vertLayout = content.AddComponent<VerticalLayoutGroup>();
			vertLayout.padding = new RectOffset(10, 10, 10, 10);
			vertLayout.spacing = 8;
			vertLayout.childForceExpandWidth = true;
			vertLayout.childForceExpandHeight = false;

			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.sizeDelta = Vector2.zero;

			// Create UI sections
			CreateTitle(content.transform);
			CreateTopRow(content.transform);  // Channel ID + Priority + Whitelist Toggle
			CreateSearchBox(content.transform);
			CreateMainSection(content.transform);  // Category Sidebar + Item Grid

			// Apply Jotunn styling
			ApplyJotunnStyling(panel);
			GUIManager.Instance.ApplyWoodpanelStyle(panel.GetComponent<RectTransform>());
		}

		private void CreateTitle(Transform parent)
		{
			GameObject titleObj = new GameObject("Title");
			titleObj.transform.SetParent(parent, false);

			Text title = titleObj.AddComponent<Text>();
			title.text = "Insert Node Configuration";
			title.alignment = TextAnchor.MiddleCenter;
			title.name = "Title";
			title.fontSize = 20;

			LayoutElement layout = titleObj.AddComponent<LayoutElement>();
			layout.preferredHeight = 35;
		}

		private void CreateTopRow(Transform parent)
		{
			// Horizontal container for Channel ID, Priority, and Whitelist Toggle
			GameObject topRow = new GameObject("TopRow");
			topRow.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = topRow.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 10;
			hLayout.childForceExpandWidth = false;
			hLayout.childAlignment = TextAnchor.MiddleLeft;

			LayoutElement topRowLayout = topRow.AddComponent<LayoutElement>();
			topRowLayout.preferredHeight = 35;

			// Channel ID section
			GameObject channelLabel = new GameObject("ChannelLabel");
			channelLabel.transform.SetParent(topRow.transform, false);
			Text channelLabelText = channelLabel.AddComponent<Text>();
			channelLabelText.text = "Channel ID:";
			channelLabelText.alignment = TextAnchor.MiddleLeft;
			LayoutElement channelLabelLayout = channelLabel.AddComponent<LayoutElement>();
			channelLabelLayout.preferredWidth = 80;

			GameObject channelInputObj = CreateInputField(topRow.transform, "None", 120);
			channelInput = channelInputObj.GetComponent<InputField>();
			channelInput.onEndEdit.AddListener(OnChannelChanged);

			// Priority section
			GameObject priorityLabel = new GameObject("PriorityLabel");
			priorityLabel.transform.SetParent(topRow.transform, false);
			Text priorityLabelText = priorityLabel.AddComponent<Text>();
			priorityLabelText.text = "Priority:";
			priorityLabelText.alignment = TextAnchor.MiddleLeft;
			LayoutElement priorityLabelLayout = priorityLabel.AddComponent<LayoutElement>();
			priorityLabelLayout.preferredWidth = 60;

			GameObject priorityInputObj = CreateInputField(topRow.transform, "0", 60);
			priorityInput = priorityInputObj.GetComponent<InputField>();
			priorityInput.contentType = InputField.ContentType.IntegerNumber;
			priorityInput.onEndEdit.AddListener(OnPriorityChanged);

			// Spacer to push whitelist toggle to the right
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(topRow.transform, false);
			LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
			spacerLayout.flexibleWidth = 1;

			// Whitelist/Blacklist toggle
			GameObject whitelistLabel = new GameObject("WhitelistLabel");
			whitelistLabel.transform.SetParent(topRow.transform, false);
			Text whitelistLabelText = whitelistLabel.AddComponent<Text>();
			whitelistLabelText.text = "Whitelist/Blacklist:";
			whitelistLabelText.alignment = TextAnchor.MiddleRight;
			LayoutElement whitelistLabelLayout = whitelistLabel.AddComponent<LayoutElement>();
			whitelistLabelLayout.preferredWidth = 130;

			GameObject toggleObj = CreateToggle(topRow.transform, "Whitelist", true);
			whitelistToggle = toggleObj.GetComponent<Toggle>();
			whitelistToggle.onValueChanged.AddListener(OnWhitelistChanged);
		}

		private void CreateSearchBox(Transform parent)
		{
			// Search box container
			GameObject searchContainer = new GameObject("SearchContainer");
			searchContainer.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = searchContainer.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 8;
			hLayout.childForceExpandWidth = true;

			LayoutElement searchLayout = searchContainer.AddComponent<LayoutElement>();
			searchLayout.preferredHeight = 35;

			GameObject searchLabel = new GameObject("SearchLabel");
			searchLabel.transform.SetParent(searchContainer.transform, false);
			Text searchLabelText = searchLabel.AddComponent<Text>();
			searchLabelText.text = "Search:";
			searchLabelText.alignment = TextAnchor.MiddleLeft;
			LayoutElement searchLabelLayout = searchLabel.AddComponent<LayoutElement>();
			searchLabelLayout.preferredWidth = 60;

			GameObject searchInputObj = CreateInputField(searchContainer.transform, "Search items...", 0);
			searchInput = searchInputObj.GetComponent<InputField>();
			searchInput.onValueChanged.AddListener(OnSearchChanged);
			LayoutElement searchInputLayout = searchInputObj.GetComponent<LayoutElement>();
			searchInputLayout.flexibleWidth = 1;  // Take remaining space
		}

		private void CreateMainSection(Transform parent)
		{
			// Main section with horizontal layout: Category Sidebar | Item Grid
			GameObject mainSection = new GameObject("MainSection");
			mainSection.transform.SetParent(parent, false);

			RectTransform mainRect = mainSection.AddComponent<RectTransform>();
			mainRect.anchorMin = new Vector2(0, 0);
			mainRect.anchorMax = new Vector2(1, 1);
			mainRect.pivot = new Vector2(0.5f, 1);

			HorizontalLayoutGroup hLayout = mainSection.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 10;
			hLayout.childForceExpandWidth = false;
			hLayout.childForceExpandHeight = true;
			hLayout.childAlignment = TextAnchor.UpperLeft;
			hLayout.childControlWidth = true;
			hLayout.childControlHeight = true;

			LayoutElement mainLayout = mainSection.AddComponent<LayoutElement>();
			mainLayout.flexibleHeight = 1;
			mainLayout.flexibleWidth = 1;

			CreateCategorySidebar(mainSection.transform);
			CreateItemGrid(mainSection.transform);
		}

		private void CreateCategorySidebar(Transform parent)
		{
			// Left sidebar with category buttons
			GameObject sidebar = new GameObject("CategorySidebar");
			sidebar.transform.SetParent(parent, false);

			RectTransform sidebarRect = sidebar.AddComponent<RectTransform>();
			sidebarRect.anchorMin = new Vector2(0, 0);
			sidebarRect.anchorMax = new Vector2(0, 1);
			sidebarRect.pivot = new Vector2(0, 1);

			VerticalLayoutGroup vLayout = sidebar.AddComponent<VerticalLayoutGroup>();
			vLayout.spacing = 2;
			vLayout.childForceExpandWidth = true;
			vLayout.childForceExpandHeight = false;
			vLayout.childAlignment = TextAnchor.UpperLeft;

			LayoutElement sidebarLayout = sidebar.AddComponent<LayoutElement>();
			sidebarLayout.preferredWidth = 150;
			sidebarLayout.minWidth = 150;
			sidebarLayout.flexibleHeight = 1;

			// Add category buttons
			AddCategoryButton(sidebar.transform, Category.CurrentlyFiltered);
			AddCategoryButton(sidebar.transform, Category.All);
			AddCategoryButton(sidebar.transform, Category.Weapons);
			AddCategoryButton(sidebar.transform, Category.Armors);
			AddCategoryButton(sidebar.transform, Category.Foods);
			AddCategoryButton(sidebar.transform, Category.Materials);
			AddCategoryButton(sidebar.transform, Category.Consumables);
			AddCategoryButton(sidebar.transform, Category.Tools);
			AddCategoryButton(sidebar.transform, Category.Trophies);
			AddCategoryButton(sidebar.transform, Category.Misc);
		}

		private void AddCategoryButton(Transform parent, Category category)
		{
			string categoryName = GetCategoryDisplayName(category);
			Button button = CreateButton(parent, categoryName, 150, 30);

			LayoutElement layout = button.gameObject.GetComponent<LayoutElement>();
			layout.preferredWidth = 150;
			layout.preferredHeight = 30;

			button.onClick.AddListener(() => SelectCategory(category));
			categoryButtons[category] = button;
		}

		private void CreateItemGrid(Transform parent)
		{
			// Right side: scrollable item grid
			GameObject gridSection = new GameObject("GridSection");
			gridSection.transform.SetParent(parent, false);

			RectTransform gridSectionRect = gridSection.AddComponent<RectTransform>();
			gridSectionRect.anchorMin = new Vector2(0, 0);
			gridSectionRect.anchorMax = new Vector2(1, 1);
			gridSectionRect.pivot = new Vector2(0, 1);

			LayoutElement gridSectionLayout = gridSection.AddComponent<LayoutElement>();
			gridSectionLayout.flexibleWidth = 1;
			gridSectionLayout.flexibleHeight = 1;
			gridSectionLayout.minWidth = 400;

			// Scroll view
			GameObject scrollView = new GameObject("ScrollView");
			scrollView.transform.SetParent(gridSection.transform, false);

			RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
			scrollRect.anchorMin = Vector2.zero;
			scrollRect.anchorMax = Vector2.one;
			scrollRect.sizeDelta = Vector2.zero;

			Image scrollBg = scrollView.AddComponent<Image>();
			scrollBg.color = new Color(0, 0, 0, 0.3f);

			ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;

			// Grid container
			itemGridContainer = new GameObject("GridContainer");
			itemGridContainer.transform.SetParent(scrollView.transform, false);

			RectTransform gridRect = itemGridContainer.GetComponent<RectTransform>();
			if (gridRect == null)
				gridRect = itemGridContainer.AddComponent<RectTransform>();
			gridRect.anchorMin = new Vector2(0, 1);
			gridRect.anchorMax = new Vector2(1, 1);
			gridRect.pivot = new Vector2(0.5f, 1);

			GridLayoutGroup gridLayout = itemGridContainer.AddComponent<GridLayoutGroup>();
			gridLayout.cellSize = new Vector2(ITEM_SLOT_SIZE, ITEM_SLOT_SIZE);
			gridLayout.spacing = new Vector2(4, 4);
			gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			gridLayout.constraintCount = GRID_COLUMNS;
			gridLayout.childAlignment = TextAnchor.UpperLeft;
			gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
			gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
			gridLayout.padding = new RectOffset(5, 5, 5, 5);

			ContentSizeFitter fitter = itemGridContainer.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			scroll.content = gridRect;

			// Create item slots
			for (int i = 0; i < GRID_COLUMNS * GRID_ROWS; i++)
			{
				CreateItemSlot(itemGridContainer.transform, i);
			}
		}

		private void CreateItemSlot(Transform parent, int index)
		{
			GameObject slotObj = new GameObject($"ItemSlot_{index}");
			slotObj.transform.SetParent(parent, false);

			// Background
			Image bg = slotObj.AddComponent<Image>();
			bg.sprite = GUIManager.Instance.GetSprite("item_background");
			bg.type = Image.Type.Sliced;
			bg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

			// Highlight border (visible when item is filtered)
			GameObject borderObj = new GameObject("HighlightBorder");
			borderObj.transform.SetParent(slotObj.transform, false);
			RectTransform borderRect = borderObj.AddComponent<RectTransform>();
			borderRect.anchorMin = Vector2.zero;
			borderRect.anchorMax = Vector2.one;
			borderRect.sizeDelta = Vector2.zero;
			Image highlightBorder = borderObj.AddComponent<Image>();
			highlightBorder.color = new Color(1f, 0.8f, 0.2f, 1f);  // Gold/yellow highlight
			highlightBorder.enabled = false;

			// Icon
			GameObject iconObj = new GameObject("Icon");
			iconObj.transform.SetParent(slotObj.transform, false);
			RectTransform iconRect = iconObj.AddComponent<RectTransform>();
			iconRect.anchorMin = Vector2.zero;
			iconRect.anchorMax = Vector2.one;
			iconRect.sizeDelta = new Vector2(-8, -8);

			Image icon = iconObj.AddComponent<Image>();
			icon.preserveAspect = true;
			icon.enabled = false;

			// Button
			Button button = slotObj.AddComponent<Button>();
			int slotIndex = index;
			button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));

			// Item slot component
			ItemSlot slot = slotObj.AddComponent<ItemSlot>();
			slot.background = bg;
			slot.icon = icon;
			slot.button = button;
			slot.highlightBorder = highlightBorder;

			itemSlots.Add(slot);
		}

		#endregion

		#region Category Selection

		private void SelectCategory(Category category)
		{
			currentCategory = category;

			// Update button visual states
			foreach (var kvp in categoryButtons)
			{
				Image buttonImg = kvp.Value.GetComponent<Image>();
				if (kvp.Key == category)
				{
					buttonImg.color = new Color(0.5f, 0.4f, 0.3f);  // Highlighted
				}
				else
				{
					buttonImg.color = new Color(0.3f, 0.25f, 0.2f);  // Normal
				}
			}

			UpdateFilteredItems();
		}

		#endregion

		#region Item Management

		private void UpdateFilteredItems()
		{
			filteredItems.Clear();

			string searchText = searchInput != null ? searchInput.text.ToLower() : "";

			if (currentCategory == Category.CurrentlyFiltered)
			{
				// Show only items that are in the node's filter
				if (node != null && node.ItemFilter != null && node.ItemFilter.Count > 0)
				{
					foreach (var item in allItems)
					{
						string itemPrefabName = item.m_dropPrefab?.name ?? item.m_shared.m_name;

						if (node.ItemFilter.Any(f => f.Equals(itemPrefabName, System.StringComparison.OrdinalIgnoreCase)))
						{
							// Apply search filter if needed
							if (string.IsNullOrEmpty(searchText) ||
								item.m_shared.m_name.ToLower().Contains(searchText) ||
								itemPrefabName.ToLower().Contains(searchText))
							{
								filteredItems.Add(item);
							}
						}
					}
				}
			}
			else
			{
				// Show items by category
				foreach (var item in allItems)
				{
					if (!MatchesCategory(item, currentCategory))
						continue;

					if (!string.IsNullOrEmpty(searchText))
					{
						string itemName = item.m_shared.m_name.ToLower();
						string prefabName = (item.m_dropPrefab?.name ?? "").ToLower();

						if (!itemName.Contains(searchText) && !prefabName.Contains(searchText))
							continue;
					}

					filteredItems.Add(item);
				}
			}

			UpdateItemGrid();
		}

		private void UpdateItemGrid()
		{
			for (int i = 0; i < itemSlots.Count; i++)
			{
				if (i < filteredItems.Count)
				{
					ItemDrop.ItemData item = filteredItems[i];
					itemSlots[i].SetItem(item);

					// Highlight if this item is in the filter
					bool isInFilter = node.ItemFilter.Any(f =>
						f.Equals(item.m_dropPrefab?.name ?? item.m_shared.m_name, System.StringComparison.OrdinalIgnoreCase));
					itemSlots[i].SetHighlight(isInFilter);
				}
				else
				{
					itemSlots[i].Clear();
					itemSlots[i].SetHighlight(false);
				}
			}
		}

		#endregion

		#region Event Handlers

		private void OnChannelChanged(string newChannel)
		{
			if (node != null)
			{
				node.SetChannel(newChannel);
			}
		}

		private void OnPriorityChanged(string priorityText)
		{
			if (node != null && int.TryParse(priorityText, out int priority))
			{
				node.SetPriority(priority);
			}
		}

		private void OnWhitelistChanged(bool isWhitelist)
		{
			if (node != null)
			{
				node.SetWhitelist(isWhitelist);
			}
		}

		private void OnSearchChanged(string searchText)
		{
			UpdateFilteredItems();
		}

		private void OnItemSlotClicked(int slotIndex)
		{
			if (slotIndex >= filteredItems.Count) return;

			ItemDrop.ItemData item = filteredItems[slotIndex];
			string itemName = item.m_dropPrefab?.name ?? item.m_shared.m_name;

			if (node.ItemFilter.Contains(itemName))
			{
				// Remove from filter
				HashSet<string> newFilter = new HashSet<string>(node.ItemFilter);
				newFilter.Remove(itemName);
				node.SetFilter(newFilter);
			}
			else
			{
				// Add to filter
				HashSet<string> newFilter = new HashSet<string>(node.ItemFilter);
				newFilter.Add(itemName);
				node.SetFilter(newFilter);
			}

			UpdateItemGrid();
		}

		#endregion

		#region GUI Control

		private void LoadNodeSettings()
		{
			if (node == null) return;

			channelInput.text = node.ChannelId;
			priorityInput.text = node.Priority.ToString();
			whitelistToggle.isOn = node.IsWhitelist;
			UpdateItemGrid();
		}

		public override void Show()
		{
			base.Show();
			LoadNodeSettings();
		}

		#endregion

		#region ItemSlot Class

		private class ItemSlot : BaseItemSlot
		{
			// Inherits all functionality from BaseItemSlot
		}

		#endregion
	}
}