using HarmonyLib;
using ItemConduit.Debug;
using ItemConduit.Extensions;
using System.Runtime.Remoting.Messaging;
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
				if (isExtensionContainer(__instance))
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
				if (isExtensionContainer(__instance)) return false;

				return true;
			}

		}

		private static bool isExtensionContainer(Container __instance)
		{
			SmelteryExtension smelterExt = __instance.GetComponentInParent<SmelteryExtension>();
			BeehiveExtension beehiveExt = __instance.GetComponentInParent<BeehiveExtension>();
			SapCollectorExtention sapExt = __instance.GetComponentInParent<SapCollectorExtention>();
			FermenterExtension fermenterExt = __instance.GetComponentInParent<FermenterExtension>();

			return (smelterExt != null && smelterExt.m_container == __instance) ||
		   (beehiveExt != null && beehiveExt.m_container == __instance) ||
		   (sapExt != null && sapExt.m_container == __instance) ||
		   (fermenterExt != null && fermenterExt.m_container == __instance);
		}
	}
}