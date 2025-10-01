using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Nodes;
using System.Collections.Generic;

namespace ItemConduit.GUI
{
	public class ExtractNodeGUI : BaseNodeGUI
	{
		private ExtractNode node;

		// UI Elements
		private InputField channelInput;
		private InputField filterInput;
		private Toggle whitelistToggle;
		private Text statusText;

		protected override Vector2 GetPanelSize()
		{
			return new Vector2(500, 600);
		}

		public void Initialize(ExtractNode extractNode)
		{
			node = extractNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadCurrentSettings();
		}

		private void BuildUI()
		{
			// Create content container with scroll view
			GameObject scrollView = CreateScrollView(panel);
			GameObject content = scrollView.transform.Find("Viewport/Content").gameObject;

			// Title
			CreateTitle(content.transform, "Extract Node Configuration");

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Channel Section
			CreateSectionHeader(content.transform, "Channel ID");
			channelInput = CreateInputField(content.transform, "Enter channel ID...");
			CreateChannelButtons(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Filter Section
			CreateSectionHeader(content.transform, "Item Filter");

			GameObject filterToggleContainer = CreateHorizontalGroup(content.transform);
			whitelistToggle = CreateToggle(filterToggleContainer.transform, "Whitelist Mode");

			filterInput = CreateMultilineInputField(content.transform, "Enter item names (one per line)...", 150);
			CreateFilterButtons(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Status Section
			CreateSectionHeader(content.transform, "Status");
			statusText = CreateStatusText(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 30);

			// Buttons
			CreateActionButtons(content.transform);
		}

		private GameObject CreateScrollView(GameObject parent)
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

		private void CreateTitle(Transform parent, string text)
		{
			GameObject titleObj = new GameObject("Title");
			titleObj.transform.SetParent(parent, false);

			Text titleText = titleObj.AddComponent<Text>();
			titleText.text = text;
			titleText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			titleText.fontSize = 24;
			titleText.color = new Color(1f, 0.82f, 0f); // Golden yellow
			titleText.alignment = TextAnchor.MiddleCenter;

			LayoutElement layoutElement = titleObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 40;
		}

		private void CreateSectionHeader(Transform parent, string text)
		{
			GameObject headerObj = new GameObject("Header_" + text);
			headerObj.transform.SetParent(parent, false);

			Text headerText = headerObj.AddComponent<Text>();
			headerText.text = text;
			headerText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			headerText.fontSize = 18;
			headerText.color = new Color(0.9f, 0.9f, 0.8f);
			headerText.fontStyle = FontStyle.Bold;

			LayoutElement layoutElement = headerObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 30;
		}

		private void CreateSpacer(Transform parent, float height)
		{
			GameObject spacer = new GameObject("Spacer");
			spacer.transform.SetParent(parent, false);

			LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = height;
		}

		private GameObject CreateHorizontalGroup(Transform parent)
		{
			GameObject group = new GameObject("HorizontalGroup");
			group.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;

			LayoutElement layoutElement = group.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 30;

			return group;
		}

		private InputField CreateInputField(Transform parent, string placeholder)
		{
			GameObject fieldObj = new GameObject("InputField");
			fieldObj.transform.SetParent(parent, false);

			Image background = fieldObj.AddComponent<Image>();
			background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

			InputField input = fieldObj.AddComponent<InputField>();

			// Text component
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(fieldObj.transform, false);
			Text text = textObj.AddComponent<Text>();
			text.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			text.fontSize = 16;
			text.color = Color.white;
			text.supportRichText = false;

			RectTransform textRect = textObj.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(10, 5);
			textRect.offsetMax = new Vector2(-10, -5);

			// Placeholder
			GameObject placeholderObj = new GameObject("Placeholder");
			placeholderObj.transform.SetParent(fieldObj.transform, false);
			Text placeholderText = placeholderObj.AddComponent<Text>();
			placeholderText.font = text.font;
			placeholderText.fontSize = text.fontSize;
			placeholderText.fontStyle = FontStyle.Italic;
			placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
			placeholderText.text = placeholder;

			RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
			placeholderRect.anchorMin = Vector2.zero;
			placeholderRect.anchorMax = Vector2.one;
			placeholderRect.offsetMin = textRect.offsetMin;
			placeholderRect.offsetMax = textRect.offsetMax;

			input.targetGraphic = background;
			input.textComponent = text;
			input.placeholder = placeholderText;

			LayoutElement layoutElement = fieldObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 35;

			return input;
		}

		private InputField CreateMultilineInputField(Transform parent, string placeholder, float height)
		{
			InputField input = CreateInputField(parent, placeholder);
			input.lineType = InputField.LineType.MultiLineNewline;

			LayoutElement layoutElement = input.GetComponent<LayoutElement>();
			layoutElement.preferredHeight = height;

			return input;
		}

		private Toggle CreateToggle(Transform parent, string label)
		{
			GameObject toggleObj = new GameObject("Toggle_" + label);
			toggleObj.transform.SetParent(parent, false);

			Toggle toggle = toggleObj.AddComponent<Toggle>();

			// Background
			GameObject background = new GameObject("Background");
			background.transform.SetParent(toggleObj.transform, false);
			Image bgImage = background.AddComponent<Image>();
			bgImage.color = new Color(0.3f, 0.3f, 0.3f);

			RectTransform bgRect = background.GetComponent<RectTransform>();
			bgRect.anchorMin = Vector2.zero;
			bgRect.anchorMax = Vector2.zero;
			bgRect.sizeDelta = new Vector2(20, 20);
			bgRect.anchoredPosition = new Vector2(10, 0);

			// Checkmark
			GameObject checkmark = new GameObject("Checkmark");
			checkmark.transform.SetParent(background.transform, false);
			Image checkImage = checkmark.AddComponent<Image>();
			checkImage.color = new Color(0.8f, 0.8f, 0.2f);

			RectTransform checkRect = checkmark.GetComponent<RectTransform>();
			checkRect.anchorMin = Vector2.zero;
			checkRect.anchorMax = Vector2.one;
			checkRect.offsetMin = new Vector2(4, 4);
			checkRect.offsetMax = new Vector2(-4, -4);

			// Label
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(toggleObj.transform, false);
			Text text = labelObj.AddComponent<Text>();
			text.text = label;
			text.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			text.fontSize = 14;
			text.color = Color.white;

			RectTransform labelRect = labelObj.GetComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = new Vector2(1, 1);
			labelRect.offsetMin = new Vector2(35, 0);
			labelRect.offsetMax = Vector2.zero;

			toggle.targetGraphic = bgImage;
			toggle.graphic = checkImage;

			LayoutElement layoutElement = toggleObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 25;
			layoutElement.preferredWidth = 200;

			return toggle;
		}

		private void CreateChannelButtons(Transform parent)
		{
			GameObject buttonGroup = CreateHorizontalGroup(parent);

			CreateSmallButton(buttonGroup.transform, "None", () => channelInput.text = "None");
			CreateSmallButton(buttonGroup.transform, "Ore", () => channelInput.text = "Ore");
			CreateSmallButton(buttonGroup.transform, "Food", () => channelInput.text = "Food");
			CreateSmallButton(buttonGroup.transform, "Materials", () => channelInput.text = "Materials");
		}

		private void CreateFilterButtons(Transform parent)
		{
			GameObject buttonGroup = CreateHorizontalGroup(parent);

			CreateSmallButton(buttonGroup.transform, "Ores", () => AddToFilter("CopperOre\nTinOre\nIronOre"));
			CreateSmallButton(buttonGroup.transform, "Metals", () => AddToFilter("Copper\nBronze\nIron"));
			CreateSmallButton(buttonGroup.transform, "Wood", () => AddToFilter("Wood\nFineWood\nCoreWood"));
			CreateSmallButton(buttonGroup.transform, "Clear", () => filterInput.text = "");
		}

		private void AddToFilter(string items)
		{
			if (string.IsNullOrEmpty(filterInput.text))
				filterInput.text = items;
			else
				filterInput.text += "\n" + items;
		}

		private Text CreateStatusText(Transform parent)
		{
			GameObject statusObj = new GameObject("Status");
			statusObj.transform.SetParent(parent, false);

			Text text = statusObj.AddComponent<Text>();
			text.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			text.fontSize = 14;
			text.color = new Color(0.7f, 0.7f, 0.7f);

			LayoutElement layoutElement = statusObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 25;

			return text;
		}

		private void CreateActionButtons(Transform parent)
		{
			GameObject buttonContainer = CreateHorizontalGroup(parent);

			CreateValheimButton(buttonContainer.transform, "Apply", OnApply);
			CreateValheimButton(buttonContainer.transform, "Reset", OnReset);
			CreateValheimButton(buttonContainer.transform, "Cancel", OnCancel);
		}

		private void CreateSmallButton(Transform parent, string text, System.Action action)
		{
			CreateValheimButton(parent, text, action, 70, 25);
		}

		private void CreateValheimButton(Transform parent, string text, System.Action action, float width = 120, float height = 35)
		{
			GameObject buttonObj = new GameObject("Button_" + text);
			buttonObj.transform.SetParent(parent, false);

			Button button = buttonObj.AddComponent<Button>();

			Image buttonImage = buttonObj.AddComponent<Image>();
			buttonImage.color = new Color(0.5f, 0.35f, 0.15f); // Wood color

			// Button text
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(buttonObj.transform, false);
			Text buttonText = textObj.AddComponent<Text>();
			buttonText.text = text;
			buttonText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			buttonText.fontSize = 14;
			buttonText.color = Color.white;
			buttonText.alignment = TextAnchor.MiddleCenter;

			RectTransform textRect = textObj.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			// Button colors
			ColorBlock colors = button.colors;
			colors.normalColor = new Color(1f, 1f, 1f, 1f);
			colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
			button.colors = colors;

			button.targetGraphic = buttonImage;
			button.onClick.AddListener(() => action());

			LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
			layoutElement.preferredWidth = width;
			layoutElement.preferredHeight = height;
		}

		private void OnApply()
		{
			node.SetChannel(channelInput.text);
			node.SetFilter(ParseFilter(), whitelistToggle.isOn);
			Hide();
		}

		private void OnReset()
		{
			LoadCurrentSettings();
		}

		private void OnCancel()
		{
			Hide();
		}

		private void LoadCurrentSettings()
		{
			channelInput.text = node.ChannelId ?? "None";
			whitelistToggle.isOn = node.IsWhitelist;

			if (node.ItemFilter != null && node.ItemFilter.Count > 0)
			{
				filterInput.text = string.Join("\n", node.ItemFilter);
			}

			UpdateStatus();
		}

		private HashSet<string> ParseFilter()
		{
			HashSet<string> filter = new HashSet<string>();
			string[] lines = filterInput.text.Split('\n');
			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (!string.IsNullOrEmpty(trimmed))
				{
					filter.Add(trimmed);
				}
			}
			return filter;
		}

		private void UpdateStatus()
		{
			Container container = node.GetTargetContainer();
			if (container != null)
			{
				statusText.text = $"<color=green>Connected:</color> {container.m_name}";
			}
			else
			{
				statusText.text = "<color=red>Not Connected</color>";
			}
		}
	}
}