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
	/// GUI for configuring Extract Nodes - using Jötunn GUIManager
	/// </summary>
	public class ExtractNodeGUI : BaseNodeGUI
	{
		#region Fields

		private ExtractNode node;

		// UI Components
		private InputField channelInput;
		private Toggle whitelistToggle;
		private InputField searchInput;
		private Text categoryBrowserText;

		// Item grid
		private ScrollRect scrollRect;
		private GridLayoutGroup itemGrid;
		private List<ItemSlot> itemSlots = new List<ItemSlot>();

		// Category filtering
		private int currentCategoryIndex = 0;
		private readonly string[] categories = { "All", "Weapons", "Armors", "Foods", "Materials", "Consumables", "Tools", "Trophies", "Misc" };

		#endregion

		#region Initialization

		public void Initialize(ExtractNode extractNode)
		{
			node = extractNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadItemDatabase();
			UpdateFilteredItems();
			LoadNodeSettings();
		}

		protected override Vector2 GetPanelSize()
		{
			return new Vector2(640, 720);
		}

		private void BuildUI()
		{
			// Create main content container
			GameObject content = CreateVerticalGroup(panel.transform, spacing: 10);

			VerticalLayoutGroup vertLayout = content.GetComponent<VerticalLayoutGroup>();
			vertLayout.padding = new RectOffset(15, 15, 15, 15);
			vertLayout.childForceExpandWidth = true;

			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.sizeDelta = Vector2.zero;

			// Build UI sections
			CreateTitle(content.transform);
			CreateSpacer(content.transform, 5);
			CreateTopSection(content.transform);
			CreateSpacer(content.transform, 10);
			CreateSearchSection(content.transform);
			CreateSpacer(content.transform, 10);
			CreateItemGrid(content.transform);
			CreateSpacer(content.transform, 10);
			CreateBottomButtons(content.transform);
		}

		private void CreateTitle(Transform parent)
		{
			Text title = CreateJotunnText(
				parent: parent,
				text: "Extract Node Configuration",
				fontSize: 20,
				alignment: TextAnchor.MiddleCenter,
				bold: true,
				width: 600,
				height: 40
			);
			title.name = "Title";
		}

		private void CreateTopSection(Transform parent)
		{
			GameObject topSection = CreateHorizontalGroup(parent, spacing: 10, height: 50);

			// Channel label
			CreateJotunnText(
				parent: topSection.transform,
				text: "Channel ID:",
				fontSize: 16,
				alignment: TextAnchor.MiddleLeft,
				width: 100,
				height: 30
			);

			// Channel input
			channelInput = CreateJotunnInputField(
				parent: topSection.transform,
				placeholder: "None",
				width: 200,
				height: 30
			);
			channelInput.onEndEdit.AddListener(OnChannelChanged);

			// Spacer
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(topSection.transform, false);
			LayoutElement spacerElem = spacer.AddComponent<LayoutElement>();
			spacerElem.flexibleWidth = 1;

			// Whitelist toggle
			whitelistToggle = CreateJotunnToggle(
				parent: topSection.transform,
				label: "Whitelist",
				defaultValue: true,
				width: 120,
				height: 30
			);
			whitelistToggle.onValueChanged.AddListener(OnWhitelistChanged);
		}

		private void CreateSearchSection(Transform parent)
		{
			GameObject searchSection = CreateVerticalGroup(parent, spacing: 5);

			// Category browser row
			GameObject categoryRow = CreateHorizontalGroup(searchSection.transform, spacing: 5, height: 40);

			// Left arrow button
			Button leftArrow = CreateJotunnButton(
				parent: categoryRow.transform,
				text: "<",
				onClick: OnPreviousCategory,
				width: 40,
				height: 30
			);

			// Category text
			categoryBrowserText = CreateJotunnText(
				parent: categoryRow.transform,
				text: $"Browse: {categories[currentCategoryIndex]}",
				fontSize: 16,
				alignment: TextAnchor.MiddleCenter,
				width: 300,
				height: 30
			);

			// Right arrow button
			Button rightArrow = CreateJotunnButton(
				parent: categoryRow.transform,
				text: ">",
				onClick: OnNextCategory,
				width: 40,
				height: 30
			);

			// Search input
			searchInput = CreateJotunnInputField(
				parent: searchSection.transform,
				placeholder: "Search items...",
				width: 600,
				height: 30
			);
			searchInput.onValueChanged.AddListener(OnSearchChanged);
		}

		private void CreateItemGrid(Transform parent)
		{
			// Create scroll rect using Jötunn helper
			float gridWidth = GRID_COLUMNS * ITEM_SLOT_SIZE + (GRID_COLUMNS - 1) * 5 + 20;
			float gridHeight = GRID_ROWS * ITEM_SLOT_SIZE + 20;

			scrollRect = CreateJotunnScrollRect(parent, gridWidth, gridHeight);

			// Get the content from scroll rect
			GameObject itemGridContainer = scrollRect.content.gameObject;

			// Add grid layout
			itemGrid = itemGridContainer.AddComponent<GridLayoutGroup>();
			itemGrid.cellSize = new Vector2(ITEM_SLOT_SIZE, ITEM_SLOT_SIZE);
			itemGrid.spacing = new Vector2(5, 5);
			itemGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			itemGrid.constraintCount = GRID_COLUMNS;
			itemGrid.childAlignment = TextAnchor.UpperCenter;
			itemGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
			itemGrid.startAxis = GridLayoutGroup.Axis.Horizontal;

			// Add content size fitter
			ContentSizeFitter fitter = itemGridContainer.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

			// Icon
			GameObject iconObj = new GameObject("Icon");
			iconObj.transform.SetParent(slotObj.transform, false);

			RectTransform iconRect = iconObj.AddComponent<RectTransform>();
			iconRect.anchorMin = Vector2.zero;
			iconRect.anchorMax = Vector2.one;
			iconRect.sizeDelta = new Vector2(-10, -10);

			Image icon = iconObj.AddComponent<Image>();
			icon.preserveAspect = true;

			// Button
			Button button = slotObj.AddComponent<Button>();
			GUIManager.Instance.ApplyButtonStyle(button);

			int slotIndex = index;
			button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));

			// Item slot component
			ItemSlot slot = slotObj.AddComponent<ItemSlot>();
			slot.background = bg;
			slot.icon = icon;
			slot.button = button;

			itemSlots.Add(slot);
		}

		private void CreateBottomButtons(Transform parent)
		{
			GameObject buttonRow = CreateHorizontalGroup(parent, spacing: 10, height: 50, alignment: TextAnchor.MiddleCenter);

			// Close button
			CreateJotunnButton(
				parent: buttonRow.transform,
				text: "Close",
				onClick: Hide,
				width: 150,
				height: 40
			);

			// Clear filter button
			CreateJotunnButton(
				parent: buttonRow.transform,
				text: "Clear Filter",
				onClick: OnClearFilter,
				width: 150,
				height: 40
			);
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

			UpdateItemSlots();
		}

		private bool MatchesCategory(ItemDrop.ItemData item, string category)
		{
			if (category == "All") return true;

			return category switch
			{
				"Weapons" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
							 item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
							 item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow ||
							 item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
				"Armors" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet ||
							item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Chest ||
							item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Legs ||
							item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder,
				"Foods" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable &&
						   item.m_shared.m_food > 0,
				"Materials" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material,
				"Consumables" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable,
				"Tools" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool,
				"Trophies" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy,
				"Misc" => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Misc,
				_ => false
			};
		}

		private void UpdateItemSlots()
		{
			for (int i = 0; i < itemSlots.Count; i++)
			{
				if (i < filteredItems.Count)
				{
					itemSlots[i].SetItem(filteredItems[i]);
					itemSlots[i].SetHighlight(IsItemInFilter(filteredItems[i]));
				}
				else
				{
					itemSlots[i].Clear();
				}
			}
		}

		private bool IsItemInFilter(ItemDrop.ItemData item)
		{
			if (node == null) return false;
			return node.ItemFilter.Contains(item.m_dropPrefab?.name ?? "");
		}

		#endregion

		#region Event Handlers

		private void OnChannelChanged(string channelId)
		{
			if (node != null)
			{
				node.SetChannel(channelId);
			}
		}

		private void OnWhitelistChanged(bool isWhitelist)
		{
			if (node != null)
			{
				node.SetWhitelist(isWhitelist);
				UpdateItemSlots();
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

			categoryBrowserText.text = $"Browse: {categories[currentCategoryIndex]}";
			UpdateFilteredItems();
		}

		private void OnNextCategory()
		{
			currentCategoryIndex++;
			if (currentCategoryIndex >= categories.Length)
				currentCategoryIndex = 0;

			categoryBrowserText.text = $"Browse: {categories[currentCategoryIndex]}";
			UpdateFilteredItems();
		}

		private void OnItemSlotClicked(int slotIndex)
		{
			if (slotIndex >= filteredItems.Count) return;

			ItemDrop.ItemData item = filteredItems[slotIndex];
			if (item == null) return;

			string itemName = item.m_dropPrefab?.name ?? "";
			if (string.IsNullOrEmpty(itemName)) return;

			if (node != null)
			{
				if (node.ItemFilter.Contains(itemName))
				{
					node.ItemFilter.Remove(itemName);
				}
				else
				{
					node.ItemFilter.Add(itemName);
				}

				node.SetFilter(node.ItemFilter);
				UpdateItemSlots();
			}
		}

		private void OnClearFilter()
		{
			if (node != null)
			{
				node.ItemFilter.Clear();
				node.SetFilter(node.ItemFilter);
				UpdateItemSlots();
			}
		}

		#endregion

		#region Settings

		private void LoadNodeSettings()
		{
			if (node == null) return;

			if (channelInput != null)
			{
				channelInput.text = node.ChannelId ?? "";
			}

			if (whitelistToggle != null)
			{
				whitelistToggle.isOn = node.IsWhitelist;
			}

			UpdateItemSlots();
		}

		#endregion

		#region Item Slot Class

		protected class ItemSlot : BaseItemSlot
		{
			// Inherits from BaseItemSlot in BaseNodeGUI
		}

		#endregion
	}
}