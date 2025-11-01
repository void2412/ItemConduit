using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ItemConduit.GUI
{
	public abstract class FilterNodeGUI<TNode> : BaseNodeGUI where TNode : BaseNode, IFilterNode
	{
		protected TNode node;

		protected InputField channelInput;
		protected Toggle whitelistToggle;
		protected InputField searchInput;

		private GameObject itemGridContainer;
		private readonly List<FilterItemSlot> itemSlots = new List<FilterItemSlot>();
		private readonly Dictionary<Category, Button> categoryButtons = new Dictionary<Category, Button>();

		public void Initialize(TNode targetNode)
		{
			node = targetNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadItemDatabase();
			LoadNodeSettings();
			SelectCategory(Category.All);
		}

		protected override Vector2 GetPanelSize() => new Vector2(850, 600);

		private void BuildUI()
		{
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
			hLayout.spacing = GetTopRowSpacing();
			hLayout.childForceExpandWidth = false;
			hLayout.childAlignment = TextAnchor.MiddleLeft;

			LayoutElement topRowLayout = topRow.AddComponent<LayoutElement>();
			topRowLayout.preferredHeight = 35;

			CreateLabel(topRow.transform, "ChannelLabel", GetChannelLabelText(), GetChannelLabelWidth());

			GameObject channelInputObj = CreateInputField(topRow.transform, GetChannelPlaceholder(), GetChannelInputWidth());
			channelInput = channelInputObj.GetComponent<InputField>();
			channelInput.onEndEdit.AddListener(OnChannelChanged);

			AddTopRowContent(topRow.transform);

			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(topRow.transform, false);
			LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
			spacerLayout.flexibleWidth = 1;

			CreateLabel(topRow.transform, "WhitelistLabel", GetWhitelistLabelText(), GetWhitelistLabelWidth(), TextAnchor.MiddleRight);

			GameObject toggleObj = CreateToggle(topRow.transform, GetWhitelistToggleText(), true);
			whitelistToggle = toggleObj.GetComponent<Toggle>();
			whitelistToggle.onValueChanged.AddListener(OnWhitelistChanged);
		}

		private void CreateSearchBox(Transform parent)
		{
			GameObject searchContainer = new GameObject("SearchContainer");
			searchContainer.transform.SetParent(parent, false);

			HorizontalLayoutGroup hLayout = searchContainer.AddComponent<HorizontalLayoutGroup>();
			hLayout.spacing = 8;
			hLayout.childForceExpandWidth = true;

			LayoutElement searchLayout = searchContainer.AddComponent<LayoutElement>();
			searchLayout.preferredHeight = 35;

			CreateLabel(searchContainer.transform, "SearchLabel", "Search:", 60f);

			GameObject searchInputObj = CreateInputField(searchContainer.transform, "Search items...", 0);
			searchInput = searchInputObj.GetComponent<InputField>();
			searchInput.onValueChanged.AddListener(OnSearchChanged);
			searchInputObj.GetComponent<LayoutElement>().flexibleWidth = 1;
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
			vLayout.spacing = 2;
			vLayout.childForceExpandWidth = true;
			vLayout.childForceExpandHeight = false;
			vLayout.childAlignment = TextAnchor.UpperLeft;

			LayoutElement sidebarLayout = sidebar.AddComponent<LayoutElement>();
			sidebarLayout.preferredWidth = 150;
			sidebarLayout.minWidth = 150;
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
			layout.preferredHeight = 30;

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
			//itemSlots.Clear();

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

			itemGridContainer = new GameObject("GridContainer");
			itemGridContainer.transform.SetParent(scrollView.transform, false);

			RectTransform gridRect = itemGridContainer.GetComponent<RectTransform>();
			if (gridRect == null)
			{
				gridRect = itemGridContainer.AddComponent<RectTransform>();
			}
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

			FilterItemSlot slot = slotObj.AddComponent<FilterItemSlot>();
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
			for (int i = 0; i < itemSlots.Count; i++)
			{
				if (i < filteredItems.Count)
				{
					ItemDrop.ItemData item = filteredItems[i];
					itemSlots[i].SetItem(item);

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

		private void OnChannelChanged(string newChannel)
		{
			if (node != null)
			{
				node.SetChannel(newChannel);
			}
		}

		private void OnWhitelistChanged(bool isWhitelist)
		{
			if (node != null)
			{
				node.SetWhitelist(isWhitelist);
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

			if (whitelistToggle != null)
			{
				whitelistToggle.SetIsOnWithoutNotify(node.IsWhitelist);
			}

			OnAfterLoadNodeSettings();
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

		protected Text CreateLabel(Transform parent, string name, string text, float preferredWidth, TextAnchor alignment = TextAnchor.MiddleLeft)
		{
			GameObject labelObj = new GameObject(name);
			labelObj.transform.SetParent(parent, false);

			Text labelText = labelObj.AddComponent<Text>();
			labelText.text = text;
			labelText.alignment = alignment;

			LayoutElement layout = labelObj.AddComponent<LayoutElement>();
			layout.preferredWidth = preferredWidth;

			return labelText;
		}

		protected virtual float GetTopRowSpacing() => 15f;
		protected virtual float GetChannelLabelWidth() => 80f;
		protected virtual string GetChannelLabelText() => "Channel ID:";
		protected virtual string GetChannelPlaceholder() => "None";
		protected virtual float GetChannelInputWidth() => 150f;
		protected virtual string GetWhitelistLabelText() => "Whitelist/Blacklist:";
		protected virtual float GetWhitelistLabelWidth() => 130f;
		protected virtual string GetWhitelistToggleText() => "Whitelist";

		protected abstract string GetTitleText();
		protected virtual void AddTopRowContent(Transform topRow) { }


	}
}