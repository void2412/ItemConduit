using HarmonyLib;
using ItemConduit.Debug;
using ItemConduit.Events;
using UnityEngine;

namespace ItemConduit.Patches
{
	/// <summary>
	/// Patches to add wireframe visualization to containers
	/// </summary>
	public static class ContainerPatches
	{
		/// <summary>
		/// Add wireframe when container awakens
		/// </summary>
		[HarmonyPatch(typeof(Container), "Awake")]
		public static class Container_Awake_Patch
		{
			private static void Postfix(Container __instance)
			{
				// Register container for wireframe visualization
				ContainerWireframeManager.Instance.RegisterContainer(__instance);

				ContainerEventManager.Instance.NotifyContainerPlaced(__instance);
			}
		}

		/// <summary>
		/// Track when containers are destroyed using ZNetView destruction
		/// </summary>
		[HarmonyPatch(typeof(ZNetScene), "Destroy")]
		public static class ZNetScene_Destroy_Patch
		{
			private static void Prefix(GameObject go)
			{
				if (go != null)
				{
					Container container = go.GetComponent<Container>();
					if (container != null)
					{
						ContainerWireframeManager.Instance.UnregisterContainer(container);
						ContainerEventManager.Instance.NotifyContainerRemoved(container);
					}
				}
			}
		}

		/// <summary>
		/// Alternative: Track when pieces are removed if container is a buildable piece
		/// </summary>
		[HarmonyPatch(typeof(Piece), "DropResources")]
		public static class Piece_DropResources_Patch_Container
		{
			private static void Prefix(Piece __instance)
			{
				if (__instance != null)
				{
					Container container = __instance.GetComponent<Container>();
					if (container != null)
					{
						ContainerWireframeManager.Instance.UnregisterContainer(container);
						ContainerEventManager.Instance.NotifyContainerRemoved(container);
					}
				}
			}
		}

		/// <summary>
		/// Additional patch for container destruction via damage
		/// </summary>
		[HarmonyPatch(typeof(WearNTear), "Destroy")]
		public static class WearNTear_Destroy_Patch
		{
			private static void Prefix(WearNTear __instance)
			{
				if (__instance != null)
				{
					Container container = __instance.GetComponent<Container>();
					if (container != null)
					{
						ContainerEventManager.Instance.NotifyContainerRemoved(container);
					}
				}
			}
		}
	}
}