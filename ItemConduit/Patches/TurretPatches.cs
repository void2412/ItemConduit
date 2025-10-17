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
	public static class TurretPatches
	{
		[HarmonyPatch(typeof(Turret), "Awake")]
		public static class Turret_Awake_Patch
		{
			private static void Postfix(Turret __instance)
			{
				if (__instance.GetComponent<TurretExtension>() == null)
				{
					__instance.gameObject.AddComponent<TurretExtension>();
				}
			}
		}
	}
}