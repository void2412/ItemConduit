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
	public static class SmelterPatches
	{
		[HarmonyPatch(typeof(Smelter), "Awake")]
		public static class Smelter_Awake_Patch
		{
			private static void Postfix(Smelter __instance)
			{
				if (__instance.GetComponent<SmelteryExtension>() == null)
				{
					__instance.gameObject.AddComponent<SmelteryExtension>();
				}
			}
		}

		[HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
		public static class Smelter_UpdateSmelter_Patch
		{
			private static bool Prefix(Smelter __instance)
			{
				if (!__instance.m_nview.IsValid())
				{
					return false;
				}
				__instance.UpdateRoof();
				__instance.UpdateSmoke();
				__instance.UpdateState();
				if (!__instance.m_nview.IsOwner())
				{
					return false;
				}

				var extension = __instance.GetComponent<SmelteryExtension>();

				double deltaTime = __instance.GetDeltaTime();
				float num = __instance.GetAccumulator();
				num += (float)deltaTime;
				if (num > 3600f)
				{
					num = 3600f;
				}
				float num2 = (__instance.m_windmill ? __instance.m_windmill.GetPowerOutput() : 1f);
				while (num >= 1f)
				{
					num -= 1f;
					float num3 = __instance.GetFuel();
					string queuedOre = __instance.GetQueuedOre();
					if ((__instance.m_maxFuel == 0 || num3 > 0f) && (__instance.m_maxOre == 0 || queuedOre != "") && __instance.m_secPerProduct > 0f && (!__instance.m_requiresRoof || __instance.m_haveRoof) && !__instance.m_blockedSmoke)
					{
						float num4 = 1f * num2;
						if (__instance.m_maxFuel > 0)
						{
							float num5 = __instance.m_secPerProduct / (float)__instance.m_fuelPerProduct;
							num3 -= num4 / num5;
							if (num3 < 0.0001f)
							{
								num3 = 0f;
							}
							__instance.SetFuel(num3);
						}
						if (queuedOre != "")
						{
							float num6 = __instance.GetBakeTimer();
							num6 += num4;
							__instance.SetBakeTimer(num6);
							if (num6 >= __instance.m_secPerProduct)
							{
								
								__instance.SetBakeTimer(0f);
								
								if (!extension.IsConnected)
								{
									__instance.RemoveOneOre();
									__instance.QueueProcessed(queuedOre);
								}
								else
								{
									Smelter.ItemConversion itemConversion = __instance.GetItemConversion(queuedOre);
									ItemDrop ore = Object.Instantiate<GameObject>(itemConversion.m_to.gameObject, __instance.m_outputPoint.position,
										__instance.m_outputPoint.rotation).GetComponent<ItemDrop>();

									ItemDrop.ItemData itemData = ore.m_itemData;
									if(extension.AddToInventory(itemData, 1))
									{
										__instance.RemoveOneOre();
									}

								}

							}
						}
					}
				}
				if (!extension.IsConnected)
				{
					if (__instance.GetQueuedOre() == "" || ((float)__instance.m_maxFuel > 0f && __instance.GetFuel() == 0f))
					{
						__instance.SpawnProcessed();
					}
				}
				
					__instance.SetAccumulator(num);
				return false;
			}
		}


	}
}
