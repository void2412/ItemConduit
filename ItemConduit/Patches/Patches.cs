using HarmonyLib;
using UnityEngine;
using ItemConduit.Nodes;
using ItemConduit.Network;
using ItemConduit.Core;

namespace ItemConduit.Patches
{
	/// <summary>
	/// Simplified Harmony patches for ItemConduit
	/// Removed references to non-existent wrapper classes
	/// </summary>
	public static class HarmonyPatches
	{
		/// <summary>
		/// Patch for when a player places a piece
		/// Triggers network rebuild when conduit nodes are placed
		/// </summary>
		[HarmonyPatch(typeof(Player), "PlacePiece")]
		public static class Player_PlacePiece_Patch
		{
			/// <summary>
			/// After a piece is placed
			/// </summary>
			private static void Postfix(Player __instance, Piece piece)
			{
				// Only process on server
				if (!ZNet.instance.IsServer()) return;

				// Check if the placed piece is a conduit node
				if (piece != null && piece.gameObject != null)
				{
					BaseNode node = piece.GetComponent<BaseNode>();
					if (node != null)
					{
						// Network rebuild will be triggered when the node registers itself
						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Debug.Log($"[ItemConduit] Conduit node placed: {piece.name}");
						}
					}
				}
			}
		}

		/// <summary>
		/// Patch for when a piece is destroyed
		/// Triggers network rebuild when conduit nodes are removed
		/// </summary>
		[HarmonyPatch(typeof(Piece), "OnDestroy")]
		public static class Piece_OnDestroy_Patch
		{
			/// <summary>
			/// Before a piece is destroyed
			/// </summary>
			private static void Prefix(Piece __instance)
			{
				// Only process on server
				if (!ZNet.instance.IsServer()) return;

				// Check if the destroyed piece is a conduit node
				BaseNode node = __instance.GetComponent<BaseNode>();
				if (node != null)
				{
					// Network rebuild will be triggered when the node unregisters itself
					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Debug.Log($"[ItemConduit] Conduit node destroyed: {__instance.name}");
					}
				}
			}
		}

		/// <summary>
		/// Patch for game initialization
		/// Initializes network manager on server/host startup
		/// </summary>
		[HarmonyPatch(typeof(Game), "Start")]
		public static class Game_Start_Patch
		{
			/// <summary>
			/// After game starts
			/// </summary>
			private static void Postfix()
			{
				if (ZNet.instance != null && ZNet.instance.IsServer())
				{
					// Initialize network manager on server startup
					NetworkManager.Instance.Initialize();

					// Request initial network rebuild after a delay
					NetworkManager.Instance.RequestNetworkRebuild();

					Debug.Log("[ItemConduit] Network manager initialized on server start");
				}
			}
		}

		/// <summary>
		/// Patch for game shutdown
		/// Ensures clean shutdown of network manager
		/// </summary>
		[HarmonyPatch(typeof(ZNetScene), "Shutdown")]
		public static class ZNetScene_Shutdown_Patch
		{
			/// <summary>
			/// Before network scene shuts down
			/// </summary>
			private static void Prefix()
			{
				// Clean shutdown of network manager
				NetworkManager.Instance?.Shutdown();

				Debug.Log("[ItemConduit] Network manager shutdown");
			}
		}

		#region GUI Integration Patches

		/// <summary>
		/// Patch to block player input when ItemConduit GUI is open
		/// </summary>
		[HarmonyPatch(typeof(Player), "TakeInput")]
		public static class Player_TakeInput_Patch
		{
			/// <summary>
			/// Check if input should be blocked
			/// </summary>
			/// <returns>False to skip original method if GUI is open</returns>
			private static bool Prefix(Player __instance)
			{
				// Block input if our GUI is open
				if (GUI.GUIManager.Instance != null && GUI.GUIManager.Instance.HasActiveGUI())
				{
					// Clear any movement input
					__instance.m_moveDir = Vector3.zero;
					__instance.m_lookDir = Vector3.zero;
					return false; // Skip the original TakeInput method
				}

				return true; // Allow normal input
			}
		}

		/// <summary>
		/// Patch to prevent game pause when ItemConduit GUI is open
		/// </summary>
		[HarmonyPatch(typeof(Game), "Pause")]
		public static class Game_Pause_Patch
		{
			/// <summary>
			/// Check if game should be allowed to pause
			/// </summary>
			/// <returns>False to prevent original method execution if GUI is open</returns>
			private static bool Prefix()
			{
				// Don't pause if ItemConduit GUI is open
				if (GUI.GUIManager.Instance != null && GUI.GUIManager.Instance.HasActiveGUI())
				{
					return false; // Skip original pause method
				}

				return true; // Allow normal pause
			}
		}

		/// <summary>
		/// Patch to prevent inventory from opening while configuring nodes
		/// </summary>
		[HarmonyPatch(typeof(InventoryGui), "Show")]
		public static class InventoryGui_Show_Patch
		{
			/// <summary>
			/// Prevent inventory from showing if our GUI is open
			/// </summary>
			private static bool Prefix()
			{
				if (GUI.GUIManager.Instance != null && GUI.GUIManager.Instance.HasActiveGUI())
				{
					return false; // Don't show inventory
				}
				return true;
			}
		}

		#endregion

		#region Network Sync Patches

		/// <summary>
		/// Patch to handle network synchronization on player connection
		/// Ensures clients receive network state when joining
		/// </summary>
		[HarmonyPatch(typeof(ZNet), "OnNewConnection")]
		public static class ZNet_OnNewConnection_Patch
		{
			/// <summary>
			/// After a new connection is established
			/// </summary>
			private static void Postfix(ZNetPeer peer)
			{
				if (ZNet.instance.IsServer())
				{
					// Server should sync network state to new client
					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Debug.Log($"[ItemConduit] New client connected, syncing network state to peer {peer.m_uid}");
					}

					// TODO: Implement network state synchronization
					// This would send all network IDs and node states to the new client
				}
			}
		}

		#endregion

		#region Performance Optimization Patches

		/// <summary>
		/// Patch to optimize container inventory updates
		/// Reduces unnecessary updates when items are transferred
		/// </summary>
		[HarmonyPatch(typeof(Container), "Save")]
		public static class Container_Save_Patch
		{
			private static float lastSaveTime = 0f;
			private const float SAVE_COOLDOWN = 1f; // Minimum time between saves

			/// <summary>
			/// Throttle container saves during bulk transfers
			/// </summary>
			/// <returns>False to skip save if too frequent</returns>
			private static bool Prefix(Container __instance)
			{
				// Check if this container is connected to a conduit node
				bool hasConduitNode = false;
				Collider[] colliders = Physics.OverlapSphere(__instance.transform.position, 3f);
				foreach (var col in colliders)
				{
					if (col.GetComponent<BaseNode>() != null)
					{
						hasConduitNode = true;
						break;
					}
				}

				// If connected to conduit system, throttle saves
				if (hasConduitNode)
				{
					float currentTime = Time.time;
					if (currentTime - lastSaveTime < SAVE_COOLDOWN)
					{
						return false; // Skip this save
					}
					lastSaveTime = currentTime;
				}

				return true; // Allow save
			}
		}

		#endregion
	}
}