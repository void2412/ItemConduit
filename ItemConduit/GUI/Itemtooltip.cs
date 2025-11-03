using UnityEngine;
using UnityEngine.UI;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Simple tooltip displayer - just call Show() and Hide()
	/// </summary>
	public class ItemTooltip
	{
		private GameObject tooltipObject;
		private Text tooltipText;
		private RectTransform tooltipRect;
		public string itemName = "";
		public string prefabName = "";

		/// <summary>
		/// Create the tooltip (call this once in your GUI's Initialize or BuildUI)
		/// </summary>


		public void Create(Transform parent)
		{
			tooltipObject = new GameObject("ItemTooltip");
			tooltipObject.transform.SetParent(parent, false);

			tooltipRect = tooltipObject.AddComponent<RectTransform>();
			tooltipRect.anchorMin = Vector2.zero;
			tooltipRect.anchorMax = Vector2.zero;
			tooltipRect.pivot = new Vector2(0, 1);

			// Background
			Image bg = tooltipObject.AddComponent<Image>();
			bg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

			// Border
			Outline outline = tooltipObject.AddComponent<Outline>();
			outline.effectColor = new Color(0.7f, 0.6f, 0.4f, 1f);
			outline.effectDistance = new Vector2(2, -2);

			// Text
			GameObject textObj = new GameObject("Text");
			textObj.transform.SetParent(tooltipObject.transform, false);
			RectTransform textRect = textObj.AddComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.sizeDelta = new Vector2(-16, -16);

			tooltipText = textObj.AddComponent<Text>();
			tooltipText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			tooltipText.fontSize = 14;
			tooltipText.color = Color.white;
			tooltipText.alignment = TextAnchor.UpperLeft;
			tooltipText.supportRichText = true;

			// Auto-size
			ContentSizeFitter fitter = tooltipObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			// Render on top
			Canvas canvas = tooltipObject.AddComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = 1000;

			tooltipObject.SetActive(false);
		}

		/// <summary>
		/// Show tooltip with item info
		/// </summary>
		public void Show()
		{
			if (tooltipText == null) return;

			tooltipText.text = $"<b><size=16>{itemName}</size></b>\n" +
							   $"<color=#CCCCCC><size=12>Prefab: {prefabName}</size></color>";

			UpdatePosition();
			tooltipObject.SetActive(true);

			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
		}

		/// <summary>
		/// Hide tooltip
		/// </summary>
		public void Hide()
		{
			if (tooltipObject != null)
				tooltipObject.SetActive(false);
		}

		/// <summary>
		/// Update position near mouse (call in Update if tooltip is visible)
		/// </summary>
		public void UpdatePosition()
		{
			if (tooltipRect == null) return;

			Vector2 mousePos = Input.mousePosition;
			tooltipRect.position = mousePos + new Vector2(15, -15);

			// Keep within screen bounds
			Vector3[] corners = new Vector3[4];
			tooltipRect.GetWorldCorners(corners);

			if (corners[2].x > Screen.width)
				tooltipRect.position = mousePos + new Vector2(-tooltipRect.rect.width - 15, -15);

			if (corners[0].y < 0)
				tooltipRect.position = new Vector3(tooltipRect.position.x,
												   mousePos.y + tooltipRect.rect.height + 15,
												   tooltipRect.position.z);
		}

		/// <summary>
		/// Check if tooltip is currently visible
		/// </summary>
		public bool IsVisible()
		{
			return tooltipObject != null && tooltipObject.activeSelf;
		}
	}
}