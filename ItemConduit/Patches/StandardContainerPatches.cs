using HarmonyLib;
using ItemConduit.Debug;
using ItemConduit.Extensions;
using UnityEngine;

namespace ItemConduit.Patches
{
	/// <summary>
	/// Patches to add extensions and wireframe visualization to containers
	/// </summary>
	public static class StandardContainerPatches
	{
		/// <summary>
		/// Add extension and wireframe when container awakens
		/// </summary>
		[HarmonyPatch(typeof(Container), "Awake")]
		public static class Container_Awake_Patch
		{
			private static void Postfix(Container __instance)
			{
				// Add the extension if it doesn't exist
				if (__instance.GetComponent<StandardContainerExtension>() == null)
				{
					__instance.gameObject.AddComponent<StandardContainerExtension>();
				}
			}
		}

		[HarmonyPatch(typeof(Container), "GetHoverText")]
		public static class Container_GetHoverText_Patch
		{
			private static bool Prefix(Container __instance, ref string __result)
			{
				SmelteryExtension smelterExt = __instance.GetComponentInParent<SmelteryExtension>();
				if (smelterExt != null && smelterExt.m_container == __instance)
				{
					__result = "";
					return false;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(Container), "Interact")]
		public static class Container_Interact_Patch
		{
			private static bool Prefix(Container __instance)
			{
				SmelteryExtension smelterExt = __instance.GetComponentInParent<SmelteryExtension>();
				if (smelterExt != null && smelterExt.m_container == __instance)
				{
					return false;
				}

				return true;
			}
		}
	}

}