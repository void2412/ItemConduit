using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemConduit.Interfaces
{
	
    /// <summary>
    /// Interface for interacting with different container types in Valheim
    /// </summary>
    public interface IContainerInterface
	{
		bool CanAddItem(ItemDrop.ItemData item);
		bool CanRemoveItem(ItemDrop.ItemData item);
		bool AddItem(ItemDrop.ItemData item);
		bool RemoveItem(ItemDrop.ItemData item, int amount = 1);
		List<ItemDrop.ItemData> GetAllItems();
		int GetEmptySlots();
		string GetName();
		bool IsValid();
	}
}

