using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.Nodes;
using Logger = Jotunn.Logger;

namespace ItemConduit.Network
{
	/// <summary>
	/// Simplified Network Manager for ItemConduit
	/// Manages node networks and item transfers
	/// </summary>
	public class NetworkManager : MonoBehaviour
	{
		#region Singleton

		private static NetworkManager _instance;
		public static NetworkManager Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_NetworkManager");
					_instance = go.AddComponent<NetworkManager>();
					DontDestroyOnLoad(go);

					// Initialize immediately
					_instance.InitializeFields();
				}
				return _instance;
			}
		}

		#endregion

		#region Fields

		/// <summary>All active networks indexed by ID</summary>
		private Dictionary<string, ConduitNetwork> networks;

		/// <summary>All registered nodes in the system</summary>
		private HashSet<BaseNode> allNodes;

		/// <summary>Map of nodes to their current network IDs for quick lookup</summary>
		private Dictionary<BaseNode, string> nodeNetworkMap;

		/// <summary>Coroutine reference for transfer loop</summary>
		private Coroutine transferCoroutine;

		/// <summary>Reference to the rebuild manager</summary>
		private RebuildManager rebuildManager;

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize fields to prevent null references
		/// </summary>
		private void InitializeFields()
		{
			if (networks == null)
				networks = new Dictionary<string, ConduitNetwork>();

			if (allNodes == null)
				allNodes = new HashSet<BaseNode>();

			if (nodeNetworkMap == null)
				nodeNetworkMap = new Dictionary<BaseNode, string>();

			// Initialize rebuild manager
			rebuildManager = RebuildManager.Instance;

			Logger.LogInfo("[ItemConduit] NetworkManager fields initialized");
		}

		/// <summary>
		/// Initialize the network manager
		/// </summary>
		public void Initialize()
		{
			// Ensure fields are initialized
			InitializeFields();

			// Stop any existing coroutine
			if (transferCoroutine != null)
			{
				StopCoroutine(transferCoroutine);
				transferCoroutine = null;
			}

			// Start transfer loop only if we're the server
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				transferCoroutine = StartCoroutine(TransferLoop());
				Logger.LogInfo("[ItemConduit] NetworkManager transfer loop started");
			}
			else
			{
				Logger.LogInfo("[ItemConduit] NetworkManager initialized (client mode - no transfers)");
			}
		}

		/// <summary>
		/// Ensure singleton is created
		/// </summary>
		void Awake()
		{
			if (_instance == null)
			{
				_instance = this;
				DontDestroyOnLoad(gameObject);
				InitializeFields();
			}
			else if (_instance != this)
			{
				Destroy(gameObject);
			}
		}

		/// <summary>
		/// Shutdown the network manager
		/// </summary>
		public void Shutdown()
		{
			if (transferCoroutine != null)
			{
				StopCoroutine(transferCoroutine);
				transferCoroutine = null;
			}

			// Cancel any pending rebuilds
			if (rebuildManager != null)
			{
				rebuildManager.CancelPendingRebuilds();
			}

			// Clear all data
			if (networks != null)
				networks.Clear();

			if (allNodes != null)
				allNodes.Clear();

			if (nodeNetworkMap != null)
				nodeNetworkMap.Clear();

			Logger.LogInfo("[ItemConduit] NetworkManager shutdown complete");
		}

		#endregion

		#region Node Registration

		/// <summary>
		/// Register a node with the network manager
		/// </summary>
		public void RegisterNode(BaseNode node)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			// Ensure collections exist
			if (allNodes == null)
				allNodes = new HashSet<BaseNode>();

			if (allNodes.Add(node))
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Registered node: {node.name} (Total: {allNodes.Count})");
				}

				// Request rebuild through RebuildManager
				rebuildManager.RequestRebuildForNode(node);
			}
		}

		/// <summary>
		/// Unregister a node from the network manager
		/// </summary>
		public void UnregisterNode(BaseNode node)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			// Store connected nodes before removing
			List<BaseNode> connectedNodes = node.GetConnectedNodes();

			if (allNodes != null && allNodes.Remove(node))
			{
				// Remove from network map
				if (nodeNetworkMap != null)
					nodeNetworkMap.Remove(node);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Unregistered node: {node.name} (Remaining: {allNodes.Count})");
				}

				// Request rebuild for connected nodes
				foreach (var connectedNode in connectedNodes)
				{
					if (connectedNode != null && allNodes.Contains(connectedNode))
					{
						rebuildManager.RequestRebuildForNode(connectedNode);
					}
				}
			}
		}

		#endregion

		#region Network Management

		/// <summary>
		/// Register a new network
		/// </summary>
		public void RegisterNetwork(ConduitNetwork network)
		{
			if (network == null || string.IsNullOrEmpty(network.NetworkId)) return;

			networks[network.NetworkId] = network;

			// Update node-network mapping
			foreach (var node in network.Nodes)
			{
				if (node != null)
				{
					nodeNetworkMap[node] = network.NetworkId;
					node.SetNetworkId(network.NetworkId);
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Registered network {network.NetworkId.Substring(0, 8)} with {network.Nodes.Count} nodes");
			}
		}

		/// <summary>
		/// Remove a network
		/// </summary>
		public void RemoveNetwork(string networkId)
		{
			if (string.IsNullOrEmpty(networkId)) return;

			if (networks.ContainsKey(networkId))
			{
				var network = networks[networkId];

				// Clear network ID from nodes
				foreach (var node in network.Nodes)
				{
					if (node != null)
					{
						nodeNetworkMap.Remove(node);
					}
				}

				networks.Remove(networkId);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Removed network {networkId.Substring(0, 8)}");
				}
			}
		}

		/// <summary>
		/// Get the network ID for a specific node
		/// </summary>
		public string GetNodeNetworkId(BaseNode node)
		{
			if (node == null) return null;
			return nodeNetworkMap.ContainsKey(node) ? nodeNetworkMap[node] : null;
		}

		/// <summary>
		/// Get all nodes in a specific network
		/// </summary>
		public List<BaseNode> GetNetworkNodes(string networkId)
		{
			if (string.IsNullOrEmpty(networkId) || !networks.ContainsKey(networkId))
				return new List<BaseNode>();

			return networks[networkId].Nodes.ToList();
		}

		/// <summary>
		/// Get all registered nodes
		/// </summary>
		public HashSet<BaseNode> GetAllNodes()
		{
			return new HashSet<BaseNode>(allNodes);
		}

		/// <summary>
		/// Request a full network rebuild (backward compatibility)
		/// </summary>
		public void RequestNetworkRebuild()
		{
			if (!ZNet.instance.IsServer()) return;
			rebuildManager.RequestFullRebuild();
		}

		/// <summary>
		/// Check if network manager is currently rebuilding
		/// </summary>
		public bool IsRebuilding()
		{
			return rebuildManager != null && rebuildManager.IsRebuildInProgress();
		}

		#endregion

		#region Statistics

		/// <summary>
		/// Get current network statistics
		/// </summary>
		public string GetStatistics()
		{
			int totalNodes = allNodes?.Count ?? 0;
			int totalNetworks = networks?.Count ?? 0;
			int activeNetworks = 0;
			int totalExtractNodes = 0;
			int totalInsertNodes = 0;
			int totalConduitNodes = 0;

			if (networks != null)
			{
				foreach (var network in networks.Values)
				{
					if (network.IsActive && network.IsValid())
						activeNetworks++;

					totalExtractNodes += network.ExtractNodes.Count;
					totalInsertNodes += network.InsertNodes.Count;
					totalConduitNodes += network.ConduitNodes.Count;
				}
			}

			string stats = $"Networks: {totalNetworks} ({activeNetworks} active)\n";
			stats += $"Total Nodes: {totalNodes}\n";
			stats += $"  Extract: {totalExtractNodes}\n";
			stats += $"  Insert: {totalInsertNodes}\n";
			stats += $"  Conduit: {totalConduitNodes}\n";

			if (rebuildManager != null)
			{
				stats += $"\nRebuild Stats:\n{rebuildManager.GetStatistics()}";
			}

			return stats;
		}

		#endregion

		#region Item Transfer

		/// <summary>
		/// Main transfer loop coroutine
		/// </summary>
		private IEnumerator TransferLoop()
		{
			Logger.LogInfo("[ItemConduit] Transfer loop started");

			while (true)
			{
				// Wait for transfer interval
				yield return new WaitForSeconds(ItemConduitMod.TransferInterval.Value);

				// Skip if not server or rebuilding
				if (!ZNet.instance.IsServer() || IsRebuilding())
				{
					continue;
				}

				// Ensure networks dictionary exists
				if (networks == null)
				{
					InitializeFields();
					continue;
				}

				// Process each network
				try
				{
					foreach (var kvp in networks.ToList()) // ToList to avoid modification during iteration
					{
						var network = kvp.Value;
						if (network != null && network.IsActive && network.IsValid())
						{
							ProcessNetworkTransfers(network);
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogError($"[ItemConduit] Error in transfer loop: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Process item transfers for a specific network
		/// </summary>
		private void ProcessNetworkTransfers(ConduitNetwork network)
		{
			if (network == null || !network.IsActive) return;

			try
			{
				// Check if network has both extract and insert nodes
				if (network.ExtractNodes.Count == 0 || network.InsertNodes.Count == 0)
				{
					return;
				}

				// Process each extract node
				foreach (var extractNode in network.ExtractNodes)
				{
					if (extractNode == null || !extractNode.IsActive) continue;

					// Ensure container detection before getting the container
					extractNode.EnsureContainerDetection();

					Container sourceContainer = extractNode.GetTargetContainer();
					if (sourceContainer == null) continue;

					Inventory sourceInventory = sourceContainer.GetInventory();
					if (sourceInventory == null) continue;

					// Find matching insert nodes (same channel or "None")
					var matchingInserts = network.InsertNodes
						.Where(n => n != null && n.IsActive &&
								   (n.ChannelId == extractNode.ChannelId ||
									extractNode.ChannelId == "None" ||
									n.ChannelId == "None"))
						.OrderByDescending(n => n.Priority)
						.ThenBy(n => Vector3.Distance(extractNode.transform.position, n.transform.position))
						.ToList();

					if (matchingInserts.Count == 0) continue;

					// Get items to transfer
					var items = extractNode.GetExtractableItems();
					if (items.Count == 0) continue;

					// Calculate items per transfer based on transfer rate
					int itemsPerTransfer = Mathf.Max(1, Mathf.RoundToInt(ItemConduitMod.TransferRate.Value * ItemConduitMod.TransferInterval.Value));

					foreach (var item in items.Take(itemsPerTransfer))
					{
						if (item == null) continue;

						// Try to insert into matching nodes
						foreach (var insertNode in matchingInserts)
						{
							// Ensure container detection for the insert node
							insertNode.EnsureContainerDetection();

							if (insertNode.CanInsertItem(item))
							{
								// Clone item for transfer (transfer 1 at a time for simplicity)
								var itemToTransfer = item.Clone();
								itemToTransfer.m_stack = 1;

								// Remove from source
								sourceInventory.RemoveItem(item, 1);

								// Add to destination
								if (insertNode.InsertItem(itemToTransfer))
								{
									if (ItemConduitMod.ShowDebugInfo.Value)
									{
										Logger.LogInfo($"[ItemConduit] Transferred 1x {item.m_shared.m_name} from {extractNode.name} to {insertNode.name}");
									}
									break; // Item transferred, move to next item
								}
								else
								{
									// Failed to insert, return item
									sourceInventory.AddItem(itemToTransfer);
								}
							}
						}

						// Break if item has been depleted
						if (item.m_stack <= 0) break;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"[ItemConduit] Error processing network transfers: {ex.Message}");
			}
		}

		#endregion

		#region Debug

		/// <summary>
		/// Log all networks and their nodes (for debugging)
		/// </summary>
		public void LogNetworkState()
		{
			Logger.LogInfo("[ItemConduit] === Current Network State ===");
			Logger.LogInfo($"Total Networks: {networks.Count}");
			Logger.LogInfo($"Total Nodes: {allNodes.Count}");

			foreach (var kvp in networks)
			{
				var network = kvp.Value;
				Logger.LogInfo($"\nNetwork {kvp.Key.Substring(0, 8)}:");
				Logger.LogInfo($"  Nodes: {network.Nodes.Count}");
				Logger.LogInfo($"  Extract: {network.ExtractNodes.Count}");
				Logger.LogInfo($"  Insert: {network.InsertNodes.Count}");
				Logger.LogInfo($"  Conduit: {network.ConduitNodes.Count}");
				Logger.LogInfo($"  Active: {network.IsActive}");
				Logger.LogInfo($"  Valid: {network.IsValid()}");
			}
		}

		#endregion
	}
}