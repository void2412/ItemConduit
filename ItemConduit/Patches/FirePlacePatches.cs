using HarmonyLib;
using ItemConduit.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace ItemConduit.Patches
{
	public static class FirePlacePatches
	{
		[HarmonyPatch(typeof(Fireplace), "Awake")]
		public static class Fireplace_Awake_Patch
		{
			private static void Postfix(Fireplace __instance)
			{
				if (__instance.GetComponent<FirePlaceExtention>() == null)
				{
					__instance.gameObject.AddComponent<FirePlaceExtention>();
				}
			}
		}
	}
}
