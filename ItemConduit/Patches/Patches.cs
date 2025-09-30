using HarmonyLib;
using ItemConduit.Core;
using ItemConduit.Debug;
using ItemConduit.Network;
using ItemConduit.Nodes;
using ItemConduit.Config;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Patches
{
	/// <summary>
	/// Optimized Harmony patches for ItemConduit
	/// Prevents ghost/preview pieces from triggering rebuilds
	/// </summary>
	public static class HarmonyPatches
	{
		/// <summary>
		/// Patch for when a player places a piece
		/// Only triggers rebuild for successfully placed pieces (not ghosts)
		/// </summary>
		[HarmonyPatch(typeof(Player), "PlacePiece")]
		public static class Player_PlacePiece_Patch
		{
			/// <summary>
			/// After a piece is placed, check if it's a valid conduit node
			/// Note: PlacePiece is void, so we check validity by examining the piece state
			/// </summary>
			private static void Postfix(Player __instance, Piece piece)
			{
				if (__instance == null || piece == null) return;
				if (ZNet.instance == null) return;

				// Only process on server
				if (!ZNet.instance.IsServer()) return;

				// Check if the placed piece has a node component
				BaseNode node = piece.GetComponent<BaseNode>();
				if (node != null)
				{
					// Verify this is a real placed piece by checking ZNetView validity
					// A successfully placed piece will have a valid ZNetView
					// Ghost/preview pieces will not
					ZNetView znet = piece.GetComponent<ZNetView>();
					if (znet != null && znet.IsValid())
					{
						// Also verify the piece isn't marked for destruction
						if (!piece.IsPlacedByPlayer())
						{
							if (DebugConfig.showDebug.Value)
							{
								Logger.LogInfo($"[ItemConduit] Piece not placed by player: {piece.name}");
							}
							return;
						}

						// Small delay to ensure the node is fully initialized
						__instance.StartCoroutine(DelayedRebuildRequest(node, 0.5f));

						if (DebugConfig.showDebug.Value)
						{
							Logger.LogInfo($"[ItemConduit] Successfully placed conduit node: {piece.name}");
						}
					}
					else
					{
						if (DebugConfig.showDebug.Value)
						{
							Logger.LogInfo($"[ItemConduit] Ignoring invalid piece (likely ghost): {piece.name}");
						}
					}
				}
			}

			/// <summary>
			/// Coroutine to delay rebuild request
			/// </summary>
			private static System.Collections.IEnumerator DelayedRebuildRequest(BaseNode node, float delay)
			{
				yield return new WaitForSeconds(delay);

				// Verify node is still valid and placed
				if (node != null && node.IsValidPlacedNode())
				{
					RebuildManager.Instance.RequestRebuildForNode(node, true);
				}
			}
		}

		/// <summary>
		/// Patch for when a piece is removed/destroyed
		/// Handles cleanup and network rebuild
		/// </summary>
		[HarmonyPatch(typeof(Piece), "DropResources")]
		public static class Piece_DropResources_Patch
		{
			/// <summary>
			/// Before a piece drops resources (is being destroyed)
			/// </summary>
			private static void Prefix(Piece __instance)
			{
				if (__instance == null) return;
				if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

				// Check if this is a conduit node
				BaseNode node = __instance.GetComponent<BaseNode>();
				if (node != null && node.IsValidPlacedNode())
				{
					// Get connected nodes before destruction
					var connectedNodes = node.GetConnectedNodes();

					// Request rebuild for connected nodes after this one is destroyed
					foreach (var connectedNode in connectedNodes)
					{
						if (connectedNode != null && connectedNode.IsValidPlacedNode())
						{
							RebuildManager.Instance.RequestRebuildForNode(connectedNode);
						}
					}

					if (DebugConfig.showDebug.Value)
					{
						Logger.LogInfo($"[ItemConduit] Conduit node being destroyed: {__instance.name}");
					}
				}
			}
		}

		/// <summary>
		/// Patch to prevent ghost pieces from being processed
		/// </summary>
		[HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
		public static class Player_UpdatePlacementGhost_Patch
		{
			/// <summary>
			/// Mark placement ghost pieces so they can be ignored
			/// </summary>
			private static void Postfix(Player __instance)
			{
				if (__instance.m_placementGhost != null)
				{
					// Check if the ghost has a node component
					BaseNode node = __instance.m_placementGhost.GetComponent<BaseNode>();
					if (node != null)
					{
						// The ghost detection is already handled in BaseNode.CheckIfGhost()
						// This is here as a safeguard
						if (DebugConfig.showDebug.Value)
						{
							// Don't spam log for ghosts
							// Logger.LogInfo($"[ItemConduit] Ghost piece detected in placement");
						}
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

					// Don't request initial rebuild - let nodes register themselves
					Logger.LogInfo("[ItemConduit] Network manager initialized on server start");

					ContainerWireframeManager.Instance.InitializeExistingContainers();
				}
			}
		}

		/// <summary>
		/// Patch for world loading
		/// Ensures proper initialization after world load
		/// </summary>
		[HarmonyPatch(typeof(ZNetScene), "Awake")]
		public static class ZNetScene_Awake_Patch
		{
			/// <summary>
			/// After ZNetScene is created
			/// </summary>
			private static void Postfix(ZNetScene __instance)
			{
				if (ZNet.instance != null && ZNet.instance.IsServer())
				{
					// Ensure managers are initialized
					var networkManager = NetworkManager.Instance;
					var rebuildManager = RebuildManager.Instance;

					Logger.LogInfo("[ItemConduit] Managers initialized in ZNetScene");
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
				RebuildManager.Instance?.CancelPendingRebuilds();

				Logger.LogInfo("[ItemConduit] Network manager shutdown");
			}
		}

		
		[HarmonyPatch(typeof(Terminal), "InitTerminal")]
		public static class Terminal_InitTerminal_Patch
		{
			private static void Postfix()
			{
				Terminal.ConsoleCommand nodeWireframeCmd = new Terminal.ConsoleCommand(
					"conduit_wireframe",
					"Toggle conduit wireframe visualization",
					delegate (Terminal.ConsoleEventArgs args)
					{
						VisualConfig.nodeWireframe.Value = !VisualConfig.nodeWireframe.Value;
						UpdateAllNodeVisualizations();
						args.Context.AddString($"Node wireframe visualization: {(VisualConfig.nodeWireframe.Value ? "ON" : "OFF")}");
					}
				);

				Terminal.ConsoleCommand ContainerWireframeCmd = new Terminal.ConsoleCommand(
					"container_wireframe",
					"Toggle container wireframe visualization",
					delegate (Terminal.ConsoleEventArgs args)
					{
						VisualConfig.containerWireframe.Value = !VisualConfig.containerWireframe.Value;
						ContainerWireframeManager.Instance.SetWireframesVisible(VisualConfig.containerWireframe.Value);
						args.Context.AddString($"Container wireframes: {(VisualConfig.containerWireframe.Value ? "ON" : "OFF")}");
					}
				);

				Terminal.ConsoleCommand snappointSphereCmd = new Terminal.ConsoleCommand(
					"conduit_snappoint",
					"Toggle conduit snappoint visualization",
					delegate (Terminal.ConsoleEventArgs args)
					{
						VisualConfig.snappointSphere.Value = !VisualConfig.snappointSphere.Value;
						UpdateAllNodeVisualizations();
						args.Context.AddString($"Node snappoint visualization: {(VisualConfig.snappointSphere.Value ? "ON" : "OFF")}");
					}
				);
			}

			private static void UpdateAllNodeVisualizations()
			{
				var allNodes = UnityEngine.Object.FindObjectsOfType<BaseNode>();
				foreach (var node in allNodes)
				{
					if (node.TryGetComponent<BoundsVisualizer>(out var viz))
					{
						viz.SetVisible(VisualConfig.nodeWireframe.Value);
					}
					else if (VisualConfig.nodeWireframe.Value)
					{
						node.SendMessage("Initialize node wireframe visualization", SendMessageOptions.DontRequireReceiver);
					}

					if (node.TryGetComponent<SnapConnectionVisualizer>(out var snap))
					{
						snap.SetVisible(VisualConfig.snappointSphere.Value);
					}
					else if (VisualConfig.snappointSphere.Value)
					{
						node.SendMessage("Initialize node snappoint visualization", SendMessageOptions.DontRequireReceiver);
					}

				}

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
					if (DebugConfig.showDebug.Value)
					{
						Logger.LogInfo($"[ItemConduit] New client connected, will sync network state to peer {peer.m_uid}");
					}

					// The node states will be synced automatically via ZDO system
					// But we can trigger a refresh to ensure everything is up to date
					RebuildManager.Instance.RequestFullRebuild();
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

				// Use physics overlap for efficiency
				Collider[] colliders = Physics.OverlapSphere(
					__instance.transform.position,
					2f,
					LayerMask.GetMask("piece", "piece_nonsolid")
				);

				foreach (var col in colliders)
				{
					if (col.GetComponent<ExtractNode>() != null ||
						col.GetComponent<InsertNode>() != null)
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

		/// <summary>
		/// Patch to prevent placement effects from triggering during ghost preview
		/// </summary>
		[HarmonyPatch(typeof(Piece), "SetCreator")]
		public static class Piece_SetCreator_Patch
		{
			/// <summary>
			/// Only set creator for actually placed pieces
			/// </summary>
			private static bool Prefix(Piece __instance)
			{
				// Check if this is a node
				BaseNode node = __instance.GetComponent<BaseNode>();
				if (node != null)
				{
					// Check if it's a valid placed piece
					if (!node.IsValidPlacedNode())
					{
						return false; // Skip setting creator for ghosts
					}
				}
				return true;
			}
		}

		#endregion
	}
}