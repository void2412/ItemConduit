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

	}
}
