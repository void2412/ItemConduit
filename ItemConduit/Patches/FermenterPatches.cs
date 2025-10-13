using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemConduit.Patches
{
	public static class FermenterPatches
	{
		[HarmonyPatch(typeof(Fermenter), "Awake")]
		public static class Fermenter_Awake_Patch
		{
			private static void Postfix(Fermenter __instance)
			{
				if (__instance.GetComponent<FermenterExtension>() == null)
				{
					__instance.gameObject.AddComponent<FermenterExtension>();
				}
			}
		}

		[HarmonyPatch(typeof(Fermenter), "GetHoverText")]
		public static class Fermenter_GetHoverText_Patch
		{
			private static bool Prefix(Fermenter __instance, ref string __result)
			{
				var extension = __instance.GetComponent<FermenterExtension>();
				if (extension.IsConnected)
				{
					__result = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Fermenter Output");
					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(Fermenter), "Interact")]
		public static class Fermenter_Interact_Patch
		{
			private static bool Prefix(Fermenter __instance)
			{
				var extension = __instance.GetComponent<FermenterExtension>();
				if (extension == null) return true;

				if (extension.IsConnected)
				{
					InventoryGui.instance.Show(extension.m_container, 1);
					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(Fermenter), "SlowUpdate")]
		public static class Fermenter_SlowUpdate_Patch
		{
			private static bool Prefix(Fermenter __instance)
			{
				var extension = __instance.GetComponent<FermenterExtension>();
				if (extension == null) return true;


				if (extension.IsConnected)
				{
					__instance.UpdateCover(2f, false);
					switch (__instance.GetStatus())
					{
						case Fermenter.Status.Empty:
							__instance.m_fermentingObject.SetActive(false);
							__instance.m_readyObject.SetActive(false);
							__instance.m_topObject.SetActive(false);
							return false;
						case Fermenter.Status.Fermenting:
							__instance.m_readyObject.SetActive(false);
							__instance.m_topObject.SetActive(true);
							__instance.m_fermentingObject.SetActive(!__instance.m_exposed && __instance.m_hasRoof);
							return false;
						case Fermenter.Status.Exposed:
							break;
						case Fermenter.Status.Ready:
							__instance.m_fermentingObject.SetActive(false);

							Fermenter.ItemConversion itemConversion = __instance.GetItemConversion(__instance.m_delayedTapItem);
							ItemDrop itemDropPrefab = itemConversion.m_to.gameObject.GetComponent<ItemDrop>();

							ItemDrop.ItemData itemData = itemDropPrefab.m_itemData.Clone();
							itemData.m_dropPrefab = itemDropPrefab.gameObject;
							itemData.m_stack = itemConversion.m_producedItems;

							if (extension.AddToInventory(itemData))
							{
								__instance.m_nview.GetZDO().Set(ZDOVars.s_content, "");
								__instance.m_nview.GetZDO().Set(ZDOVars.s_startTime, 0, false);
							}
								
							return false;
						default:
							return false;
					}
				}

				return true;
			}
		}
	}
}
