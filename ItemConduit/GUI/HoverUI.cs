using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ItemConduit.GUI
{
	public class HoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public ItemTooltip itemTooltip;
		private ItemDrop.ItemData itemData;
		public void OnPointerEnter(PointerEventData eventData)
		{
			if (itemTooltip == null || itemData == null) return;
			itemTooltip.itemData = itemData;
			itemTooltip.Show();
		}

		public void OnPointerExit(PointerEventData eventData) 
		{
			if (itemTooltip == null) return;
			itemTooltip.Hide();
		}
		public void SetSharedTooltip(ItemTooltip tooltip)
		{
			itemTooltip = tooltip;
		}
		public void SetItemData(ItemDrop.ItemData data)
		{
			itemData = data;
		}
		private void Update()
		{
			if (itemTooltip != null && itemTooltip.IsVisible())
			{
				itemTooltip.UpdatePosition();
			}
		}
	}
}
