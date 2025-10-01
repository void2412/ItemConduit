using UnityEngine;
using UnityEngine.UI;
using ItemConduit.Nodes;
using ItemConduit.Config;
using System.Collections.Generic;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	public class InsertNodeGUI : BaseNodeGUI
	{
		private InsertNode node;

		// UI Elements
		private InputField channelInput;
		private Slider prioritySlider;
		private InputField priorityInput;
		private Text statusText;
		private Text containerInfoText;
		private Toggle whitelistToggle;
		private InputField filterInput;

		protected override Vector2 GetPanelSize()
		{
			return new Vector2(450, 550);
		}

		public void Initialize(InsertNode insertNode)
		{
			node = insertNode;
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
			CreateTitle(content.transform, "Insert Node Configuration");

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Channel Section
			CreateSectionHeader(content.transform, "Channel ID");
			CreateInfoText(content.transform, "Receives items only from Extract nodes with matching channel", 10);
			channelInput = CreateInputField(content.transform, "Enter channel ID...");
			CreateChannelButtons(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Priority Section
			CreateSectionHeader(content.transform, "Container Priority");
			CreateInfoText(content.transform, "Higher priority containers are filled first (-100 to 100)", 10);
			CreatePriorityControls(content.transform);
			CreatePriorityPresets(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Filter Section (Optional - similar to Extract)
			CreateSectionHeader(content.transform, "Item Filter");
			CreateInfoText(content.transform, "Optional: Restrict which items this container accepts", 10);

			GameObject filterToggleContainer = CreateHorizontalGroup(content.transform);
			whitelistToggle = CreateToggle(filterToggleContainer.transform, "Accept Only Listed Items");

			filterInput = CreateMultilineInputField(content.transform, "Enter item names (one per line)...", 100);
			CreateFilterButtons(content.transform);

			// Add spacing
			CreateSpacer(content.transform, 20);

			// Status Section
			CreateSectionHeader(content.transform, "Status");
			statusText = CreateStatusText(content.transform);
			containerInfoText = CreateStatusText(content.transform);

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

		private void CreateInfoText(Transform parent, string text, float spacing)
		{
			GameObject infoObj = new GameObject("Info");
			infoObj.transform.SetParent(parent, false);

			Text infoText = infoObj.AddComponent<Text>();
			infoText.text = text;
			infoText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			infoText.fontSize = 12;
			infoText.fontStyle = FontStyle.Italic;
			infoText.color = new Color(0.7f, 0.7f, 0.6f);

			LayoutElement layoutElement = infoObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 20;

			if (spacing > 0)
			{
				CreateSpacer(parent, spacing);
			}
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
			layout.childAlignment = TextAnchor.MiddleCenter;

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

		private void CreatePriorityControls(Transform parent)
		{
			// Slider container
			GameObject sliderContainer = new GameObject("SliderContainer");
			sliderContainer.transform.SetParent(parent, false);

			HorizontalLayoutGroup sliderLayout = sliderContainer.AddComponent<HorizontalLayoutGroup>();
			sliderLayout.spacing = 10;
			sliderLayout.childForceExpandWidth = true;
			sliderLayout.childForceExpandHeight = false;
			sliderLayout.childAlignment = TextAnchor.MiddleCenter;

			// Min label
			GameObject minLabel = new GameObject("MinLabel");
			minLabel.transform.SetParent(sliderContainer.transform, false);
			Text minText = minLabel.AddComponent<Text>();
			minText.text = "-100";
			minText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			minText.fontSize = 14;
			minText.color = new Color(0.6f, 0.6f, 0.6f);
			minText.alignment = TextAnchor.MiddleCenter;

			LayoutElement minLayout = minLabel.AddComponent<LayoutElement>();
			minLayout.preferredWidth = 40;
			minLayout.flexibleWidth = 0;

			// Slider
			GameObject sliderObj = new GameObject("PrioritySlider");
			sliderObj.transform.SetParent(sliderContainer.transform, false);

			prioritySlider = CreateSlider(sliderObj);

			// Max label
			GameObject maxLabel = new GameObject("MaxLabel");
			maxLabel.transform.SetParent(sliderContainer.transform, false);
			Text maxText = maxLabel.AddComponent<Text>();
			maxText.text = "100";
			maxText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			maxText.fontSize = 14;
			maxText.color = new Color(0.6f, 0.6f, 0.6f);
			maxText.alignment = TextAnchor.MiddleCenter;

			LayoutElement maxLayout = maxLabel.AddComponent<LayoutElement>();
			maxLayout.preferredWidth = 40;
			maxLayout.flexibleWidth = 0;

			LayoutElement containerLayout = sliderContainer.AddComponent<LayoutElement>();
			containerLayout.preferredHeight = 30;

			// Priority value input
			GameObject valueContainer = CreateHorizontalGroup(parent);

			GameObject valueLabel = new GameObject("ValueLabel");
			valueLabel.transform.SetParent(valueContainer.transform, false);
			Text valueLabelText = valueLabel.AddComponent<Text>();
			valueLabelText.text = "Value:";
			valueLabelText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			valueLabelText.fontSize = 14;
			valueLabelText.color = Color.white;

			LayoutElement valueLabelLayout = valueLabel.AddComponent<LayoutElement>();
			valueLabelLayout.preferredWidth = 50;

			// Priority input field
			priorityInput = CreateSmallInputField(valueContainer.transform, "0", 80);

			// Connect slider and input field
			prioritySlider.onValueChanged.AddListener((value) => {
				priorityInput.text = Mathf.RoundToInt(value).ToString();
			});

			priorityInput.onValueChanged.AddListener((text) => {
				if (int.TryParse(text, out int value))
				{
					value = Mathf.Clamp(value, -100, 100);
					prioritySlider.value = value;
					priorityInput.text = value.ToString();
				}
			});
		}

		private Slider CreateSlider(GameObject parent)
		{
			Slider slider = parent.AddComponent<Slider>();

			// Background
			GameObject background = new GameObject("Background");
			background.transform.SetParent(parent.transform, false);
			Image bgImage = background.AddComponent<Image>();
			bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

			RectTransform bgRect = background.GetComponent<RectTransform>();
			bgRect.anchorMin = new Vector2(0, 0.5f);
			bgRect.anchorMax = new Vector2(1, 0.5f);
			bgRect.offsetMin = new Vector2(0, -2);
			bgRect.offsetMax = new Vector2(0, 2);

			// Fill area
			GameObject fillArea = new GameObject("Fill Area");
			fillArea.transform.SetParent(parent.transform, false);

			RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
			fillAreaRect.anchorMin = new Vector2(0, 0.5f);
			fillAreaRect.anchorMax = new Vector2(1, 0.5f);
			fillAreaRect.offsetMin = new Vector2(0, -2);
			fillAreaRect.offsetMax = new Vector2(0, 2);

			// Fill
			GameObject fill = new GameObject("Fill");
			fill.transform.SetParent(fillArea.transform, false);
			Image fillImage = fill.AddComponent<Image>();
			fillImage.color = new Color(0.8f, 0.6f, 0.2f, 0.8f); // Amber color

			RectTransform fillRect = fill.GetComponent<RectTransform>();
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = Vector2.one;
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;

			// Handle area
			GameObject handleArea = new GameObject("Handle Slide Area");
			handleArea.transform.SetParent(parent.transform, false);

			RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
			handleAreaRect.anchorMin = new Vector2(0, 0);
			handleAreaRect.anchorMax = new Vector2(1, 1);
			handleAreaRect.offsetMin = new Vector2(10, 0);
			handleAreaRect.offsetMax = new Vector2(-10, 0);

			// Handle
			GameObject handle = new GameObject("Handle");
			handle.transform.SetParent(handleArea.transform, false);
			Image handleImage = handle.AddComponent<Image>();
			handleImage.color = new Color(0.9f, 0.7f, 0.3f); // Light wood color

			RectTransform handleRect = handle.GetComponent<RectTransform>();
			handleRect.sizeDelta = new Vector2(20, 20);

			// Setup slider component
			slider.fillRect = fillRect;
			slider.handleRect = handleRect;
			slider.targetGraphic = handleImage;
			slider.minValue = -100;
			slider.maxValue = 100;
			slider.wholeNumbers = true;
			slider.value = 0;

			LayoutElement sliderLayout = parent.AddComponent<LayoutElement>();
			sliderLayout.preferredHeight = 25;
			sliderLayout.flexibleWidth = 1;

			return slider;
		}

		private InputField CreateSmallInputField(Transform parent, string placeholder, float width)
		{
			InputField input = CreateInputField(parent, placeholder);

			LayoutElement layoutElement = input.GetComponent<LayoutElement>();
			layoutElement.preferredWidth = width;
			layoutElement.preferredHeight = 25;
			layoutElement.flexibleWidth = 0;

			return input;
		}

		private void CreatePriorityPresets(Transform parent)
		{
			CreateInfoText(parent, "Quick Priority Settings:", 5);

			GameObject buttonGroup = CreateHorizontalGroup(parent);

			CreatePriorityButton(buttonGroup.transform, "Lowest\n(-100)", -100, new Color(0.5f, 0.5f, 0.5f));
			CreatePriorityButton(buttonGroup.transform, "Low\n(-50)", -50, new Color(0.6f, 0.6f, 0.6f));
			CreatePriorityButton(buttonGroup.transform, "Normal\n(0)", 0, new Color(0.7f, 0.7f, 0.7f));
			CreatePriorityButton(buttonGroup.transform, "High\n(50)", 50, new Color(0.8f, 0.7f, 0.4f));
			CreatePriorityButton(buttonGroup.transform, "Highest\n(100)", 100, new Color(0.9f, 0.8f, 0.3f));
		}

		private void CreatePriorityButton(Transform parent, string text, int priority, Color tint)
		{
			GameObject buttonObj = new GameObject("PriorityButton_" + priority);
			buttonObj.transform.SetParent(parent, false);

			Button button = buttonObj.AddComponent<Button>();

			Image buttonImage = buttonObj.AddComponent<Image>();
			buttonImage.color = new Color(0.4f, 0.3f, 0.2f); // Dark wood base

			// Button text
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(buttonObj.transform, false);
			Text buttonText = textObj.AddComponent<Text>();
			buttonText.text = text;
			buttonText.font = norseFont ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
			buttonText.fontSize = 11;
			buttonText.color = tint;
			buttonText.alignment = TextAnchor.MiddleCenter;

			RectTransform textRect = textObj.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			// Button colors with priority-based tinting
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
			button.colors = colors;

			button.targetGraphic = buttonImage;
			button.onClick.AddListener(() => SetPriority(priority));

			LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
			layoutElement.preferredWidth = 70;
			layoutElement.preferredHeight = 40;
		}

		private void SetPriority(int value)
		{
			priorityInput.text = value.ToString();
			prioritySlider.value = value;
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

			CreateSmallButton(buttonGroup.transform, "Basic", () => AddToFilter("Wood\nStone\nResin"));
			CreateSmallButton(buttonGroup.transform, "Valuable", () => AddToFilter("Coins\nRuby\nAmber"));
			CreateSmallButton(buttonGroup.transform, "Food", () => AddToFilter("CookedMeat\nBread\nHoney"));
			CreateSmallButton(buttonGroup.transform, "Clear", () => filterInput.text = "");
		}

		private void AddToFilter(string items)
		{
			if (string.IsNullOrEmpty(filterInput.text))
				filterInput.text = items;
			else
				filterInput.text += "\n" + items;
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
			layoutElement.preferredWidth = 250;

			return toggle;
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
			layoutElement.preferredHeight = 20;

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
			CreateValheimButton(parent, text, action, 80, 25);
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
			// Set channel
			node.SetChannel(channelInput.text);

			// Set priority
			if (int.TryParse(priorityInput.text, out int priority))
			{
				priority = Mathf.Clamp(priority, -100, 100);
				node.SetPriority(priority);
			}

			// Set filter if used
			if (!string.IsNullOrWhiteSpace(filterInput.text))
			{
				node.SetFilter(ParseFilter(), whitelistToggle.isOn);
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Applied settings to insert node: Channel={channelInput.text}, Priority={priority}");
			}

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
			// Load channel
			channelInput.text = node.ChannelId ?? "None";

			// Load priority
			priorityInput.text = node.Priority.ToString();
			prioritySlider.value = node.Priority;

			// Load filter if present
			if (node.ItemFilter != null && node.ItemFilter.Count > 0)
			{
				filterInput.text = string.Join("\n", node.ItemFilter);
				whitelistToggle.isOn = node.IsWhitelist;
			}
			else
			{
				filterInput.text = "";
				whitelistToggle.isOn = true;
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
			// Network status
			string networkInfo = node.NetworkId != null
				? $"<color=green>Network:</color> Connected"
				: "<color=yellow>Network:</color> Not Connected";
			statusText.text = networkInfo;

			// Container status
			Container container = node.GetTargetContainer();
			if (container != null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null)
				{
					int emptySlots = inv.GetEmptySlots();
					int totalSlots = inv.GetWidth() * inv.GetHeight();
					int usedSlots = totalSlots - emptySlots;

					string fillStatus = emptySlots > 0
						? $"<color=green>{container.m_name}</color>"
						: $"<color=red>{container.m_name} (FULL)</color>";

					containerInfoText.text = $"Container: {fillStatus} [{usedSlots}/{totalSlots} slots used]";
				}
				else
				{
					containerInfoText.text = $"<color=yellow>Container: {container.m_name} (No Inventory)</color>";
				}
			}
			else
			{
				containerInfoText.text = "<color=red>Container: Not Connected</color>";
			}
		}
	}
}