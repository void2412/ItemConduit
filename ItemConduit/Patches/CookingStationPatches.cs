using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemConduit.Patches
{
	public static class CookingStationPatches
	{
		[HarmonyPatch(typeof(CookingStation), "Awake")]
		public static class CookingStation_Awake_Patch
		{
			private static void Postfix(CookingStation __instance)
			{
				if (__instance.GetComponent<CookingStationExtension>() == null)
				{
					__instance.gameObject.AddComponent<CookingStationExtension>();
				}
			}
		}


	}
}
