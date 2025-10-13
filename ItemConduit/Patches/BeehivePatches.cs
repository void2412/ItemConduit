using HarmonyLib;
using ItemConduit.Debug;
using ItemConduit.Extensions;
using ItemConduit.Interfaces;
using Jotunn;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ItemConduit.Patches
{
	public static class BeehivePatches
	{
		[HarmonyPatch(typeof(Beehive), "Awake")]
		public static class Beehive_Awake_Patch
		{
			private static void Postfix(Beehive __instance)
			{
				if (__instance.GetComponent<BeehiveExtension>() == null) 
				{
					__instance.gameObject.AddComponent<BeehiveExtension>();
				}
			}
		}

		[HarmonyPatch(typeof(Beehive), "IncreseLevel")]
		public static class Beehive_IncreseLevel_Patch
		{
			private static bool Prefix(Beehive __instance) 
			{
				var extension = __instance.GetComponent<BeehiveExtension>();
				if (extension.IsConnected)
				{
					int currentHoney = __instance.GetHoneyLevel();
					if (currentHoney > 0)
					{
						currentHoney += 1;
						currentHoney = Mathf.Clamp(currentHoney, 0, __instance.m_maxHoney);
						__instance.m_nview.GetZDO().Set(ZDOVars.s_level, 0, false);

						ItemDrop.ItemData itemData = __instance.m_honeyItem.m_itemData.Clone();
						itemData.m_stack = currentHoney;
						extension.m_container.m_inventory.AddItem(itemData);
					}
					else
					{
						ItemDrop.ItemData itemData = __instance.m_honeyItem.m_itemData.Clone();
						itemData.m_stack = 1;
						extension.m_container.m_inventory.AddItem(itemData);
					}

					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(Beehive), "GetHoverText")]
		public static class Beehive_GetHoverText_Patch
		{
			private static bool Prefix(Beehive __instance, ref string __result)
			{
				var extension = __instance.GetComponent<BeehiveExtension>();
				if (extension.IsConnected)
				{
					__result = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Beehive Output");
					return false;
				}

				return true;
			}
		}
	}
}
