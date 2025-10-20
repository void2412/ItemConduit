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
	/// GUI for configuring Insert Nodes - inherits common UI logic from BaseNodeGUI
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
		private Text categoryFilterText; // Shows currently filtered items
		private Text categoryBrowserText; // Shows current category being browsed

		// Item grid
		private GameObject itemGridContainer;
		private GridLayoutGroup itemGrid;
		private List<ItemSlot> itemSlots = new List<ItemSlot>();

		// Category filtering
		private int currentCategoryIndex = 0;
		private readonly string[] categories = { "All", "Weapons", "Armors", "Foods", "Materials", "Consumables", "Tools", "Trophies", "Misc" };

		#endregion

		#region Initialization

		public void Initialize(InsertNode insertNode)
		{
			node = insertNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadItemDatabase();
			UpdateFilteredItems();
			LoadNodeSettings();
		}

		protected override Vector2 GetPanelSize()
		{
			return new Vector2(640, 750);
		}

		private void BuildUI()
		{
			GameObject content = new GameObject("Content");
			content.transform.SetParent(panel.transform, false);

			VerticalLayoutGroup vertLayout = content.AddComponent<VerticalLayoutGroup>();
			vertLayout.padding = new RectOffset(15, 15, 15, 15);
			vertLayout.spacing = 10;
			vertLayout.childForceExpandWidth = true;
			vertLayout.childForceExpandHeight = false;
			vertLayout.childControlHeight = true;

			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.sizeDelta = Vector2.zero;

			CreateTitle(content.transform);
			CreateSpacer(content.transform, 5);
			CreateTopSection(content.transform);
			CreateSpacer(content.transform, 10);
			CreateSearchSection(content.transform);
			CreateSpacer(content.transform, 10);
			CreateItemGrid(content.transform);
			CreateSpacer(content.transform, 10);
			CreateBottomButtons(content.transform);

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

			LayoutElement layoutElem = titleObj.AddComponent<LayoutElement>();
			layoutElem.preferredHeight = 40;
		}

		private void CreateTopSection(Transform parent)
		{
			// First row: Channel and Priority
			GameObject firstRow = CreateHorizontalGroup(parent);
			firstRow.GetComponent<LayoutElement>().preferredHeight = 50;
			firstRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

			// Channel
			GameObject channelLabelObj = new GameObject("ChannelLabel");
			channelLabelObj.transform.SetParent(firstRow.transform, false);
			Text channelLabel = channelLabelObj.AddComponent<Text>();
			channelLabel.text = "Channel ID:";
			channelLabel.alignment = TextAnchor.MiddleLeft;
			LayoutElement channelLabelElem = channelLabelObj.AddComponent<LayoutElement>();
			channelLabelElem.preferredWidth = 100;

			GameObject channelInputObj = CreateStandardInputField(firstRow.transform, "None");
			channelInput = channelInputObj.GetComponent<InputField>();
			channelInput.onEndEdit.AddListener(OnChannelChanged);
			LayoutElement channelInputElem = channelInputObj.GetComponent<LayoutElement>();
			channelInputElem.preferredWidth = 150;

			// Spacer
			GameObject spacer1 = new GameObject("Spacer");
			spacer1.transform.SetParent(firstRow.transform, false);
			LayoutElement spacerElem1 = spacer1.AddComponent<LayoutElement>();
			spacerElem1.preferredWidth = 20;

			// Priority
			GameObject priorityLabelObj = new GameObject("PriorityLabel");
			priorityLabelObj.transform.SetParent(firstRow.transform, false);
			Text priorityLabel = priorityLabelObj.AddComponent<Text>();
			priorityLabel.text = "Priority:";
			priorityLabel.alignment = TextAnchor.MiddleLeft;
			LayoutElement priorityLabelElem = priorityLabelObj.AddComponent<LayoutElement>();
			priorityLabelElem.preferredWidth = 70;

			GameObject priorityInputObj = CreateStandardInputField(firstRow.transform, "0");
			priorityInput = priorityInputObj.GetComponent<InputField>();
			priorityInput.contentType = InputField.ContentType.IntegerNumber;
			priorityInput.onEndEdit.AddListener(OnPriorityChanged);
			LayoutElement priorityInputElem = priorityInputObj.GetComponent<LayoutElement>();
			priorityInputElem.preferredWidth = 80;

			// Second row: Whitelist
			GameObject secondRow = CreateHorizontalGroup(parent);
			secondRow.GetComponent<LayoutElement>().preferredHeight = 40;
			secondRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

			GameObject toggleObj = CreateStandardToggle(secondRow.transform, "Whitelist", true);
			whitelistToggle = toggleObj.GetComponent<Toggle>();
			whitelistToggle.onValueChanged.AddListener(OnWhitelistChanged);
		}

		private void CreateSearchSection(Transform parent)
		{
			GameObject searchSection = new GameObject("SearchSection");
			searchSection.transform.SetParent(parent, false);

			VerticalLayoutGroup vLayout = searchSection.AddComponent<VerticalLayoutGroup>();
			vLayout.spacing = 5;
			vLayout.childForceExpandWidth = true;
			vLayout.childForceExpandHeight = false;

			LayoutElement searchElem = searchSection.AddComponent<LayoutElement>();
			searchElem.preferredHeight = 90;

			// Current filtered items display
			GameObject filteredRow = CreateHorizontalGroup(searchSection.transform);
			filteredRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
			filteredRow.GetComponent<LayoutElement>().preferredHeight = 25;

			GameObject filteredTextObj = new GameObject("FilteredText");
			filteredTextObj.transform.SetParent(filteredRow.transform, false);
			categoryFilterText = filteredTextObj.AddComponent<Text>();
			categoryFilterText.text = "Current Filtered: None";
			categoryFilterText.alignment = TextAnchor.MiddleCenter;
			categoryFilterText.fontSize = 14;
			LayoutElement filteredTextElem = filteredTextObj.AddComponent<LayoutElement>();
			filteredTextElem.flexibleWidth = 1;

			// Category browser
			GameObject categoryRow = CreateHorizontalGroup(searchSection.transform);
			categoryRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
			categoryRow.GetComponent<LayoutElement>().preferredHeight = 30;

			Button leftArrow = CreateStandardButton(categoryRow.transform, "<", 30);
			leftArrow.onClick.AddListener(OnPreviousCategory);

			GameObject categoryTextObj = new GameObject("CategoryText");
			categoryTextObj.transform.SetParent(categoryRow.transform, false);
			categoryBrowserText = categoryTextObj.AddComponent<Text>();
			categoryBrowserText.text = $"Browse: {categories[currentCategoryIndex]}";
			categoryBrowserText.alignment = TextAnchor.MiddleCenter;
			LayoutElement catTextElem = categoryTextObj.AddComponent<LayoutElement>();
			catTextElem.preferredWidth = 300;

			Button rightArrow = CreateStandardButton(categoryRow.transform, ">", 30);
			rightArrow.onClick.AddListener(OnNextCategory);

			// Search input
			GameObject searchInputObj = CreateStandardInputField(searchSection.transform, "Search items...");
			searchInput = searchInputObj.GetComponent<InputField>();
			searchInput.onValueChanged.AddListener(OnSearchChanged);
		}

		private void CreateItemGrid(Transform parent)
		{
			GameObject scrollContainer = new GameObject("ScrollContainer");
			scrollContainer.transform.SetParent(parent, false);

			LayoutElement scrollElem = scrollContainer.AddComponent<LayoutElement>();
			scrollElem.flexibleHeight = 1;
			scrollElem.preferredHeight = GRID_ROWS * ITEM_SLOT_SIZE + 20;

			ScrollRect scroll = scrollContainer.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;

			// Viewport
			GameObject viewport = new GameObject("Viewport");
			viewport.transform.SetParent(scrollContainer.transform, false);
			RectTransform viewportRect = viewport.AddComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.sizeDelta = Vector2.zero;
			viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
			viewport.AddComponent<Mask>().showMaskGraphic = true;

			scroll.viewport = viewportRect;

			// Content
			itemGridContainer = new GameObject("ItemGrid");
			itemGridContainer.transform.SetParent(viewport.transform, false);

			RectTransform gridRect = itemGridContainer.AddComponent<RectTransform>();
			gridRect.anchorMin = new Vector2(0, 1);
			gridRect.anchorMax = new Vector2(1, 1);
			gridRect.pivot = new Vector2(0.5f, 1);

			itemGrid = itemGridContainer.AddComponent<GridLayoutGroup>();
			itemGrid.cellSize = new Vector2(ITEM_SLOT_SIZE, ITEM_SLOT_SIZE);
			itemGrid.spacing = new Vector2(5, 5);
			itemGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			itemGrid.constraintCount = GRID_COLUMNS;
			itemGrid.childAlignment = TextAnchor.UpperCenter;
			itemGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
			itemGrid.startAxis = GridLayoutGroup.Axis.Horizontal;

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

			Image bg = slotObj.AddComponent<Image>();
			bg.sprite = GUIManager.Instance.GetSprite("item_background");
			bg.type = Image.Type.Sliced;

			GameObject iconObj = new GameObject("Icon");
			iconObj.transform.SetParent(slotObj.transform, false);
			RectTransform iconRect = iconObj.AddComponent<RectTransform>();
			iconRect.anchorMin = Vector2.zero;
			iconRect.anchorMax = Vector2.one;
			iconRect.sizeDelta = new Vector2(-10, -10);

			Image icon = iconObj.AddComponent<Image>();
			icon.preserveAspect = true;

			Button button = slotObj.AddComponent<Button>();
			int slotIndex = index;
			button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));

			ItemSlot slot = slotObj.AddComponent<ItemSlot>();
			slot.background = bg;
			slot.icon = icon;
			slot.button = button;

			itemSlots.Add(slot);
		}

		private void CreateBottomButtons(Transform parent)
		{
			GameObject buttonRow = CreateHorizontalGroup(parent);
			buttonRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
			buttonRow.GetComponent<LayoutElement>().preferredHeight = 50;

			Button closeBtn = CreateStandardButton(buttonRow.transform, "Close", 150);
			closeBtn.onClick.AddListener(Hide);

			Button clearBtn = CreateStandardButton(buttonRow.transform, "Clear Filter", 150);
			clearBtn.onClick.AddListener(OnClearFilter);
		}

		#endregion

		#region Item Management

		private void UpdateFilteredItems()
		{
			filteredItems.Clear();

			string searchText = searchInput != null ? searchInput.text.ToLower() : "";
			string category = categories[currentCategoryIndex];

			foreach (var item in allItems)
			{
				if (!MatchesCategory(item, category))
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

					bool isFiltered = node.ItemFilter.Any(f =>
						f.Equals(item.m_dropPrefab?.name ?? item.m_shared.m_name, System.StringComparison.OrdinalIgnoreCase));
					itemSlots[i].SetHighlight(isFiltered);
				}
				else
				{
					itemSlots[i].Clear();
				}
			}

			// Update the "Current Filtered" display
			UpdateCurrentFilteredDisplay();
		}

		private void UpdateCurrentFilteredDisplay()
		{
			if (categoryFilterText == null || node == null) return;

			if (node.ItemFilter.Count == 0)
			{
				categoryFilterText.text = "Current Filtered: None";
			}
			else if (node.ItemFilter.Count <= 3)
			{
				// Show item names if 3 or fewer
				string itemNames = string.Join(", ", node.ItemFilter.Take(3));
				categoryFilterText.text = $"Current Filtered: {itemNames}";
			}
			else
			{
				// Show count if more than 3
				categoryFilterText.text = $"Current Filtered: {node.ItemFilter.Count} items";
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
				node.SetFilter(node.ItemFilter, isWhitelist);
			}
		}

		private void OnSearchChanged(string searchText)
		{
			UpdateFilteredItems();
		}

		private void OnPreviousCategory()
		{
			currentCategoryIndex--;
			if (currentCategoryIndex < 0)
				currentCategoryIndex = categories.Length - 1;

			if (categoryBrowserText != null)
				categoryBrowserText.text = $"Browse: {categories[currentCategoryIndex]}";

			UpdateFilteredItems();
		}

		private void OnNextCategory()
		{
			currentCategoryIndex++;
			if (currentCategoryIndex >= categories.Length)
				currentCategoryIndex = 0;

			if (categoryBrowserText != null)
				categoryBrowserText.text = $"Browse: {categories[currentCategoryIndex]}";

			UpdateFilteredItems();
		}

		private void OnItemSlotClicked(int slotIndex)
		{
			if (slotIndex >= filteredItems.Count) return;

			ItemDrop.ItemData item = filteredItems[slotIndex];
			string itemName = item.m_dropPrefab?.name ?? item.m_shared.m_name;

			if (node.ItemFilter.Contains(itemName))
			{
				HashSet<string> newFilter = new HashSet<string>(node.ItemFilter);
				newFilter.Remove(itemName);
				node.SetFilter(newFilter, node.IsWhitelist);
			}
			else
			{
				HashSet<string> newFilter = new HashSet<string>(node.ItemFilter);
				newFilter.Add(itemName);
				node.SetFilter(newFilter, node.IsWhitelist);
			}

			UpdateItemGrid();
		}

		private void OnClearFilter()
		{
			if (node != null)
			{
				node.SetFilter(new HashSet<string>(), node.IsWhitelist);
				UpdateItemGrid();
			}
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

		#region ItemSlot Class - Inherits from BaseItemSlot

		private class ItemSlot : BaseItemSlot
		{
			// Inherits all functionality from BaseItemSlot
			// Can override methods here if needed for Insert-specific behavior
		}

		#endregion
	}
}