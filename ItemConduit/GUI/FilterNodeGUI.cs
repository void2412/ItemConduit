using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;


namespace ItemConduit.GUI
{
	public abstract class FilterNodeGUI<TNode> : BaseNodeGUI where TNode : BaseNode, IFilterNode
	{
		protected TNode node;

		protected Humanoid user;
		protected InputField channelInput;
		protected Button modeButton;
		protected InputField searchInput;

		private GameObject itemGridContainer;
		private readonly List<ItemSlot> itemSlots = new List<ItemSlot>();
		private readonly Dictionary<Category, Button> categoryButtons = new Dictionary<Category, Button>();
		private Transform tooltipParent;


		

		public void Initialize(TNode targetNode, Humanoid player)
		{
			node = targetNode;
			user = player;
			InitializeBaseNodeUI();
			BuildUI();
			LoadItemDatabase();
			LoadNodeSettings();
			UpdateFilteredCountDisplay();
			SelectCategory(Category.All);
		}

		protected override Vector2 GetPanelSize() => new Vector2(1100, 850);

		private void BuildUI()
		{
			tooltipParent = panel.transform;
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

			CreateTitle(content.transform);
			CreateTopRow(content.transform);
			CreateSearchBox(content.transform);
			CreateMainSection(content.transform);
			CreateFooter(content.transform);

			ApplyJotunnStyling(panel);
			GUIManager.Instance.ApplyWoodpanelStyle(panel.GetComponent<RectTransform>());
		}

		private void CreateTitle(Transform parent)
		{
			Text title = CreateLabel(parent, "Title", GetTitleText(), 0, TextAnchor.MiddleCenter);
			title.fontSize = 20;
			title.GetComponent<LayoutElement>().preferredHeight = 35;
		}

		private void CreateTopRow(Transform parent)
		{
			GameObject topRow = new GameObject("TopRow");
			topRow.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = topRow.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 10;
			hLayout.childForceExpandWidth = false;
			hLayout.childForceExpandHeight = false;
			hLayout.childControlHeight = false;
			hLayout.childControlWidth = false;
			hLayout.childAlignment = TextAnchor.MiddleCenter;

			LayoutElement topRowLayout = topRow.AddComponent<LayoutElement>();
			topRowLayout.preferredHeight = 56; // DOUBLED from 28
			topRowLayout.minHeight = 56;

			CreateLabel(topRow.transform, "ChannelLabel", "Channel:", 120f); // DOUBLED width

			GameObject channelInputObj = CreateInputField(topRow.transform, GetChannelPlaceholder(), 240f); // DOUBLED width
			channelInput = channelInputObj.GetComponent<InputField>();
			channelInput.onEndEdit.AddListener(OnChannelChanged);

			AddTopRowContent(topRow.transform);

			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(topRow.transform, false);
			LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
			spacerLayout.flexibleWidth = 1;

			CreateLabel(topRow.transform, "ModeLabel", "Filter Mode:", 160f, TextAnchor.MiddleRight); // DOUBLED width

			// Create mode toggle button
			modeButton = CreateButton(topRow.transform, "Whitelist", 200f, 48f); // DOUBLED both dimensions
			modeButton.onClick.AddListener(OnModeButtonClicked);
		}

		private void CreateSearchBox(Transform parent)
		{
			GameObject searchContainer = new GameObject("SearchContainer");
			searchContainer.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = searchContainer.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 8;
			hLayout.childForceExpandWidth = false;
			hLayout.childForceExpandHeight = false;
			hLayout.childControlHeight = false;
			hLayout.childControlWidth = true;
			hLayout.childAlignment = TextAnchor.MiddleCenter;

			LayoutElement searchLayout = searchContainer.AddComponent<LayoutElement>();
			searchLayout.preferredHeight = 56; // DOUBLED from 28
			searchLayout.minHeight = 56;
			searchLayout.flexibleWidth = 1;

			Text label = CreateLabel(searchContainer.transform, "SearchLabel", "Search:", 110f); // DOUBLED width
			LayoutElement labelLayout = label.GetComponent<LayoutElement>();
			labelLayout.flexibleWidth = 0;

			GameObject searchInputObj = CreateInputField(searchContainer.transform, "Search items...", 200f); // DOUBLED minimum width
			searchInput = searchInputObj.GetComponent<InputField>();
			searchInput.onValueChanged.AddListener(OnSearchChanged);

			LayoutElement inputLayout = searchInputObj.GetComponent<LayoutElement>();
			inputLayout.flexibleWidth = 1;
			inputLayout.minWidth = 200f;
			inputLayout.preferredHeight = 48; // DOUBLED from 24
			inputLayout.minHeight = 48;
			inputLayout.flexibleHeight = 0;
		}

		private void CreateMainSection(Transform parent)
		{
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

		private void CreateFooter(Transform parent)
		{
			GameObject footer = new GameObject("Footer");
			footer.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = footer.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 12; // Reduced spacing to fit 4 buttons
			hLayout.childForceExpandWidth = true;
			hLayout.childForceExpandHeight = false;
			hLayout.childControlHeight = false;
			hLayout.childControlWidth = false;
			hLayout.childAlignment = TextAnchor.MiddleCenter;
			hLayout.padding = new RectOffset(10, 10, 10, 10);

			LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
			footerLayout.preferredHeight = 70;
			footerLayout.minHeight = 70;

			// Copy button
			Button copyButton = CreateButton(footer.transform, "Copy Settings", 240f, 50f); // Reduced width
			copyButton.onClick.AddListener(OnCopySettings);

			// Paste button
			Button pasteButton = CreateButton(footer.transform, "Paste Settings", 240f, 50f);
			pasteButton.onClick.AddListener(OnPasteSettings);

			// Clear Clipboard button
			Button clearClipboardButton = CreateButton(footer.transform, "Clear Clipboard", 240f, 50f);
			clearClipboardButton.onClick.AddListener(OnClearClipboard);

			// Style clear clipboard button (gray tint)
			Image clearClipboardImg = clearClipboardButton.GetComponent<Image>();
			if (clearClipboardImg != null)
			{
				ColorBlock colors = clearClipboardButton.colors;
				colors.normalColor = new Color(0.25f, 0.25f, 0.25f);
				colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
				colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
				clearClipboardButton.colors = colors;
			}

			// Clear Filter button
			Button clearFilterButton = CreateButton(footer.transform, "Clear Filter", 240f, 50f);
			clearFilterButton.onClick.AddListener(OnClearFilter);

			// Style the clear filter button (red tint)
			Image clearFilterImg = clearFilterButton.GetComponent<Image>();
			if (clearFilterImg != null)
			{
				ColorBlock colors = clearFilterButton.colors;
				colors.normalColor = new Color(0.5f, 0.2f, 0.2f);
				colors.highlightedColor = new Color(0.6f, 0.3f, 0.3f);
				colors.pressedColor = new Color(0.4f, 0.1f, 0.1f);
				clearFilterButton.colors = colors;
			}
		}

		private void CreateCategorySidebar(Transform parent)
		{
			categoryButtons.Clear();

			GameObject sidebar = new GameObject("CategorySidebar");
			sidebar.transform.SetParent(parent, false);

			RectTransform sidebarRect = sidebar.AddComponent<RectTransform>();
			sidebarRect.anchorMin = new Vector2(0, 0);
			sidebarRect.anchorMax = new Vector2(0, 1);
			sidebarRect.pivot = new Vector2(0, 1);

			VerticalLayoutGroup vLayout = sidebar.AddComponent<VerticalLayoutGroup>();
			vLayout.spacing = 3;
			vLayout.childForceExpandWidth = true;
			vLayout.childForceExpandHeight = false;
			vLayout.childAlignment = TextAnchor.UpperLeft;

			LayoutElement sidebarLayout = sidebar.AddComponent<LayoutElement>();
			sidebarLayout.preferredWidth = 100;
			sidebarLayout.minWidth = 100;
			sidebarLayout.flexibleHeight = 1;

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
			Button button = CreateButton(parent, GetCategoryDisplayName(category), 150, 30);
			LayoutElement layout = button.gameObject.GetComponent<LayoutElement>();
			layout.preferredWidth = 150;
			layout.preferredHeight = 45;
			layout.minHeight = 45;

			Text buttonText = button.GetComponentInChildren<Text>();
			if (buttonText != null)
			{
				buttonText.fontSize = 42;
			}

			button.onClick.AddListener(() => SelectCategory(category));
			categoryButtons[category] = button;
		}

		private void SelectCategory(Category category)
		{
			currentCategory = category;

			foreach (var kvp in categoryButtons)
			{
				Image buttonImg = kvp.Value.GetComponent<Image>();
				buttonImg.color = kvp.Key == category
					? new Color(0.5f, 0.4f, 0.3f)
					: new Color(0.3f, 0.25f, 0.2f);
			}

			UpdateFilteredItems();
		}

		private void CreateItemGrid(Transform parent)
		{
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

			// Create scroll view with viewport
			GameObject scrollView = new GameObject("ScrollView");
			scrollView.transform.SetParent(gridSection.transform, false);

			RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
			scrollRect.anchorMin = Vector2.zero;
			scrollRect.anchorMax = Vector2.one;
			scrollRect.sizeDelta = Vector2.zero;

			Image scrollBg = scrollView.AddComponent<Image>();
			scrollBg.color = new Color(0, 0, 0, 0.3f);

			// ADD MASK - This clips content outside viewport
			Mask mask = scrollView.AddComponent<Mask>();
			mask.showMaskGraphic = true;

			ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 1000f;
			scroll.inertia = true;
			scroll.decelerationRate = 0.135f;
			scroll.viewport = scrollRect; // Set viewport for clipping

			// Create scrollbar
			GameObject scrollbarObj = new GameObject("Scrollbar");
			scrollbarObj.transform.SetParent(gridSection.transform, false);

			RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
			scrollbarRect.anchorMin = new Vector2(1, 0);
			scrollbarRect.anchorMax = new Vector2(1, 1);
			scrollbarRect.pivot = new Vector2(1, 1);
			scrollbarRect.sizeDelta = new Vector2(20, 0);
			scrollbarRect.anchoredPosition = new Vector2(-5, 0);

			Image scrollbarBg = scrollbarObj.AddComponent<Image>();
			scrollbarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

			Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
			scrollbar.direction = Scrollbar.Direction.BottomToTop;

			// Scrollbar handle
			GameObject handleObj = new GameObject("Handle");
			handleObj.transform.SetParent(scrollbarObj.transform, false);

			RectTransform handleRect = handleObj.AddComponent<RectTransform>();
			handleRect.anchorMin = Vector2.zero;
			handleRect.anchorMax = Vector2.one;
			handleRect.sizeDelta = Vector2.zero;

			Image handleImg = handleObj.AddComponent<Image>();
			handleImg.color = new Color(0.5f, 0.4f, 0.3f, 1f);

			scrollbar.handleRect = handleRect;
			scrollbar.targetGraphic = handleImg;

			scroll.verticalScrollbar = scrollbar;
			scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

			// Create content container
			itemGridContainer = new GameObject("GridContainer");
			itemGridContainer.transform.SetParent(scrollView.transform, false);

			RectTransform gridRect = itemGridContainer.AddComponent<RectTransform>();
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
			gridLayout.padding = new RectOffset(100, 5, 5, 5); // CHANGED: left=10 (was 5)

			ContentSizeFitter fitter = itemGridContainer.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			scroll.content = gridRect;
		}

		private void CreateItemSlot(Transform parent, int index)
		{
			GameObject slotObj = new GameObject($"ItemSlot_{index}");
			slotObj.transform.SetParent(parent, false);

			Image bg = slotObj.AddComponent<Image>();
			bg.sprite = GUIManager.Instance.GetSprite("item_background");
			bg.type = Image.Type.Sliced;
			bg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

			GameObject borderObj = new GameObject("HighlightBorder");
			borderObj.transform.SetParent(slotObj.transform, false);
			RectTransform borderRect = borderObj.AddComponent<RectTransform>();
			borderRect.anchorMin = Vector2.zero;
			borderRect.anchorMax = Vector2.one;
			borderRect.sizeDelta = Vector2.zero;
			Image highlightBorder = borderObj.AddComponent<Image>();
			highlightBorder.color = new Color(1f, 0.8f, 0.2f, 1f);
			highlightBorder.enabled = false;

			GameObject iconObj = new GameObject("Icon");
			iconObj.transform.SetParent(slotObj.transform, false);
			RectTransform iconRect = iconObj.AddComponent<RectTransform>();
			iconRect.anchorMin = Vector2.zero;
			iconRect.anchorMax = Vector2.one;
			iconRect.sizeDelta = new Vector2(-8, -8);

			Image icon = iconObj.AddComponent<Image>();
			icon.preserveAspect = true;
			icon.enabled = false;

			Button button = slotObj.AddComponent<Button>();
			int slotIndex = index;
			button.onClick.AddListener(() => OnItemSlotClicked(slotIndex));
			
			HoverUI hover = slotObj.AddComponent<HoverUI>();
			

			ItemSlot slot = slotObj.AddComponent<ItemSlot>();
			slot.background = bg;
			slot.icon = icon;
			slot.button = button;
			slot.highlightBorder = highlightBorder;

			itemSlots.Add(slot);
		}

		private void UpdateFilteredItems()
		{
			filteredItems.Clear();

			string searchText = searchInput != null ? searchInput.text.ToLower() : string.Empty;

			if (currentCategory == Category.CurrentlyFiltered)
			{
				if (node != null && node.ItemFilter != null && node.ItemFilter.Count > 0)
				{
					foreach (var item in allItems)
					{
						string itemPrefabName = item.m_dropPrefab?.name ?? item.m_shared.m_name;

						if (node.ItemFilter.Any(f => f.Equals(itemPrefabName, System.StringComparison.OrdinalIgnoreCase)))
						{
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
				foreach (var item in allItems)
				{
					if (!MatchesCategory(item, currentCategory))
					{
						continue;
					}

					if (!string.IsNullOrEmpty(searchText))
					{
						string itemName = item.m_shared.m_name.ToLower();
						string prefabName = (item.m_dropPrefab?.name ?? string.Empty).ToLower();

						if (!itemName.Contains(searchText) && !prefabName.Contains(searchText))
						{
							continue;
						}
					}

					filteredItems.Add(item);
				}
			}

			UpdateItemGrid();
		}

		private void UpdateItemGrid()
		{
			int requiredSlots = filteredItems.Count;

			// Create slots as needed
			while (itemSlots.Count < requiredSlots)
			{
				CreateItemSlot(itemGridContainer.transform, itemSlots.Count);
			}

			// Update all slots (they're all active, mask handles visibility)
			for (int i = 0; i < itemSlots.Count; i++)
			{
				if (i < requiredSlots)
				{
					// Update slot with item data
					itemSlots[i].gameObject.SetActive(true);
					ItemDrop.ItemData item = filteredItems[i];
					itemSlots[i].SetItem(item);

					bool isInFilter = node.ItemFilter.Any(f =>
						f.Equals(item.m_dropPrefab?.name ?? item.m_shared.m_name, System.StringComparison.OrdinalIgnoreCase));
					itemSlots[i].SetHighlight(isInFilter);
					HoverUI hover = itemSlots[i].GetComponent<HoverUI>();
					if (hover != null)
					{
						hover.itemTooltip.Create(tooltipParent);
						hover.itemTooltip.itemData = item;
					}
				}
				else
				{
					// Hide extra slots
					itemSlots[i].gameObject.SetActive(false);
				}
			}

			// Force layout rebuild
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(itemGridContainer.GetComponent<RectTransform>());
		}

		private void UpdateFilteredCountDisplay()
		{
			if (categoryButtons.ContainsKey(Category.CurrentlyFiltered))
			{
				Button filteredButton = categoryButtons[Category.CurrentlyFiltered];
				Text buttonText = filteredButton.GetComponentInChildren<Text>();

				if (buttonText != null)
				{
					int filterCount = node?.ItemFilter?.Count ?? 0;
					buttonText.text = $"Currently Filtered ({filterCount})";
				}
			}
		}
		private void OnChannelChanged(string newChannel)
		{
			if (node != null)
			{
				node.SetChannel(newChannel);
			}
		}


		private void OnSearchChanged(string _)
		{
			UpdateFilteredItems();
		}

		private void OnItemSlotClicked(int slotIndex)
		{
			if (slotIndex >= filteredItems.Count)
			{
				return;
			}

			ItemDrop.ItemData item = filteredItems[slotIndex];
			string itemName = item.m_dropPrefab?.name ?? item.m_shared.m_name;

			HashSet<string> newFilter = node.ItemFilter != null
				? new HashSet<string>(node.ItemFilter)
				: new HashSet<string>();

			if (newFilter.Contains(itemName))
			{
				newFilter.Remove(itemName);
			}
			else
			{
				newFilter.Add(itemName);
			}

			node.SetFilter(newFilter);
			UpdateItemGrid();
			UpdateFilteredCountDisplay();
		}

		private void OnModeButtonClicked()
		{
			if (node != null)
			{
				bool newMode = !node.IsWhitelist;
				node.SetWhitelist(newMode);

				// Update button text and color
				Text buttonText = modeButton.GetComponentInChildren<Text>();
				if (buttonText != null)
				{
					buttonText.text = newMode ? "Whitelist" : "Blacklist";
				}

				// Optional: Change button color based on mode
				Image buttonImg = modeButton.GetComponent<Image>();
				if (buttonImg != null)
				{
					buttonImg.color = newMode
						? new Color(0.3f, 0.5f, 0.3f) // Green tint for whitelist
						: new Color(0.5f, 0.3f, 0.3f); // Red tint for blacklist
				}
			}
		}


		private void OnCopySettings()
		{
			if (node == null) return;

			// Copy common settings to clipboard
			Clipboard.ChannelId = node.ChannelId;
			Clipboard.ItemFilter = new HashSet<string>(node.ItemFilter);
			Clipboard.IsWhitelist = node.IsWhitelist;

			// Copy priority if this is an InsertNode
			if (node is InsertNode insertNode)
			{
				Clipboard.Priority = insertNode.Priority;
			}
			else
			{
				Clipboard.Priority = null;
			}

			Clipboard.HasData = true;

			user.Message(MessageHud.MessageType.Center, "Setting Copied to Clipboard", 0, null);

			if (DebugConfig.showDebug.Value)
			{
				string priorityInfo = Clipboard.Priority.HasValue ? $", Priority={Clipboard.Priority.Value}" : "";
				Logger.LogInfo($"[ItemConduit] Copied settings: Channel={Clipboard.ChannelId}, Filter={Clipboard.ItemFilter.Count} items, Mode={Clipboard.IsWhitelist}{priorityInfo}");
			}
		}

		private void OnPasteSettings()
		{
			if (node == null) return;

			if (!Clipboard.HasData)
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogWarning("[ItemConduit] No settings to paste - use Copy first");
				}
				user.Message(MessageHud.MessageType.Center, "No Data in Clipboard", 0, null);
				return;
			}

			// Paste common settings from clipboard
			node.SetChannel(Clipboard.ChannelId);
			node.SetFilter(new HashSet<string>(Clipboard.ItemFilter));
			node.SetWhitelist(Clipboard.IsWhitelist);

			// Paste priority only if clipboard has it AND current node is InsertNode
			if (Clipboard.Priority.HasValue && node is InsertNode insertNode)
			{
				insertNode.SetPriority(Clipboard.Priority.Value);
			}

			// Update UI to reflect pasted settings
			LoadNodeSettings();

			user.Message(MessageHud.MessageType.Center, "Settings Pasted from Clipboard", 0, null);

			if (DebugConfig.showDebug.Value)
			{
				string priorityInfo = Clipboard.Priority.HasValue ? $", Priority={Clipboard.Priority.Value}" : "";
				Logger.LogInfo($"[ItemConduit] Pasted settings: Channel={Clipboard.ChannelId}, Filter={Clipboard.ItemFilter.Count} items, Mode={Clipboard.IsWhitelist}{priorityInfo}");
			}
		}

		private void OnClearFilter()
		{
			if (node == null) return;

			// Clear all filter settings
			node.SetFilter(new HashSet<string>());

			// Update UI
			UpdateFilteredCountDisplay();
			UpdateItemGrid();

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo("[ItemConduit] Cleared all filters");
			}
		}

		private void OnClearClipboard()
		{
			Clipboard.Clear();

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo("[ItemConduit] Cleared clipboard");
			}
		}

		protected virtual void LoadNodeSettings()
		{
			if (node == null)
			{
				return;
			}

			if (channelInput != null)
			{
				channelInput.SetTextWithoutNotify(node.ChannelId);
			}

			// Update mode button instead of toggle
			if (modeButton != null)
			{
				Text buttonText = modeButton.GetComponentInChildren<Text>();
				if (buttonText != null)
				{
					buttonText.text = node.IsWhitelist ? "Whitelist" : "Blacklist";
				}

				Image buttonImg = modeButton.GetComponent<Image>();
				if (buttonImg != null)
				{
					buttonImg.color = node.IsWhitelist
						? new Color(0.3f, 0.5f, 0.3f)
						: new Color(0.5f, 0.3f, 0.3f);
				}
			}

			OnAfterLoadNodeSettings();
			UpdateFilteredCountDisplay();
			UpdateItemGrid();
		}

		protected virtual void OnAfterLoadNodeSettings()
		{
		}

		public override void Show()
		{
			base.Show();
			LoadNodeSettings();
		}

		

		protected virtual float GetTopRowSpacing() => 10f;
		protected virtual float GetChannelLabelWidth() => 60f;
		protected virtual string GetChannelLabelText() => "Channel ID:";
		protected virtual string GetChannelPlaceholder() => "None";
		protected virtual float GetChannelInputWidth() => 120f;
		protected virtual string GetWhitelistLabelText() => "Whitelist/Blacklist:";
		protected virtual float GetWhitelistLabelWidth() => 80f;
		protected virtual string GetWhitelistToggleText() => "Whitelist";

		protected string GetCategoryDisplayName(Category category)
		{
			switch (category)
			{
				case Category.CurrentlyFiltered:
					int filterCount = node?.ItemFilter?.Count ?? 0;
					return $"Currently Filtered ({filterCount})";
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
		protected abstract string GetTitleText();
		protected virtual void AddTopRowContent(Transform topRow) { }


	}

	public class ItemSlot : BaseItemSlot
	{

	}
	public static class Clipboard
	{
		public static string ChannelId { get; set; }
		public static HashSet<string> ItemFilter { get; set; }
		public static int? Priority { get; set; }
		public static bool IsWhitelist { get; set; }
		public static bool HasData { get; set; }

		public static void Clear()
		{
			ChannelId = null;
			ItemFilter = null;
			IsWhitelist = true;
			Priority = null;
			HasData = false;
		}
	}


}