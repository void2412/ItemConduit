using ItemConduit.Config;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	public class ItemTooltip
	{
		public GameObject tooltipObject;
		private Text tooltipText;
		private RectTransform tooltipRect;
		private Canvas tooltipCanvas;  // ✅ Store canvas reference
		public ItemDrop.ItemData itemData;

		public void Create(Transform parent)
		{
			tooltipObject = new GameObject("ItemTooltip");
			tooltipObject.transform.SetParent(parent, false);

			tooltipRect = tooltipObject.AddComponent<RectTransform>();

			// ✅ FIX: Better anchor setup for positioning
			tooltipRect.anchorMin = new Vector2(0, 1);  // Top-left anchor
			tooltipRect.anchorMax = new Vector2(0, 1);
			tooltipRect.pivot = new Vector2(0, 1);      // Pivot at top-left
			tooltipRect.sizeDelta = new Vector2(300, 50);  // Initial size

			// Background
			Image bg = tooltipObject.AddComponent<Image>();
			bg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
			bg.raycastTarget = false;  // ✅ Don't block events

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
			tooltipText.raycastTarget = false;  

			// ✅ FIX: Canvas setup for proper rendering
			tooltipCanvas = tooltipObject.AddComponent<Canvas>();
			tooltipCanvas.overrideSorting = true;
			tooltipCanvas.sortingOrder = 1000;

			// ✅ CRITICAL: Add GraphicRaycaster for proper rendering
			tooltipObject.AddComponent<GraphicRaycaster>();

			tooltipObject.SetActive(false);

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"✅ Tooltip Created! Parent: {parent.name}");
			}
			
		}

		public void Show()
		{
			if (tooltipText == null)
			{
				Logger.LogError("❌ tooltipText is NULL! Create() was not called!");
				return;
			}

			if (string.IsNullOrEmpty(itemData.m_shared.m_name) && string.IsNullOrEmpty(itemData.m_dropPrefab.name))
			{
				Logger.LogWarning("⚠️ Both itemName and prefabName are empty!");
			}

			tooltipText.text = $"<b><size=16>{itemData.m_shared.m_name}</size></b>\n" +
							   $"<color=#CCCCCC><size=12>Prefab: {itemData.m_dropPrefab.name}</size></color>";

			UpdatePosition();
			tooltipObject.SetActive(true);

			// Force UI update
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

			if (DebugConfig.showDebug.Value)
			{
				// ✅ ENHANCED DEBUG - Log everything about the tooltip state
				Logger.LogInfo($"=== TOOLTIP DEBUG START ===");
				Logger.LogInfo($"📋 Item: '{itemData.m_shared.m_name}'");
				Logger.LogInfo($"✅ Active: {tooltipObject.activeSelf}");
				Logger.LogInfo($"📐 Rect Size: {tooltipRect.rect.size}");
				Logger.LogInfo($"📍 Position: {tooltipRect.position}");
				Logger.LogInfo($"📍 AnchoredPos: {tooltipRect.anchoredPosition}");
				Logger.LogInfo($"📍 LocalPos: {tooltipRect.localPosition}");
				Logger.LogInfo($"📝 Text Content: '{tooltipText.text}'");
				Logger.LogInfo($"📝 Text Font: {(tooltipText.font != null ? tooltipText.font.name : "NULL")}");
				Logger.LogInfo($"📝 Text FontSize: {tooltipText.fontSize}");
				Logger.LogInfo($"🎨 Canvas sortingOrder: {tooltipCanvas?.sortingOrder}");
				Logger.LogInfo($"🎨 Canvas overrideSorting: {tooltipCanvas?.overrideSorting}");
				Logger.LogInfo($"👁️ Parent active: {tooltipObject.transform.parent.gameObject.activeSelf}");
				Logger.LogInfo($"🖥️ Screen: {Screen.width}x{Screen.height}");
				Logger.LogInfo($"🖱️ Mouse: {Input.mousePosition}");

				// Check if tooltip is actually on screen
				Vector3[] corners = new Vector3[4];
				tooltipRect.GetWorldCorners(corners);
				Logger.LogInfo($"📦 WorldCorners: BL={corners[0]}, TL={corners[1]}, TR={corners[2]}, BR={corners[3]}");

				bool onScreen = corners[0].x < Screen.width && corners[2].x > 0 &&
								corners[0].y < Screen.height && corners[2].y > 0;
				Logger.LogInfo($"👁️ Is on screen: {onScreen}");
				Logger.LogInfo($"=== TOOLTIP DEBUG END ===");
			}
		}

		public void Hide()
		{
			if (tooltipObject != null)
			{
				tooltipObject.SetActive(false);
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo("🚫 Tooltip hidden");
				}
				
			}
		}

		public void UpdatePosition()
		{
			if (tooltipRect == null) return;

			Vector2 mousePos = Input.mousePosition;

			// Direct positioning in screen space
			tooltipRect.position = mousePos + new Vector2(15, -15);

			// Keep within screen bounds
			Vector3[] corners = new Vector3[4];
			tooltipRect.GetWorldCorners(corners);

			// Check if tooltip goes off right edge
			if (corners[2].x > Screen.width)
			{
				tooltipRect.position = new Vector2(
					mousePos.x - tooltipRect.rect.width - 15,
					tooltipRect.position.y
				);
			}

			// Check if tooltip goes off bottom edge
			if (corners[0].y < 0)
			{
				tooltipRect.position = new Vector2(
					tooltipRect.position.x,
					mousePos.y + tooltipRect.rect.height + 15
				);
			}
		}

		public bool IsVisible()
		{
			return tooltipObject != null && tooltipObject.activeSelf;
		}
	}
}