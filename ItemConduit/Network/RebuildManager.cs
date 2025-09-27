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
	/// Optimized rebuild manager with proper coroutine handling
	/// Spreads rebuild work across multiple frames to prevent FPS drops
	/// </summary>
	public class RebuildManager : MonoBehaviour
	{
		#region Singleton

		private static RebuildManager _instance;
		public static RebuildManager Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_RebuildManager");
					_instance = go.AddComponent<RebuildManager>();
					DontDestroyOnLoad(go);
					_instance.Initialize();
				}
				return _instance;
			}
		}

		#endregion

		#region Fields

		/// <summary>Queue of nodes that need their networks rebuilt</summary>
		private HashSet<BaseNode> nodesNeedingRebuild;

		/// <summary>Set of nodes currently being processed</summary>
		private HashSet<BaseNode> nodesBeingProcessed;

		/// <summary>Flag indicating if a rebuild is currently in progress</summary>
		private bool isRebuildInProgress = false;

		/// <summary>Coroutine reference for the rebuild process</summary>
		private Coroutine rebuildCoroutine;

		/// <summary>Time to wait before starting rebuild (to batch multiple changes)</summary>
		private float rebuildDelay = 0.5f;

		/// <summary>Time of last rebuild request</summary>
		private float lastRebuildRequestTime;

		/// <summary>Statistics tracking</summary>
		private RebuildStatistics statistics;

		/// <summary>Maximum nodes to process per frame</summary>
		private const int NODES_PER_FRAME = 5;

		/// <summary>Maximum time in milliseconds per frame for rebuild operations</summary>
		private const float MAX_MS_PER_FRAME = 5f;

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize the rebuild manager
		/// </summary>
		private void Initialize()
		{
			nodesNeedingRebuild = new HashSet<BaseNode>();
			nodesBeingProcessed = new HashSet<BaseNode>();
			statistics = new RebuildStatistics();

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo("[ItemConduit] RebuildManager initialized");
			}
		}

		/// <summary>
		/// Ensure singleton pattern
		/// </summary>
		private void Awake()
		{
			if (_instance == null)
			{
				_instance = this;
				DontDestroyOnLoad(gameObject);
				Initialize();
			}
			else if (_instance != this)
			{
				Destroy(gameObject);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Request a rebuild for a specific node and its connected network
		/// </summary>
		public void RequestRebuildForNode(BaseNode node, bool priority = false)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			// Add to rebuild queue
			nodesNeedingRebuild.Add(node);
			lastRebuildRequestTime = Time.time;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Rebuild requested for node: {node.name} (Priority: {priority})");
			}

			// Start rebuild process
			if (priority)
			{
				// High priority - start immediately
				StartRebuild(0.1f);
			}
			else
			{
				// Normal priority - use standard delay
				StartRebuild(rebuildDelay);
			}
		}

		/// <summary>
		/// Request a full network rebuild
		/// </summary>
		public void RequestFullRebuild()
		{
			if (!ZNet.instance.IsServer()) return;

			// Get all nodes from NetworkManager
			var allNodes = NetworkManager.Instance.GetAllNodes();

			foreach (var node in allNodes)
			{
				if (node != null)
				{
					nodesNeedingRebuild.Add(node);
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Full rebuild requested for {nodesNeedingRebuild.Count} nodes");
			}

			StartRebuild(rebuildDelay);
		}

		/// <summary>
		/// Cancel any pending rebuilds
		/// </summary>
		public void CancelPendingRebuilds()
		{
			if (rebuildCoroutine != null)
			{
				StopCoroutine(rebuildCoroutine);
				rebuildCoroutine = null;
			}

			nodesNeedingRebuild.Clear();
			nodesBeingProcessed.Clear();
			isRebuildInProgress = false;

			Logger.LogInfo("[ItemConduit] Pending rebuilds cancelled");
		}

		/// <summary>
		/// Get current rebuild statistics
		/// </summary>
		public RebuildStatistics GetStatistics()
		{
			return statistics;
		}

		/// <summary>
		/// Check if a rebuild is currently in progress
		/// </summary>
		public bool IsRebuildInProgress()
		{
			return isRebuildInProgress;
		}

		/// <summary>
		/// Set the rebuild delay
		/// </summary>
		public void SetRebuildDelay(float delay)
		{
			rebuildDelay = Mathf.Clamp(delay, 0.1f, 5f);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Start the rebuild process with specified delay
		/// </summary>
		private void StartRebuild(float delay)
		{
			// If already rebuilding or no nodes need rebuild, skip
			if (isRebuildInProgress || nodesNeedingRebuild.Count == 0) return;

			// Cancel existing coroutine if any
			if (rebuildCoroutine != null)
			{
				StopCoroutine(rebuildCoroutine);
			}

			// Start new rebuild coroutine
			rebuildCoroutine = StartCoroutine(RebuildCoroutine(delay));
		}

		/// <summary>
		/// Main rebuild coroutine - optimized to spread work across frames
		/// </summary>
		private IEnumerator RebuildCoroutine(float delay)
		{
			// Wait for the specified delay to batch multiple changes
			yield return new WaitForSeconds(delay);

			// Check if more nodes were added while waiting
			float timeSinceLastRequest = Time.time - lastRebuildRequestTime;
			if (timeSinceLastRequest < delay * 0.5f)
			{
				// More nodes were recently added, wait a bit more
				yield return new WaitForSeconds(delay * 0.5f);
			}

			// Begin rebuild process
			isRebuildInProgress = true;
			float startTime = Time.realtimeSinceStartup;

			// Collect affected data
			var affectedData = CollectAffectedNodesAndNetworks();

			if (affectedData.nodesToRebuild.Count == 0)
			{
				Logger.LogInfo("[ItemConduit] No nodes to rebuild");
				isRebuildInProgress = false;
				yield break;
			}

			Logger.LogInfo("[ItemConduit] ========================================");
			Logger.LogInfo($"[ItemConduit] Starting network rebuild");
			Logger.LogInfo($"[ItemConduit] Affected networks: {affectedData.affectedNetworkIds.Count}");
			Logger.LogInfo($"[ItemConduit] Nodes to rebuild: {affectedData.nodesToRebuild.Count}");
			Logger.LogInfo("[ItemConduit] ========================================");

			// Step 1: Deactivate affected nodes
			yield return StartCoroutine(DeactivateNodesCoroutine(affectedData.nodesToRebuild));

			// Step 2: Clear old network data
			ClearNetworkData(affectedData.affectedNetworkIds);
			yield return null; // Give a frame for cleanup

			// Step 3: Rebuild connections
			yield return StartCoroutine(RebuildConnectionsCoroutine(affectedData.nodesToRebuild));

			// Step 4: Create new networks
			int newNetworks = 0;
			yield return StartCoroutine(CreateNetworksCoroutine(affectedData.nodesToRebuild, (count) => {
				newNetworks = count;
			}));

			// Step 5: Reactivate nodes
			yield return StartCoroutine(ReactivateNodesCoroutine(affectedData.nodesToRebuild));

			// Update statistics
			float rebuildTime = Time.realtimeSinceStartup - startTime;
			statistics.RecordRebuild(
				affectedData.nodesToRebuild.Count,
				affectedData.affectedNetworkIds.Count,
				newNetworks,
				rebuildTime
			);

			Logger.LogInfo("[ItemConduit] ========================================");
			Logger.LogInfo($"[ItemConduit] Rebuild complete in {rebuildTime:F2}s");
			Logger.LogInfo($"[ItemConduit] Networks created: {newNetworks}");
			Logger.LogInfo($"[ItemConduit] Total rebuilds: {statistics.TotalRebuilds}");
			Logger.LogInfo("[ItemConduit] ========================================");

			// Clean up
			nodesNeedingRebuild.Clear();
			nodesBeingProcessed.Clear();
			isRebuildInProgress = false;
			rebuildCoroutine = null;
		}

		/// <summary>
		/// Collect all nodes and networks that need to be rebuilt
		/// </summary>
		private (HashSet<BaseNode> nodesToRebuild, HashSet<string> affectedNetworkIds) CollectAffectedNodesAndNetworks()
		{
			HashSet<BaseNode> nodesToRebuild = new HashSet<BaseNode>();
			HashSet<string> affectedNetworkIds = new HashSet<string>();

			// Remove any null nodes
			nodesNeedingRebuild.RemoveWhere(n => n == null);

			// Process each node that needs rebuild
			foreach (var node in nodesNeedingRebuild)
			{
				// Add the node itself
				nodesToRebuild.Add(node);

				// Get the network this node belongs to
				string networkId = NetworkManager.Instance.GetNodeNetworkId(node);
				if (!string.IsNullOrEmpty(networkId))
				{
					affectedNetworkIds.Add(networkId);

					// Add all nodes from this network
					var networkNodes = NetworkManager.Instance.GetNetworkNodes(networkId);
					foreach (var netNode in networkNodes)
					{
						if (netNode != null)
						{
							nodesToRebuild.Add(netNode);
						}
					}
				}

				// Find nearby nodes using physics (more efficient than FindObjectsOfType)
				var nearbyNodes = FindNearbyNodesPhysics(node, 10f);
				foreach (var nearbyNode in nearbyNodes)
				{
					nodesToRebuild.Add(nearbyNode);

					// Add their networks too
					string nearbyNetworkId = NetworkManager.Instance.GetNodeNetworkId(nearbyNode);
					if (!string.IsNullOrEmpty(nearbyNetworkId))
					{
						affectedNetworkIds.Add(nearbyNetworkId);
					}
				}
			}

			// Track which nodes are being processed
			nodesBeingProcessed = new HashSet<BaseNode>(nodesToRebuild);

			return (nodesToRebuild, affectedNetworkIds);
		}

		/// <summary>
		/// Find nodes near a given position using physics
		/// </summary>
		private List<BaseNode> FindNearbyNodesPhysics(BaseNode centerNode, float radius)
		{
			List<BaseNode> nearbyNodes = new List<BaseNode>();

			Collider[] colliders = Physics.OverlapSphere(centerNode.transform.position, radius);
			foreach (var collider in colliders)
			{
				BaseNode node = collider.GetComponent<BaseNode>();
				if (node == null)
				{
					node = collider.GetComponentInParent<BaseNode>();
				}

				if (node != null && node != centerNode)
				{
					nearbyNodes.Add(node);
				}
			}

			return nearbyNodes;
		}

		/// <summary>
		/// Deactivate nodes using coroutine
		/// </summary>
		private IEnumerator DeactivateNodesCoroutine(HashSet<BaseNode> nodes)
		{
			int processed = 0;
			foreach (var node in nodes)
			{
				if (node != null)
				{
					node.SetActive(false);
					node.SetNetworkId(null);
					processed++;

					// Yield every few nodes
					if (processed % NODES_PER_FRAME == 0)
					{
						yield return null;
					}
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Deactivated {processed} nodes");
			}
		}

		/// <summary>
		/// Clear network data for affected networks
		/// </summary>
		private void ClearNetworkData(HashSet<string> networkIds)
		{
			foreach (string networkId in networkIds)
			{
				NetworkManager.Instance.RemoveNetwork(networkId);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Cleared {networkIds.Count} networks");
			}
		}

		/// <summary>
		/// Rebuild connections using coroutine
		/// </summary>
		private IEnumerator RebuildConnectionsCoroutine(HashSet<BaseNode> nodes)
		{
			float frameStartTime = Time.realtimeSinceStartup;
			int processed = 0;

			foreach (var node in nodes)
			{
				if (node != null)
				{
					// Start connection detection for this node
					node.FindConnections();
					processed++;

					// Check if we've spent too much time this frame
					float elapsedMs = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
					if (elapsedMs > MAX_MS_PER_FRAME || processed % NODES_PER_FRAME == 0)
					{
						yield return null;
						frameStartTime = Time.realtimeSinceStartup;
					}
				}
			}

			// Wait for all connection detections to complete
			yield return new WaitForSeconds(0.5f);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Rebuilt connections for {processed} nodes");
			}
		}

		/// <summary>
		/// Create networks using coroutine
		/// </summary>
		private IEnumerator CreateNetworksCoroutine(HashSet<BaseNode> nodes, System.Action<int> onComplete)
		{
			HashSet<BaseNode> visited = new HashSet<BaseNode>();
			int networksCreated = 0;
			float frameStartTime = Time.realtimeSinceStartup;

			foreach (var node in nodes)
			{
				if (node != null && !visited.Contains(node))
				{
					// Build network from this node
					var network = BuildNetworkFromNode(node, visited);

					if (network != null && network.Nodes.Count > 0)
					{
						// Register the network
						NetworkManager.Instance.RegisterNetwork(network);
						networksCreated++;

						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Logger.LogInfo($"[ItemConduit] Created network {network.NetworkId.Substring(0, 8)} with {network.Nodes.Count} nodes");
						}
					}

					// Check if we need to yield
					float elapsedMs = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
					if (elapsedMs > MAX_MS_PER_FRAME)
					{
						yield return null;
						frameStartTime = Time.realtimeSinceStartup;
					}
				}
			}

			onComplete?.Invoke(networksCreated);
		}

		/// <summary>
		/// Build a network starting from a specific node
		/// </summary>
		private ConduitNetwork BuildNetworkFromNode(BaseNode startNode, HashSet<BaseNode> visited)
		{
			if (startNode == null) return null;

			ConduitNetwork network = new ConduitNetwork();
			network.NetworkId = Guid.NewGuid().ToString();

			Queue<BaseNode> queue = new Queue<BaseNode>();
			queue.Enqueue(startNode);
			visited.Add(startNode);

			while (queue.Count > 0)
			{
				BaseNode currentNode = queue.Dequeue();
				if (currentNode == null) continue;

				// Add to network
				network.AddNode(currentNode);
				currentNode.SetNetworkId(network.NetworkId);

				// Add connected nodes to queue
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

		/// <summary>
		/// Reactivate nodes using coroutine
		/// </summary>
		private IEnumerator ReactivateNodesCoroutine(HashSet<BaseNode> nodes)
		{
			int processed = 0;
			foreach (var node in nodes)
			{
				if (node != null)
				{
					node.SetActive(true);
					processed++;

					// Yield every few nodes
					if (processed % NODES_PER_FRAME == 0)
					{
						yield return null;
					}
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Reactivated {processed} nodes");
			}
		}

		#endregion

		#region Cleanup

		/// <summary>
		/// Clean up on destruction
		/// </summary>
		private void OnDestroy()
		{
			CancelPendingRebuilds();
		}

		#endregion

		#region Statistics

		/// <summary>
		/// Statistics tracking for rebuild operations
		/// </summary>
		public class RebuildStatistics
		{
			public int TotalRebuilds { get; private set; }
			public int TotalNodesRebuilt { get; private set; }
			public int TotalNetworksAffected { get; private set; }
			public float TotalRebuildTime { get; private set; }
			public float AverageRebuildTime { get; private set; }
			public int ErrorCount { get; private set; }
			public DateTime LastRebuildTime { get; private set; }

			public void RecordRebuild(int nodes, int networks, int newNetworks, float time)
			{
				TotalRebuilds++;
				TotalNodesRebuilt += nodes;
				TotalNetworksAffected += networks;
				TotalRebuildTime += time;
				AverageRebuildTime = TotalRebuildTime / TotalRebuilds;
				LastRebuildTime = DateTime.Now;
			}

			public void RecordError()
			{
				ErrorCount++;
			}

			public override string ToString()
			{
				return $"Rebuilds: {TotalRebuilds}, Nodes: {TotalNodesRebuilt}, Networks: {TotalNetworksAffected}, " +
					   $"Avg Time: {AverageRebuildTime:F2}s, Errors: {ErrorCount}";
			}
		}

		#endregion
	}
}