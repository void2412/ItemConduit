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

		[HarmonyPatch(typeof(Fireplace), "Interact")]
		public static class Fireplace_Interact_Patch
		{
			private static bool Prefix(Fireplace __instance, Humanoid user, bool hold)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null || !extension.IsConnected) return true;

				// Check if Shift is being held
				bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

				if (shiftHeld && !hold && extension.IsConnected)
				{
					// Open the container inventory when Shift+E is pressed
					InventoryGui.instance.Show(extension.m_container, 1);
					return false; // Prevent normal cooking station interaction
				}

				// Allow normal cooking station interaction when Shift is not held
				return true;
			}
		}

		[HarmonyPatch(typeof(Fireplace), "GetHoverText")]
		public static class Fireplace_GetHoverText_Patch
		{
			private static void Postfix(Fireplace __instance, ref string __result)
			{
				var extension = __instance.GetComponent<FirePlaceExtention>();
				if (extension == null || !extension.IsConnected) return;

				// Always show the Shift+E option when connected
				__result += Localization.instance.Localize("\n[<color=yellow><b>Shift + $KEY_Use</b></color>] Open Inventory");
			}
		}
	}
}
