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

	}

}