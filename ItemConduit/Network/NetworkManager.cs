using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.Nodes;

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

		/// <summary>Flag indicating if networks are being rebuilt</summary>
		private bool isRebuildingNetworks = false;

		/// <summary>Coroutine reference for transfer loop</summary>
		private Coroutine transferCoroutine;

		/// <summary>Queue for pending network rebuild requests</summary>
		private bool rebuildRequested = false;

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

			Debug.Log("[ItemConduit] NetworkManager fields initialized");
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
				incrementalRebuildCoroutine = StartCoroutine(IncrementalRebuildLoop());
				Debug.Log("[ItemConduit] NetworkManager transfer and rebuild loops started");
			}
			else
			{
				Debug.Log("[ItemConduit] NetworkManager initialized (client mode)");
			}
		}

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

		public void Shutdown()
		{
			if (transferCoroutine != null)
			{
				StopCoroutine(transferCoroutine);
				transferCoroutine = null;
			}

			if (incrementalRebuildCoroutine != null)
			{
				StopCoroutine(incrementalRebuildCoroutine);
				incrementalRebuildCoroutine = null;
			}

			if (networks != null)
				networks.Clear();

			if (nodeToNetworkMap != null)
				nodeToNetworkMap.Clear();

			if (allNodes != null)
				allNodes.Clear();

			if (pendingNodes != null)
				pendingNodes.Clear();

			if (networksNeedingRebuild != null)
				networksNeedingRebuild.Clear();

			if (hopDistanceCache != null)
				hopDistanceCache.Clear();

			Debug.Log("[ItemConduit] NetworkManager shutdown complete");
		}

		#endregion

		#region Node Registration

		public void RegisterNode(BaseNode node)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			if (allNodes == null)
				allNodes = new HashSet<BaseNode>();

			if (allNodes.Add(node))
			{
				if (pendingNodes == null)
					pendingNodes = new Queue<BaseNode>();

				pendingNodes.Enqueue(node);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Registered node: {node.name} (Total: {allNodes.Count}, Pending: {pendingNodes.Count})");
				}
			}
		}

		public void UnregisterNode(BaseNode node)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			if (allNodes != null && allNodes.Remove(node))
			{
				if (nodeToNetworkMap != null && nodeToNetworkMap.TryGetValue(node, out string networkId))
				{
					nodeToNetworkMap.Remove(node);

					if (networksNeedingRebuild != null)
						networksNeedingRebuild.Add(networkId);

					if (networks != null && networks.TryGetValue(networkId, out ConduitNetwork network))
					{
						network.RemoveNode(node);
						ClearHopCacheForNode(node);

						if (network.Nodes.Count == 0)
						{
							networks.Remove(networkId);
							networksNeedingRebuild.Remove(networkId);
						}
					}
				}

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Unregistered node: {node.name} (Remaining: {allNodes.Count})");
				}
			}
		}

		#endregion

		#region Hop-Based Distance Caching

		/// <summary>
		/// Get cached hop distance between extract and insert nodes
		/// </summary>
		/// <param name="extractNode">Source extract node</param>
		/// <param name="insertNode">Target insert node</param>
		/// <returns>Number of hops, or -1 if no path exists</returns>
		public int GetCachedHopDistance(ExtractNode extractNode, InsertNode insertNode)
		{
			var key = (extractNode, insertNode);
			if (hopDistanceCache.TryGetValue(key, out int hops))
			{
				return hops;
			}

			// Calculate and cache the hop distance
			int hopDistance = CalculateHopDistance(extractNode, insertNode);
			hopDistanceCache[key] = hopDistance;

			if (ItemConduitMod.ShowDebugInfo.Value && hopDistance >= 0)
			{
				Debug.Log($"[ItemConduit] Calculated hop distance: {extractNode.name} -> {insertNode.name} = {hopDistance} hops");
			}

			return hopDistance;
		}

		/// <summary>
		/// Calculate hop distance using breadth-first search
		/// </summary>
		/// <param name="start">Starting node</param>
		/// <param name="target">Target node</param>
		/// <returns>Number of hops, or -1 if no path exists</returns>
		private int CalculateHopDistance(BaseNode start, BaseNode target)
		{
			// Wait a frame to batch multiple requests
			yield return null;

			// Use BFS to find shortest path
			var queue = new Queue<(BaseNode node, int distance)>();
			var visited = new HashSet<BaseNode>();

			rebuildRequested = false;
			isRebuildingNetworks = true;

			Debug.Log("[ItemConduit] Starting network rebuild...");

			try
			{
				// Ensure collections exist
				if (networks == null)
					networks = new Dictionary<string, ConduitNetwork>();

				if (allNodes == null)
					allNodes = new HashSet<BaseNode>();

				// Deactivate all nodes during rebuild
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						node.SetActive(false);
					}
				}

				// Clear existing networks
				networks.Clear();

				// Remove any null nodes
				allNodes.RemoveWhere(n => n == null);

				// Rebuild connections for all nodes
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						node.FindConnections();
					}
				}

				// Build new networks
				HashSet<BaseNode> visited = new HashSet<BaseNode>();

				foreach (var node in allNodes)
				{
					if (node != null && !visited.Contains(node))
					{
						ConduitNetwork network = BuildNetworkFromNode(node, visited);

						if (network != null && network.Nodes.Count > 0)
						{
							string networkId = Guid.NewGuid().ToString();
							network.NetworkId = networkId;
							networks[networkId] = network;

							// Set network ID on all nodes
							foreach (var netNode in network.Nodes)
							{
								if (netNode != null)
								{
									netNode.SetNetworkId(networkId);
								}
							}
						}
					}
				}

				// Reactivate all nodes
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						node.SetActive(true);
					}
				}

				Debug.Log($"[ItemConduit] Network rebuild complete. {networks.Count} networks active.");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ItemConduit] Error during network rebuild: {ex.Message}");
			}
			finally
			{
				isRebuildingNetworks = false;
			}
		}

		/// <summary>
		/// Build a network starting from a specific node
		/// </summary>
		private ConduitNetwork BuildNetworkFromNode(BaseNode startNode, HashSet<BaseNode> visited)
		{
			if (startNode == null) return null;

			ConduitNetwork network = new ConduitNetwork();
			Queue<BaseNode> queue = new Queue<BaseNode>();

			queue.Enqueue(startNode);
			visited.Add(startNode);

			while (queue.Count > 0)
			{
				BaseNode currentNode = queue.Dequeue();
				if (currentNode == null) continue;

				network.AddNode(currentNode);

				// Add all connected nodes to the queue
				foreach (var connectedNode in currentNode.GetConnectedNodes())
				{
					if (connectedNode != null && !visited.Contains(connectedNode))
					{
						visited.Add(connectedNode);
						queue.Enqueue(connectedNode);
					}
				}
			}

			return network;
		}

		#endregion

		#region Item Transfer

		/// <summary>
		/// Main transfer loop coroutine
		/// </summary>
		private IEnumerator TransferLoop()
		{
			Debug.Log("[ItemConduit] Transfer loop started");

			while (true)
			{
				// Wait for transfer interval
				yield return new WaitForSeconds(ItemConduitMod.TransferInterval.Value);

				// Skip if not server or rebuilding
				if (!ZNet.instance.IsServer() || isRebuildingNetworks)
				{
					continue;
				}

				if (networks == null) continue;

				// Process each network
				try
				{
					foreach (var kvp in networks.ToList()) // ToList to avoid modification during iteration
					{
						var network = kvp.Value;
						if (network != null && network.IsActive)
						{
							ProcessNetworkTransfers(network);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[ItemConduit] Error in transfer loop: {ex.Message}");
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
				if (network.ExtractNodes.Count == 0 || network.InsertNodes.Count == 0)
				{
					return;
				}

				// Simple transfer: from each extract to matching insert nodes
				foreach (var extractNode in network.ExtractNodes)
				{
					if (extractNode == null || !extractNode.IsActive) continue;

					Container sourceContainer = extractNode.GetTargetContainer();
					if (sourceContainer == null) continue;

					Inventory sourceInventory = sourceContainer.GetInventory();
					if (sourceInventory == null) continue;

					// Find matching insert nodes (same channel)
					var matchingInserts = network.InsertNodes
						.Where(n => n != null && n.IsActive && n.ChannelId == extractNode.ChannelId)
						.OrderByDescending(n => n.Priority)
						.ToList();

					if (matchingInserts.Count == 0) continue;

					// Get items to transfer
					var items = extractNode.GetExtractableItems();

					foreach (var item in items)
					{
						if (item == null) continue;

						// Try to insert into matching nodes
						foreach (var insertNode in matchingInserts)
						{
							if (insertNode.CanInsertItem(item))
							{
								// Clone item for transfer
								var itemToTransfer = item.Clone();
								itemToTransfer.m_stack = (Int32)Math.Min(item.m_stack, ItemConduitMod.TransferRate.Value);

								// Remove from source
								sourceInventory.RemoveItem(item, itemToTransfer.m_stack);

								// Add to destination
								if (insertNode.InsertItem(itemToTransfer))
								{
									if (ItemConduitMod.ShowDebugInfo.Value)
									{
										Debug.Log($"[ItemConduit] Transferred {itemToTransfer.m_stack}x {item.m_shared.m_name}");
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
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ItemConduit] Error processing network transfers: {ex.Message}");
			}
		}

		#endregion
	}

	/// <summary>
	/// Represents a connected network of conduit nodes
	/// </summary>
	
}