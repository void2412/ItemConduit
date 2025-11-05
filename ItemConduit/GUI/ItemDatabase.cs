using ItemConduit.Config;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Global item database cache - loads once per game session
	/// </summary>
	public class ItemDatabase : MonoBehaviour
	{
		private static ItemDatabase _instance;
		public static ItemDatabase Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_ItemDatabase");
					_instance = go.AddComponent<ItemDatabase>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		// ✅ Static cache - shared across all GUIs
		private static List<ItemDrop.ItemData> cachedAllItems = null;
		private static Dictionary<string, Sprite> cachedIcons = null;
		private static bool isLoaded = false;
		private static bool isLoading = false;

		/// <summary>
		/// Check if database is ready
		/// </summary>
		public bool IsLoaded => isLoaded;

		/// <summary>
		/// Get all items (returns cached list)
		/// </summary>
		public List<ItemDrop.ItemData> GetAllItems()
		{
			if (!isLoaded)
			{
				LoadDatabaseSync(); // Fallback to sync load
			}
			return cachedAllItems ?? new List<ItemDrop.ItemData>();
		}

		/// <summary>
		/// Get cached icon for an item
		/// </summary>
		public Sprite GetIcon(ItemDrop.ItemData item)
		{
			if (cachedIcons == null) return null;

			string key = item.m_dropPrefab?.name ?? item.m_shared.m_name;
			return cachedIcons.TryGetValue(key, out Sprite icon) ? icon : null;
		}

		/// <summary>
		/// Load database synchronously (immediate but blocks)
		/// </summary>
		public void LoadDatabaseSync()
		{
			if (isLoaded || isLoading) return;
			if (ObjectDB.instance == null) return;

			isLoading = true;
			cachedAllItems = new List<ItemDrop.ItemData>();
			cachedIcons = new Dictionary<string, Sprite>();

			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
				if (itemDrop != null && itemDrop.m_itemData != null)
				{
					try
					{
						Sprite icon = itemDrop.m_itemData.GetIcon();
						if (icon != null)
						{
							itemDrop.m_itemData.m_dropPrefab = prefab;
							cachedAllItems.Add(itemDrop.m_itemData);

							string key = prefab.name;
							cachedIcons[key] = icon;
						}
					}
					catch (System.Exception ex)
					{
						if (DebugConfig.showDebug.Value)
						{
							Logger.LogWarning($"[ItemConduit] Failed to load item {prefab.name}: {ex.Message}");
						}
					}
				}
			}

			isLoaded = true;
			isLoading = false;

			Logger.LogInfo($"[ItemConduit] Item database loaded: {cachedAllItems.Count} items");
		}

		/// <summary>
		/// Load database asynchronously across multiple frames
		/// </summary>
		public IEnumerator LoadDatabaseAsync()
		{
			if (isLoaded || isLoading) yield break;
			if (ObjectDB.instance == null) yield break;

			isLoading = true;
			cachedAllItems = new List<ItemDrop.ItemData>();
			cachedIcons = new Dictionary<string, Sprite>();

			int itemsPerFrame = 50; // Process 50 items per frame
			int count = 0;

			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
				if (itemDrop != null && itemDrop.m_itemData != null)
				{
					try
					{
						Sprite icon = itemDrop.m_itemData.GetIcon();
						if (icon != null)
						{
							itemDrop.m_itemData.m_dropPrefab = prefab;
							cachedAllItems.Add(itemDrop.m_itemData);

							string key = prefab.name;
							cachedIcons[key] = icon;
						}
					}
					catch { }
				}

				count++;
				if (count % itemsPerFrame == 0)
				{
					yield return null; // Yield every 50 items
				}
			}

			isLoaded = true;
			isLoading = false;

			Logger.LogInfo($"[ItemConduit] Item database loaded asynchronously: {cachedAllItems.Count} items");
		}

		/// <summary>
		/// Clear cache (for world changes)
		/// </summary>
		public void ClearCache()
		{
			cachedAllItems = null;
			cachedIcons = null;
			isLoaded = false;
			isLoading = false;
		}
	}
}