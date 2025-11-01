using ItemConduit.Config;
using UnityEngine;
using UnityEngine.UI;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	public class BaseItemSlot : MonoBehaviour
	{
		public Image background;
		public Image icon;
		public Button button;
		public Image highlightBorder;
		protected ItemDrop.ItemData currentItem;

		public virtual void SetItem(ItemDrop.ItemData item)
		{
			currentItem = item;

			if (item == null)
			{
				Clear();
				return;
			}

			try
			{
				Sprite itemIcon = item.GetIcon();
				if (itemIcon != null)
				{
					icon.sprite = itemIcon;
					icon.color = Color.white;
					icon.enabled = true;
				}
				else
				{
					Clear();
				}
			}
			catch (System.Exception ex)
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogWarning($"[ItemConduit] Failed to get icon for item: {ex.Message}");
				}
				Clear();
			}
		}

		public virtual void Clear()
		{
			currentItem = null;
			if (icon != null)
			{
				icon.enabled = false;
			}
		}

		public virtual void SetHighlight(bool highlight)
		{
			if (highlightBorder != null)
			{
				highlightBorder.enabled = highlight;
			}
		}

		public ItemDrop.ItemData GetCurrentItem()
		{
			return currentItem;
		}
	}
}

