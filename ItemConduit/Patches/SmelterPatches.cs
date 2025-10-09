using HarmonyLib;
using ItemConduit.Debug;
using ItemConduit.Events;
using ItemConduit.Extensions;
using ItemConduit.Interfaces;
using System;
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

				IContainerInterface @interface = __instance.gameObject.GetComponent<IContainerInterface>();
				ContainerEventManager.Instance.NotifyContainerPlaced(@interface);
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

				if (!extension.isConnected)
				{
					if (extension.oreProcessedList.Count > 0)
					{
						foreach (var item in extension.oreProcessedList)
						{
							__instance.Spawn(item.Key, item.Value);
							extension.oreProcessedList.Remove(item.Key);
						}
					}
				}

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
								__instance.RemoveOneOre();
								if (!extension.isConnected)
								{
									__instance.QueueProcessed(queuedOre);
								}
								else
								{
									if (extension.oreProcessedList.ContainsKey(queuedOre))
									{
										extension.oreProcessedList[queuedOre] += 1;
									}
									else
									{
										extension.oreProcessedList.Add(queuedOre, 1);
									}

								}

							}
						}
					}
				}
				if (!extension.isConnected)
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


		[HarmonyPatch(typeof(ZNetScene), "Destroy")]
		public static class ZNetScene_Destroy_Patch
		{
			private static void Prefix(GameObject go)
			{
				if (go != null)
				{
					Smelter container = go.GetComponent<Smelter>();
					if (container != null)
					{
						IContainerInterface @interface = container.gameObject.GetComponent<IContainerInterface>();
						ContainerEventManager.Instance.NotifyContainerRemoved(@interface);
					}
				}
			}
		}

		[HarmonyPatch(typeof(Piece), "DropResources")]
		public static class Piece_DropResources_Patch_Container
		{
			private static void Prefix(Piece __instance)
			{
				if (__instance != null)
				{
					Smelter container = __instance.GetComponent<Smelter>();
					if (container != null)
					{
						IContainerInterface @interface = container.gameObject.GetComponent<IContainerInterface>();
						ContainerEventManager.Instance.NotifyContainerRemoved(@interface);
					}
				}
			}
		}

		[HarmonyPatch(typeof(WearNTear), "Destroy")]
		public static class WearNTear_Destroy_Patch
		{
			private static void Prefix(WearNTear __instance)
			{
				if (__instance != null)
				{
					Smelter container = __instance.GetComponent<Smelter>();
					if (container != null)
					{
						IContainerInterface @interface = container.gameObject.GetComponent<IContainerInterface>();
						ContainerEventManager.Instance.NotifyContainerRemoved(@interface);
					}
				}
			}
		}

	}
}
