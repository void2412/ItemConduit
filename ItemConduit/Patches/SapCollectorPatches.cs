using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Patches
{
	public static class SapCollectorPatches
	{
		[HarmonyPatch(typeof(SapCollector), "Awake")]
		public static class SapCollector_Awake_Patch
		{
			private static void Postfix(SapCollector __instance)
			{
				if (__instance.GetComponent<SapCollectorExtention>() == null)
				{
					__instance.gameObject.AddComponent<SapCollectorExtention>();
				}
			}
		}

		[HarmonyPatch(typeof(SapCollector), "IncreseLevel")]
		public static class SapCollector_IncreseLevel_Patch
		{
			private static bool Prefix(SapCollector __instance, int i)
			{
				var extension = __instance.GetComponent<SapCollectorExtention>();
				if (extension.IsConnected)
				{
					int currentSap = __instance.GetLevel();
					if (currentSap > 0)
					{
						currentSap += i;
						currentSap = Mathf.Clamp(currentSap, 0, __instance.m_maxLevel);
						__instance.m_nview.GetZDO().Set(ZDOVars.s_level, 0, false);

						ItemDrop.ItemData itemData = __instance.m_spawnItem.m_itemData.Clone();
						itemData.m_dropPrefab = __instance.m_spawnItem.gameObject;
						itemData.m_stack = currentSap;
						extension.m_container.m_inventory.AddItem(itemData);
					}
					else
					{

						ItemDrop.ItemData itemData = __instance.m_spawnItem.m_itemData.Clone();
						itemData.m_dropPrefab = __instance.m_spawnItem.gameObject;

						//get current sap in inventory
						var currentSapInventory = extension.m_container.m_inventory.GetAllItems();
						if (currentSapInventory.Count <= 0) currentSap = 0;
						foreach (var item in currentSapInventory)
						{
							if (item == null) continue;

							if (itemData.m_dropPrefab.name == item.m_dropPrefab.name)
							{
								currentSap = item.m_stack;
								break;
							}

						}

						int futureSap = currentSap + i;

						if (futureSap <= __instance.m_maxLevel)
						{
							itemData.m_stack = i;
							extension.m_container.m_inventory.AddItem(itemData);
						}
						else
						{
							if (currentSap < __instance.m_maxLevel)
							{
								itemData.m_stack = __instance.m_maxLevel - currentSap;
								extension.m_container.m_inventory.AddItem(itemData);
							}
						}


					}

					extension.SaveInventoryToZDO();
					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(SapCollector), "GetHoverText")]
		public static class SapCollector_GetHoverText_Patch
		{
			private static bool Prefix(SapCollector __instance, ref string __result)
			{
				var extension = __instance.GetComponent<SapCollectorExtention>();
				if (extension.IsConnected)
				{
					__result = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Sap Collector Output");
					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(SapCollector), "Interact")]
		public static class SapCollector_Interact_Patch
		{
			private static bool Prefix(SapCollector __instance)
			{
				var extension = __instance.GetComponent<SapCollectorExtention>();
				if (extension == null) return true;

				if (extension.IsConnected)
				{
					InventoryGui.instance.Show(extension.m_container, 1);
					return false;
				}

				return true;
			}
		}
	}
}
