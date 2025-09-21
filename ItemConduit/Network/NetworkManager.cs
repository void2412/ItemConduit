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
	/// Optimized Network Manager with hop-based distance caching
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
					_instance.InitializeFields();
				}
				return _instance;
			}
		}

		#endregion

		#region Fields

		/// <summary>All active networks indexed by ID</summary>
		private Dictionary<string, ConduitNetwork> networks;

		/// <summary>Node to network ID mapping for fast lookup</summary>
		private Dictionary<BaseNode, string> nodeToNetworkMap;

		/// <summary>All registered nodes in the system</summary>
		private HashSet<BaseNode> allNodes;

		/// <summary>Nodes pending network assignment</summary>
		private Queue<BaseNode> pendingNodes;

		/// <summary>Networks that need incremental rebuilding</summary>
		private HashSet<string> networksNeedingRebuild;

		/// <summary>Flag indicating if networks are being rebuilt</summary>
		private bool isRebuildingNetworks = false;

		/// <summary>Coroutine reference for transfer loop</summary>
		private Coroutine transferCoroutine;

		/// <summary>Coroutine reference for incremental rebuild</summary>
		private Coroutine incrementalRebuildCoroutine;

<<<<<<< HEAD
		/// <summary>Hop distance cache for extract-insert node pairs</summary>
		private Dictionary<(ExtractNode, InsertNode), int> hopDistanceCache;

		/// <summary>Last update time for performance monitoring</summary>
		private float lastUpdateTime;

		/// <summary>Maximum time per frame for incremental operations</summary>
		private const float MAX_FRAME_TIME = 2f; // 2ms per frame


<<<<<<< HEAD

=======
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
		#endregion

		#region Initialization

		private void InitializeFields()
		{
			if (networks == null)
				networks = new Dictionary<string, ConduitNetwork>();

			if (nodeToNetworkMap == null)
				nodeToNetworkMap = new Dictionary<BaseNode, string>();

			if (allNodes == null)
				allNodes = new HashSet<BaseNode>();

			if (pendingNodes == null)
				pendingNodes = new Queue<BaseNode>();

			if (networksNeedingRebuild == null)
				networksNeedingRebuild = new HashSet<string>();

			if (hopDistanceCache == null)
				hopDistanceCache = new Dictionary<(ExtractNode, InsertNode), int>();

			Debug.Log("[ItemConduit] NetworkManager fields initialized");
		}

		public void Initialize()
		{
			InitializeFields();

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
			if (start == null || target == null || start == target)
				return start == target ? 0 : -1;

			// Use BFS to find shortest path
			var queue = new Queue<(BaseNode node, int distance)>();
			var visited = new HashSet<BaseNode>();

			queue.Enqueue((start, 0));
			visited.Add(start);

			while (queue.Count > 0)
			{
				var (currentNode, currentDistance) = queue.Dequeue();

				// Check all connected nodes
				foreach (var connectedNode in currentNode.GetConnectedNodes())
				{
					if (connectedNode == null || visited.Contains(connectedNode))
						continue;

					// Found the target
					if (connectedNode == target)
					{
						return currentDistance + 1;
					}

					// Add to queue for further exploration
					visited.Add(connectedNode);
					queue.Enqueue((connectedNode, currentDistance + 1));
				}
			}

			// No path found
			return -1;
		}

		/// <summary>
		/// Clear hop cache entries for a specific node
		/// </summary>
		private void ClearHopCacheForNode(BaseNode node)
		{
			if (hopDistanceCache == null) return;

			var keysToRemove = new List<(ExtractNode, InsertNode)>();

			foreach (var key in hopDistanceCache.Keys)
			{
				if (key.Item1 == node || key.Item2 == node)
				{
					keysToRemove.Add(key);
				}
			}

			foreach (var key in keysToRemove)
			{
				hopDistanceCache.Remove(key);
			}

			if (ItemConduitMod.ShowDebugInfo.Value && keysToRemove.Count > 0)
			{
				Debug.Log($"[ItemConduit] Cleared {keysToRemove.Count} hop cache entries for node {node.name}");
			}
		}

		/// <summary>
		/// Rebuild hop distance cache for a specific network
		/// </summary>
		private void RebuildHopCacheForNetwork(ConduitNetwork network)
		{
<<<<<<< HEAD
<<<<<<< HEAD
			if (network == null) return;
=======
			// Wait a frame to batch multiple requests
			yield return null;
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
			// Wait a frame to batch multiple requests
			yield return null;
>>>>>>> parent of 4c82026 (Working version not optimized)

			// Clear existing cache entries for this network
			var keysToRemove = new List<(ExtractNode, InsertNode)>();

<<<<<<< HEAD
			foreach (var key in hopDistanceCache.Keys)
			{
				if (network.ExtractNodes.Contains(key.Item1) || network.InsertNodes.Contains(key.Item2))
				{
					keysToRemove.Add(key);
				}
			}

			foreach (var key in keysToRemove)
			{
				hopDistanceCache.Remove(key);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Rebuilding hop cache for network {network.NetworkId} - " +
						 $"{network.ExtractNodes.Count} extractors, {network.InsertNodes.Count} inserters");
			}

			// Pre-calculate hop distances for frequently used pairs
			// Only cache paths for nodes in the same network that can actually communicate
			foreach (var extractNode in network.ExtractNodes)
			{
				if (extractNode == null || !extractNode.IsActive) continue;

				// Find insert nodes with matching channels
				var matchingInserts = network.GetSortedInsertNodesForChannel(extractNode.ChannelId);

				foreach (var insertNode in matchingInserts)
=======
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
>>>>>>> parent of 4c82026 (Working version not optimized)
				{
					if (insertNode == null || !insertNode.IsActive) continue;

					// Calculate and cache hop distance
					var key = (extractNode, insertNode);
					if (!hopDistanceCache.ContainsKey(key))
					{
<<<<<<< HEAD
						int hopDistance = CalculateHopDistance(extractNode, insertNode);
						if (hopDistance >= 0) // Only cache valid paths
						{
							hopDistanceCache[key] = hopDistance;
=======
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
<<<<<<< HEAD
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
						}
					}
				}
			}

<<<<<<< HEAD
<<<<<<< HEAD
			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				int validPaths = hopDistanceCache.Count(kvp =>
					network.ExtractNodes.Contains(kvp.Key.Item1) &&
					network.InsertNodes.Contains(kvp.Key.Item2) &&
					kvp.Value >= 0);

				Debug.Log($"[ItemConduit] Cached {validPaths} valid hop paths for network {network.NetworkId}");
			}
		}

		/// <summary>
		/// Get insert nodes sorted by hop distance from an extract node
		/// </summary>
		/// <param name="extractNode">The source extract node</param>
		/// <param name="insertNodes">List of potential insert nodes</param>
		/// <returns>Insert nodes sorted by hop distance (closest first), then by priority</returns>
		public List<InsertNode> GetInsertNodesSortedByHopDistance(ExtractNode extractNode, List<InsertNode> insertNodes)
		{
			if (extractNode == null || insertNodes == null || insertNodes.Count == 0)
				return new List<InsertNode>();

			// Create list with hop distances
			var nodesWithHops = new List<(InsertNode node, int hops, int priority)>();

			foreach (var insertNode in insertNodes)
			{
				if (insertNode == null || !insertNode.IsActive) continue;

				int hopDistance = GetCachedHopDistance(extractNode, insertNode);
				if (hopDistance >= 0) // Only include reachable nodes
=======
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
				// Reactivate all nodes
				foreach (var node in allNodes)
>>>>>>> parent of 4c82026 (Working version not optimized)
				{
					nodesWithHops.Add((insertNode, hopDistance, insertNode.Priority));
				}
			}

			// Sort by: 1) Priority (high first), 2) Hop distance (low first), 3) Name (for consistency)
			nodesWithHops.Sort((a, b) =>
			{
				// First by priority (higher first)
				int priorityComparison = b.priority.CompareTo(a.priority);
				if (priorityComparison != 0) return priorityComparison;

				// Then by hop distance (lower first)
				int hopComparison = a.hops.CompareTo(b.hops);
				if (hopComparison != 0) return hopComparison;

				// Finally by name for consistency
				return string.Compare(a.node.name, b.node.name, System.StringComparison.Ordinal);
			});

			var result = nodesWithHops.Select(x => x.node).ToList();

			if (ItemConduitMod.ShowDebugInfo.Value && result.Count > 0)
			{
				string sortInfo = string.Join(", ", nodesWithHops.Take(5).Select(x => $"{x.node.name}(P:{x.priority},H:{x.hops})"));
				Debug.Log($"[ItemConduit] Sorted insert nodes for {extractNode.name}: {sortInfo}");
			}

			return result;
		}

		#endregion

		#region Network Priority Management

		public void OnInsertNodePriorityChanged(InsertNode insertNode)
		{
			if (insertNode == null) return;
			if (!nodeToNetworkMap.TryGetValue(insertNode, out string networkId)) return;
			if (!networks.TryGetValue(networkId, out ConduitNetwork network)) return;

			network.SortInsertNodesByPriority();

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Sorted insert nodes for network {networkId} after priority change");
			}
		}

		#endregion

		#region Incremental Network Building

		private IEnumerator IncrementalRebuildLoop()
		{
			while (true)
			{
				yield return new WaitForSeconds(0.1f);

				if (isRebuildingNetworks) continue;

				if (pendingNodes != null && pendingNodes.Count > 0)
				{
					yield return StartCoroutine(ProcessPendingNodesIncremental());
				}

<<<<<<< HEAD
<<<<<<< HEAD
				if (networksNeedingRebuild != null && networksNeedingRebuild.Count > 0)
				{
					yield return StartCoroutine(ProcessNetworkRebuildsIncremental());
				}
=======
				Debug.Log($"[ItemConduit] Network rebuild complete. {networks.Count} networks active.");
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
				Debug.Log($"[ItemConduit] Network rebuild complete. {networks.Count} networks active.");
>>>>>>> parent of 4c82026 (Working version not optimized)
			}
		}

		private IEnumerator ProcessPendingNodesIncremental()
		{
			isRebuildingNetworks = true;
			float frameStartTime = Time.realtimeSinceStartup;

			try
			{
<<<<<<< HEAD
<<<<<<< HEAD
				while (pendingNodes.Count > 0)
				{
					if (Time.realtimeSinceStartup - frameStartTime > MAX_FRAME_TIME / 1000f)
					{
						yield return null;
						frameStartTime = Time.realtimeSinceStartup;
					}

					BaseNode node = pendingNodes.Dequeue();
					if (node == null) continue;

					node.FindConnections();
					AssignNodeToNetwork(node);
				}
=======
				Debug.LogError($"[ItemConduit] Error during network rebuild: {ex.Message}");
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
				Debug.LogError($"[ItemConduit] Error during network rebuild: {ex.Message}");
>>>>>>> parent of 4c82026 (Working version not optimized)
			}
			finally
			{
				isRebuildingNetworks = false;
			}
		}

<<<<<<< HEAD
		private IEnumerator ProcessNetworkRebuildsIncremental()
=======
		/// <summary>
		/// Build a network starting from a specific node
		/// </summary>
		private ConduitNetwork BuildNetworkFromNode(BaseNode startNode, HashSet<BaseNode> visited)
>>>>>>> parent of 4c82026 (Working version not optimized)
		{
			isRebuildingNetworks = true;
			float frameStartTime = Time.realtimeSinceStartup;

			try
			{
				var networksToRebuild = new List<string>(networksNeedingRebuild);
				networksNeedingRebuild.Clear();

<<<<<<< HEAD
<<<<<<< HEAD
				foreach (string networkId in networksToRebuild)
=======
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
				network.AddNode(currentNode);

				// Add all connected nodes to the queue
				foreach (var connectedNode in currentNode.GetConnectedNodes())
>>>>>>> parent of 4c82026 (Working version not optimized)
				{
					if (Time.realtimeSinceStartup - frameStartTime > MAX_FRAME_TIME / 1000f)
					{
						yield return null;
						frameStartTime = Time.realtimeSinceStartup;
					}

					if (networks.TryGetValue(networkId, out ConduitNetwork network))
					{
						yield return StartCoroutine(RebuildSpecificNetwork(network));
					}
				}
			}
			finally
			{
				isRebuildingNetworks = false;
			}
		}

		// ... [Previous network assignment and management methods remain the same] ...

		private void AssignNodeToNetwork(BaseNode node)
		{
			if (node == null) return;

			var connectedNodes = node.GetConnectedNodes();
			HashSet<string> connectedNetworkIds = new HashSet<string>();

			foreach (var connectedNode in connectedNodes)
			{
				if (nodeToNetworkMap.TryGetValue(connectedNode, out string connectedNetworkId))
				{
					connectedNetworkIds.Add(connectedNetworkId);
				}
			}

			if (connectedNetworkIds.Count == 0)
			{
				CreateNewNetworkForNode(node);
			}
			else if (connectedNetworkIds.Count == 1)
			{
				string networkId = connectedNetworkIds.First();
				AddNodeToNetwork(node, networkId);
			}
			else
			{
				MergeNetworksAndAddNode(node, connectedNetworkIds);
			}
		}

		private void CreateNewNetworkForNode(BaseNode node)
		{
			string networkId = Guid.NewGuid().ToString();
			ConduitNetwork network = new ConduitNetwork { NetworkId = networkId };

			networks[networkId] = network;
			AddNodeToNetwork(node, networkId);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Created new network {networkId} for node {node.name}");
			}
		}

		private void AddNodeToNetwork(BaseNode node, string networkId)
		{
			if (!networks.TryGetValue(networkId, out ConduitNetwork network)) return;

			network.AddNode(node);
			nodeToNetworkMap[node] = networkId;
			node.SetNetworkId(networkId);

			RebuildHopCacheForNetwork(network);
		}

		private void MergeNetworksAndAddNode(BaseNode node, HashSet<string> networkIds)
		{
			if (networkIds.Count <= 1) return;

			string primaryNetworkId = null;
			int maxSize = 0;

			foreach (string networkId in networkIds)
			{
				if (networks.TryGetValue(networkId, out ConduitNetwork network))
				{
					if (network.Nodes.Count > maxSize)
					{
						maxSize = network.Nodes.Count;
						primaryNetworkId = networkId;
					}
				}
			}

<<<<<<< HEAD
<<<<<<< HEAD
			if (primaryNetworkId == null) return;

			ConduitNetwork primaryNetwork = networks[primaryNetworkId];

			foreach (string networkId in networkIds)
			{
				if (networkId == primaryNetworkId) continue;

				if (networks.TryGetValue(networkId, out ConduitNetwork networkToMerge))
				{
					foreach (var nodeToMove in networkToMerge.Nodes.ToList())
					{
						primaryNetwork.AddNode(nodeToMove);
						nodeToNetworkMap[nodeToMove] = primaryNetworkId;
						nodeToMove.SetNetworkId(primaryNetworkId);
					}

					networks.Remove(networkId);
				}
			}

			AddNodeToNetwork(node, primaryNetworkId);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Merged {networkIds.Count} networks into {primaryNetworkId} via node {node.name}");
			}
		}

		private IEnumerator RebuildSpecificNetwork(ConduitNetwork network)
		{
			if (network == null) yield break;

			float frameStartTime = Time.realtimeSinceStartup;

			var connectedComponents = FindConnectedComponents(network.Nodes);

			if (connectedComponents.Count > 1)
			{
				string originalNetworkId = network.NetworkId;
				bool firstComponent = true;

				foreach (var component in connectedComponents)
				{
					if (Time.realtimeSinceStartup - frameStartTime > MAX_FRAME_TIME / 1000f)
					{
						yield return null;
						frameStartTime = Time.realtimeSinceStartup;
					}

					if (firstComponent)
					{
						network.Clear();
						foreach (var node in component)
						{
							network.AddNode(node);
						}
						firstComponent = false;
					}
					else
					{
						string newNetworkId = Guid.NewGuid().ToString();
						ConduitNetwork newNetwork = new ConduitNetwork { NetworkId = newNetworkId };

						foreach (var node in component)
						{
							newNetwork.AddNode(node);
							nodeToNetworkMap[node] = newNetworkId;
							node.SetNetworkId(newNetworkId);
						}

						networks[newNetworkId] = newNetwork;
					}
				}

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Split network {originalNetworkId} into {connectedComponents.Count} components");
				}
			}

			RebuildHopCacheForNetwork(network);
		}

		private List<HashSet<BaseNode>> FindConnectedComponents(HashSet<BaseNode> nodes)
		{
			var components = new List<HashSet<BaseNode>>();
			var visited = new HashSet<BaseNode>();

			foreach (var node in nodes)
			{
				if (visited.Contains(node)) continue;

				var component = new HashSet<BaseNode>();
				var queue = new Queue<BaseNode>();

				queue.Enqueue(node);
				visited.Add(node);

				while (queue.Count > 0)
				{
					var current = queue.Dequeue();
					component.Add(current);

					foreach (var connected in current.GetConnectedNodes())
					{
						if (nodes.Contains(connected) && !visited.Contains(connected))
						{
							visited.Add(connected);
							queue.Enqueue(connected);
						}
					}
				}

				components.Add(component);
			}

			return components;
=======
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
			return network;
>>>>>>> parent of 4c82026 (Working version not optimized)
		}

		#endregion

		#region Item Transfer

		private IEnumerator TransferLoop()
		{
			Debug.Log("[ItemConduit] Transfer loop started");

			while (true)
			{
				yield return new WaitForSeconds(ItemConduitMod.TransferInterval.Value);

				if (!ZNet.instance.IsServer() || isRebuildingNetworks)
				{
					continue;
				}

				if (networks == null) continue;

				try
				{
					foreach (var kvp in networks.ToList())
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

		private void ProcessNetworkTransfers(ConduitNetwork network)
		{
			if (network == null || !network.IsActive) return;

			try
			{
				if (network.ExtractNodes.Count == 0 || network.InsertNodes.Count == 0)
				{
					return;
				}

<<<<<<< HEAD
<<<<<<< HEAD
=======
				// Simple transfer: from each extract to matching insert nodes
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
				// Simple transfer: from each extract to matching insert nodes
>>>>>>> parent of 4c82026 (Working version not optimized)
				foreach (var extractNode in network.ExtractNodes)
				{
					if (extractNode == null || !extractNode.IsActive) continue;

					Container sourceContainer = extractNode.GetTargetContainer();
					if (sourceContainer == null) continue;

					Inventory sourceInventory = sourceContainer.GetInventory();
					if (sourceInventory == null) continue;

<<<<<<< HEAD
<<<<<<< HEAD
					// Get insert nodes for this channel, sorted by priority and hop distance
					var channelInserts = network.GetSortedInsertNodesForChannel(extractNode.ChannelId);
					var sortedInserts = GetInsertNodesSortedByHopDistance(extractNode, channelInserts);
=======
=======
>>>>>>> parent of 4c82026 (Working version not optimized)
					// Find matching insert nodes (same channel)
					var matchingInserts = network.InsertNodes
						.Where(n => n != null && n.IsActive && n.ChannelId == extractNode.ChannelId)
						.OrderByDescending(n => n.Priority)
						.ToList();
>>>>>>> parent of 4c82026 (Working version not optimized)

					if (sortedInserts.Count == 0) continue;

					var items = extractNode.GetExtractableItems();

					foreach (var item in items)
					{
						if (item == null) continue;

						foreach (var insertNode in sortedInserts)
						{
							if (insertNode.CanInsertItem(item))
							{
<<<<<<< HEAD
<<<<<<< HEAD
								var itemToTransfer = item.Clone();
								itemToTransfer.m_stack = (int)Math.Min(item.m_stack, ItemConduitMod.TransferRate.Value);

=======
								// Clone item for transfer
								var itemToTransfer = item.Clone();
								itemToTransfer.m_stack = (Int32)Math.Min(item.m_stack, ItemConduitMod.TransferRate.Value);

								// Remove from source
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
								// Clone item for transfer
								var itemToTransfer = item.Clone();
								itemToTransfer.m_stack = (Int32)Math.Min(item.m_stack, ItemConduitMod.TransferRate.Value);

								// Remove from source
>>>>>>> parent of 4c82026 (Working version not optimized)
								sourceInventory.RemoveItem(item, itemToTransfer.m_stack);

								if (insertNode.InsertItem(itemToTransfer))
								{
									if (ItemConduitMod.ShowDebugInfo.Value)
									{
<<<<<<< HEAD
<<<<<<< HEAD
										int hops = GetCachedHopDistance(extractNode, insertNode);
										Debug.Log($"[ItemConduit] Transferred {itemToTransfer.m_stack}x {item.m_shared.m_name} " +
												 $"from {extractNode.name} to {insertNode.name} ({hops} hops)");
=======
										Debug.Log($"[ItemConduit] Transferred {itemToTransfer.m_stack}x {item.m_shared.m_name}");
>>>>>>> parent of 4c82026 (Working version not optimized)
=======
										Debug.Log($"[ItemConduit] Transferred {itemToTransfer.m_stack}x {item.m_shared.m_name}");
>>>>>>> parent of 4c82026 (Working version not optimized)
									}
									break;
								}
								else
								{
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

		#region Performance Monitoring

		private void Update()
		{
			if (Time.time - lastUpdateTime > 5f)
			{
				lastUpdateTime = Time.time;

				if (ItemConduitMod.ShowDebugInfo.Value && ZNet.instance.IsServer())
				{
					Debug.Log($"[ItemConduit] Performance Stats - Networks: {networks?.Count ?? 0}, " +
							 $"Nodes: {allNodes?.Count ?? 0}, " +
							 $"Pending: {pendingNodes?.Count ?? 0}, " +
							 $"Hop Cache: {hopDistanceCache?.Count ?? 0}");
				}
			}
		}

		#endregion
	}

	/// <summary>
	/// Represents a connected network of conduit nodes
	/// </summary>
	
}