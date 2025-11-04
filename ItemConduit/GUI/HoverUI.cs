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
		public ItemTooltip itemTooltip = new ItemTooltip();
		public void OnPointerEnter(PointerEventData eventData)
		{
			if (itemTooltip == null) return;

			itemTooltip.Show();
		}

		public void OnPointerExit(PointerEventData eventData) 
		{
			if(itemTooltip == null) return;
			itemTooltip.Hide();
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
