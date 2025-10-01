using ItemConduit.Config;
using ItemConduit.Nodes;
using Jotunn;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// GUI for configuring Insert Nodes - Hopper visual style
	/// </summary>
	public class InsertNodeGUI : BaseNodeGUI
	{
		private InsertNode node;

		// UI Elements
		private InputField channelInput;
		private Slider prioritySlider;
		private Text priorityValueText;
		private Toggle whitelistToggle;
		private InputField filterInput;

		protected override Vector2 GetPanelSize()
		{
			return new Vector2(400, 480);
		}

		public void Initialize(InsertNode insertNode)
		{
			node = insertNode;
			InitializeBaseNodeUI();
			BuildUI();
			LoadCurrentSettings();

			// Apply Jötunn styling after UI is built
			ApplyJotunnStyling(panel);

			// Apply localization
			ApplyLocalization();

			// Fix references (like ValheimHopper)
			uiRoot.FixReferences(true);
		}

		private void BuildUI()
		{
			// Create content container (NO scroll view - keep it compact like Hopper)
			GameObject content = new GameObject("Content");
			content.transform.SetParent(panel.transform, false);

			VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
			contentLayout.padding = new RectOffset(20, 20, 20, 20);
			contentLayout.spacing = 8;
			contentLayout.childForceExpandWidth = true;
			contentLayout.childForceExpandHeight = false;

			RectTransform contentRect = content.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = Vector2.zero;

			// Title
			CreateTitle(content.transform, "Insert Node Configuration");
			CreateSpacer(content.transform, 10);

			// Channel ID
			CreateLabel(content.transform, "Channel ID:");
			channelInput = CreateSimpleInputField(content.transform);
			CreateSpacer(content.transform, 8);

			// Priority
			CreateLabel(content.transform, "Priority:");
			CreatePrioritySliderRow(content.transform);
			CreateSpacer(content.transform, 8);

			// Filter toggle (Hopper style - text left, checkbox right)
			whitelistToggle = CreateHopperToggle(content.transform, "Whitelist mode");
			CreateSpacer(content.transform, 4);

			// Filter input
			CreateLabel(content.transform, "Item Filter:");
			filterInput = CreateMultilineInputField(content.transform, 80);
			CreateSpacer(content.transform, 12);

			// Action buttons (horizontal row like Hopper)
			CreateActionButtons(content.transform);
		}

		#region UI Component Creation (Hopper Style)

		private Text CreateLabel(Transform parent, string text)
		{
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(parent, false);

			Text label = labelObj.AddComponent<Text>();
			label.text = text;
			label.alignment = TextAnchor.MiddleLeft;

			LayoutElement layoutElement = labelObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 20;

			return label;
		}

		private InputField CreateSimpleInputField(Transform parent)
		{
			GameObject fieldObj = new GameObject("InputField");
			fieldObj.transform.SetParent(parent, false);

			Image background = fieldObj.AddComponent<Image>();
			background.color = new Color(0, 0, 0, 0.5f);

			InputField input = fieldObj.AddComponent<InputField>();

			// Text
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(fieldObj.transform, false);
			Text text = textObj.AddComponent<Text>();
			text.supportRichText = false;

			RectTransform textRect = textObj.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(8, 2);
			textRect.offsetMax = new Vector2(-8, -2);

			// Placeholder
			GameObject placeholderObj = new GameObject("Placeholder");
			placeholderObj.transform.SetParent(fieldObj.transform, false);
			Text placeholderText = placeholderObj.AddComponent<Text>();
			placeholderText.fontStyle = FontStyle.Italic;
			placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
			placeholderText.text = "Enter channel...";

			RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
			placeholderRect.anchorMin = Vector2.zero;
			placeholderRect.anchorMax = Vector2.one;
			placeholderRect.offsetMin = textRect.offsetMin;
			placeholderRect.offsetMax = textRect.offsetMax;

			input.targetGraphic = background;
			input.textComponent = text;
			input.placeholder = placeholderText;

			LayoutElement layoutElement = fieldObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 30;

			return input;
		}

		private InputField CreateMultilineInputField(Transform parent, float height)
		{
			InputField input = CreateSimpleInputField(parent);
			input.lineType = InputField.LineType.MultiLineNewline;

			Text placeholderText = input.placeholder as Text;
			if (placeholderText != null)
			{
				placeholderText.text = "Item names (one per line)...";
			}

			LayoutElement layoutElement = input.GetComponent<LayoutElement>();
			layoutElement.preferredHeight = height;

			return input;
		}

		private void CreatePrioritySliderRow(Transform parent)
		{
			GameObject sliderRow = new GameObject("PriorityRow");
			sliderRow.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = sliderRow.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			// Min label
			GameObject minLabel = new GameObject("MinLabel");
			minLabel.transform.SetParent(sliderRow.transform, false);
			Text minText = minLabel.AddComponent<Text>();
			minText.text = "-100";
			minText.alignment = TextAnchor.MiddleCenter;

			LayoutElement minLayout = minLabel.AddComponent<LayoutElement>();
			minLayout.preferredWidth = 35;
			minLayout.flexibleWidth = 0;

			// Slider - FIXED: Create properly
			prioritySlider = CreateSlider(sliderRow.transform);

			// Max label
			GameObject maxLabel = new GameObject("MaxLabel");
			maxLabel.transform.SetParent(sliderRow.transform, false);
			Text maxText = maxLabel.AddComponent<Text>();
			maxText.text = "100";
			maxText.alignment = TextAnchor.MiddleCenter;

			LayoutElement maxLayout = maxLabel.AddComponent<LayoutElement>();
			maxLayout.preferredWidth = 35;
			maxLayout.flexibleWidth = 0;

			// Value display
			GameObject valueLabel = new GameObject("ValueLabel");
			valueLabel.transform.SetParent(sliderRow.transform, false);
			priorityValueText = valueLabel.AddComponent<Text>();
			priorityValueText.text = "0";
			priorityValueText.alignment = TextAnchor.MiddleCenter;

			LayoutElement valueLayout = valueLabel.AddComponent<LayoutElement>();
			valueLayout.preferredWidth = 40;
			valueLayout.flexibleWidth = 0;

			LayoutElement rowLayout = sliderRow.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 30;

			// Update value text when slider changes
			prioritySlider.onValueChanged.AddListener(value =>
			{
				priorityValueText.text = value.ToString("F0");
			});
		}

		private Slider CreateSlider(Transform parent)
		{
			// Create slider GameObject with RectTransform
			GameObject sliderObj = new GameObject("Slider");
			RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
			sliderObj.transform.SetParent(parent, false);
			sliderRect.sizeDelta = new Vector2(100, 20);

			// Add Slider component
			Slider slider = sliderObj.AddComponent<Slider>();

			// Background
			GameObject background = new GameObject("Background");
			RectTransform bgRect = background.AddComponent<RectTransform>();
			background.transform.SetParent(sliderObj.transform, false);

			Image bgImage = background.AddComponent<Image>();
			bgImage.color = new Color(0.2f, 0.2f, 0.2f);

			bgRect.anchorMin = Vector2.zero;
			bgRect.anchorMax = Vector2.one;
			bgRect.sizeDelta = Vector2.zero;
			bgRect.anchoredPosition = Vector2.zero;

			// Fill Area
			GameObject fillArea = new GameObject("Fill Area");
			RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
			fillArea.transform.SetParent(sliderObj.transform, false);

			fillAreaRect.anchorMin = Vector2.zero;
			fillAreaRect.anchorMax = Vector2.one;
			fillAreaRect.sizeDelta = Vector2.zero;
			fillAreaRect.anchoredPosition = Vector2.zero;

			// Fill
			GameObject fill = new GameObject("Fill");
			RectTransform fillRect = fill.AddComponent<RectTransform>();
			fill.transform.SetParent(fillArea.transform, false);

			Image fillImage = fill.AddComponent<Image>();
			fillImage.color = new Color(0.8f, 0.6f, 0.2f);

			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = Vector2.one;
			fillRect.sizeDelta = Vector2.zero;
			fillRect.anchoredPosition = Vector2.zero;

			// Handle Slide Area
			GameObject handleArea = new GameObject("Handle Slide Area");
			RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
			handleArea.transform.SetParent(sliderObj.transform, false);

			handleAreaRect.anchorMin = Vector2.zero;
			handleAreaRect.anchorMax = Vector2.one;
			handleAreaRect.sizeDelta = Vector2.zero;
			handleAreaRect.anchoredPosition = Vector2.zero;

			// Handle
			GameObject handle = new GameObject("Handle");
			RectTransform handleRect = handle.AddComponent<RectTransform>();
			handle.transform.SetParent(handleArea.transform, false);

			Image handleImage = handle.AddComponent<Image>();
			handleImage.color = Color.white;

			handleRect.sizeDelta = new Vector2(16, 0);
			handleRect.anchoredPosition = Vector2.zero;

			// Configure slider references
			slider.fillRect = fillRect;
			slider.handleRect = handleRect;
			slider.targetGraphic = handleImage;
			slider.direction = Slider.Direction.LeftToRight;
			slider.minValue = -100;
			slider.maxValue = 100;
			slider.wholeNumbers = true;
			slider.value = 0;

			// Add layout element
			LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
			sliderLayout.flexibleWidth = 1;
			sliderLayout.preferredHeight = 20;

			return slider;
		}

		/// <summary>
		/// Create toggle in Hopper style: text on left, checkbox on right
		/// </summary>
		private Toggle CreateHopperToggle(Transform parent, string label)
		{
			GameObject toggleRow = new GameObject("ToggleRow_" + label);
			toggleRow.transform.SetParent(parent, false);

			// Horizontal layout
			HorizontalLayoutGroup layout = toggleRow.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.childAlignment = TextAnchor.MiddleLeft;

			// Label text (on the left)
			GameObject labelObj = new GameObject("Label");
			labelObj.transform.SetParent(toggleRow.transform, false);
			Text labelText = labelObj.AddComponent<Text>();
			labelText.text = label;
			labelText.alignment = TextAnchor.MiddleLeft;

			LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
			labelLayout.flexibleWidth = 1;

			// Toggle (on the right)
			GameObject toggleObj = new GameObject("Toggle");
			toggleObj.transform.SetParent(toggleRow.transform, false);
			Toggle toggle = toggleObj.AddComponent<Toggle>();

			// Background circle
			GameObject background = new GameObject("Background");
			background.transform.SetParent(toggleObj.transform, false);
			Image bgImage = background.AddComponent<Image>();
			bgImage.color = new Color(0.2f, 0.2f, 0.2f);

			RectTransform bgRect = background.GetComponent<RectTransform>();
			bgRect.anchorMin = new Vector2(0.5f, 0.5f);
			bgRect.anchorMax = new Vector2(0.5f, 0.5f);
			bgRect.sizeDelta = new Vector2(20, 20);

			// Checkmark
			GameObject checkmark = new GameObject("Checkmark");
			checkmark.transform.SetParent(background.transform, false);
			Image checkImage = checkmark.AddComponent<Image>();
			checkImage.color = new Color(0.8f, 0.7f, 0.3f); // Yellow-ish like Hopper

			RectTransform checkRect = checkmark.GetComponent<RectTransform>();
			checkRect.anchorMin = Vector2.zero;
			checkRect.anchorMax = Vector2.one;
			checkRect.offsetMin = new Vector2(4, 4);
			checkRect.offsetMax = new Vector2(-4, -4);

			toggle.targetGraphic = bgImage;
			toggle.graphic = checkImage;

			LayoutElement toggleLayout = toggleObj.AddComponent<LayoutElement>();
			toggleLayout.preferredWidth = 20;
			toggleLayout.preferredHeight = 20;
			toggleLayout.flexibleWidth = 0;

			LayoutElement rowLayout = toggleRow.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 25;

			return toggle;
		}

		/// <summary>
		/// Create action buttons in Hopper style (horizontal row at bottom)
		/// </summary>
		private void CreateActionButtons(Transform parent)
		{
			GameObject buttonRow = new GameObject("ButtonRow");
			buttonRow.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			// Create three buttons like Hopper: Reset, Copy, Paste equivalent
			CreateHopperButton(buttonRow.transform, "Apply", OnApply);
			CreateHopperButton(buttonRow.transform, "Reset", OnReset);
			CreateHopperButton(buttonRow.transform, "Cancel", OnCancel);

			LayoutElement rowLayout = buttonRow.AddComponent<LayoutElement>();
			rowLayout.preferredHeight = 35;
		}

		private void CreateHopperButton(Transform parent, string text, System.Action action)
		{
			GameObject buttonObj = new GameObject("Button_" + text);
			buttonObj.transform.SetParent(parent, false);

			Button button = buttonObj.AddComponent<Button>();
			Image buttonImage = buttonObj.AddComponent<Image>();
			buttonImage.color = new Color(0, 0, 0, 0.7f); // Dark background like Hopper

			// Button text
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(buttonObj.transform, false);
			Text buttonText = textObj.AddComponent<Text>();
			buttonText.text = text;
			buttonText.alignment = TextAnchor.MiddleCenter;

			RectTransform textRect = textObj.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			button.targetGraphic = buttonImage;
			button.onClick.AddListener(() => action());

			LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 35;
			layoutElement.flexibleWidth = 1;
		}

		#endregion

		#region Event Handlers

		private void OnApply()
		{
			// Set channel
			node.SetChannel(channelInput.text);

			// Set priority
			int priority = (int)prioritySlider.value;
			node.SetPriority(priority);

			// Set filter

			node.SetFilter(ParseFilter(), whitelistToggle.isOn);


			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Applied settings: Channel={channelInput.text}, Priority={priority}");
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
			channelInput.text = node.ChannelId ?? "None";
			prioritySlider.value = node.Priority;
			priorityValueText.text = node.Priority.ToString("F0");

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

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"Filter Settings: {string.Join(",", filter)}");
			}
			return filter;
		}

		#endregion
	}
}