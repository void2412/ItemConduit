using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.Nodes;
using ItemConduit.Config;
using Logger = Jotunn.Logger;

namespace ItemConduit.Network
{
	/// <summary>
	/// Optimized rebuild manager with batched updates and frame spreading
	/// Prevents FPS drops by distributing work across multiple frames
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

		/// <summary>Maximum nodes to process per frame in connection detection</summary>
		private int CONNECTIONS_PER_FRAME = NetworkPerformanceConfig.nodeProcessPerFrame.Value;

		/// <summary>Maximum nodes to process per frame in network creation</summary>
		private int NETWORKS_PER_FRAME = NetworkPerformanceConfig.networkProcessPerFrame.Value;

		/// <summary>Maximum time in milliseconds per frame for rebuild operations</summary>
		private float MAX_MS_PER_FRAME = NetworkPerformanceConfig.processingTimePerFrame.Value;

		private bool DEBUG_CONFIG = false;


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

			// Don't process ghost pieces
			if (!node.IsValidPlacedNode())
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Ignoring rebuild request for invalid/ghost node: {node.name}");
				}
				return;
			}

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

			// Get all valid nodes from NetworkManager
			var allNodes = NetworkManager.Instance.GetAllNodes();

			foreach (var node in allNodes)
			{
				if (node != null && node.IsValidPlacedNode())
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
		/// Get current statistics as string
		/// </summary>
		public string GetStatistics()
		{
			return statistics.ToString();
		}

		/// <summary>
		/// Check if a rebuild is currently in progress
		/// </summary>
		public bool IsRebuildInProgress()
		{
			return isRebuildInProgress;
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
			rebuildCoroutine = StartCoroutine(OptimizedRebuildCoroutine(delay));
		}

		/// <summary>
		/// Optimized rebuild coroutine that spreads work across frames
		/// </summary>
		private IEnumerator OptimizedRebuildCoroutine(float delay)
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

			// Remove any null or ghost nodes
			nodesNeedingRebuild.RemoveWhere(n => n == null || !n.IsValidPlacedNode());

			// Collect affected nodes (physics-based, more efficient)
			var affectedNodes = CollectAffectedNodesOptimized();

			if (affectedNodes.Count == 0)
			{
				Logger.LogInfo("[ItemConduit] No valid nodes to rebuild");
				isRebuildInProgress = false;
				yield break;
			}

			Logger.LogInfo("[ItemConduit] ========================================");
			Logger.LogInfo($"[ItemConduit] Starting optimized network rebuild");
			Logger.LogInfo($"[ItemConduit] Nodes to rebuild: {affectedNodes.Count}");
			Logger.LogInfo("[ItemConduit] ========================================");

			// Step 1: Clear old network associations
			yield return StartCoroutine(ClearNetworkAssociations(affectedNodes));

			// Step 2: Batch update connections using physics
			yield return StartCoroutine(BatchedConnectionUpdate(affectedNodes));

			// Step 3: Build networks from connected components
			int networksCreated = 0;
			yield return StartCoroutine(BuildNetworksOptimized(affectedNodes, (count) => {
				networksCreated = count;
			}));

			// Step 4: Activate valid networks
			yield return StartCoroutine(ActivateNetworks(affectedNodes));

			// Update statistics
			float rebuildTime = Time.realtimeSinceStartup - startTime;
			statistics.RecordRebuild(affectedNodes.Count, networksCreated, rebuildTime);

			Logger.LogInfo("[ItemConduit] ========================================");
			Logger.LogInfo($"[ItemConduit] Rebuild complete in {rebuildTime:F2}s");
			Logger.LogInfo($"[ItemConduit] Networks created: {networksCreated}");
			Logger.LogInfo("[ItemConduit] ========================================");

			// Clean up
			nodesNeedingRebuild.Clear();
			nodesBeingProcessed.Clear();
			isRebuildInProgress = false;
			rebuildCoroutine = null;
		}

		/// <summary>
		/// Collect affected nodes using physics for efficiency
		/// </summary>
		private HashSet<BaseNode> CollectAffectedNodesOptimized()
		{
			HashSet<BaseNode> affectedNodes = new HashSet<BaseNode>();

			foreach (var node in nodesNeedingRebuild)
			{
				if (node == null || !node.IsValidPlacedNode()) continue;

				// Add the node itself
				affectedNodes.Add(node);

				// Get connected nodes from the node's connection list
				foreach (var connected in node.GetConnectedNodes())
				{
					if (connected != null && connected.IsValidPlacedNode())
					{
						affectedNodes.Add(connected);
					}
				}

			}

			nodesBeingProcessed = new HashSet<BaseNode>(affectedNodes);
			return affectedNodes;
		}

		/// <summary>
		/// Clear network associations for affected nodes
		/// </summary>
		private IEnumerator ClearNetworkAssociations(HashSet<BaseNode> nodes)
		{
			int processed = 0;
			foreach (var node in nodes)
			{
				if (node != null)
				{
					// Get old network ID for cleanup
					string oldNetworkId = node.NetworkId;
					if (!string.IsNullOrEmpty(oldNetworkId))
					{
						NetworkManager.Instance.RemoveNetwork(oldNetworkId);
					}

					// Clear node's network association
					node.SetNetworkId(null);
					node.SetActive(false);

					processed++;

					// Yield every few nodes to prevent frame drops
					if (processed % NETWORKS_PER_FRAME == 0)
					{
						yield return null;
					}
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Cleared network associations for {processed} nodes");
			}
		}

		/// <summary>
		/// Batch update connections using optimized physics detection
		/// </summary>
		private IEnumerator BatchedConnectionUpdate(HashSet<BaseNode> nodes)
		{
			List<BaseNode> nodeList = nodes.ToList();
			int totalNodes = nodeList.Count;

			// Track completion with a HashSet
			HashSet<BaseNode> pendingNodes = new HashSet<BaseNode>();

			for (int i = 0; i < totalNodes; i += CONNECTIONS_PER_FRAME)
			{
				int endIndex = Mathf.Min(i + CONNECTIONS_PER_FRAME, totalNodes);

				// Subscribe to completion events
				for (int j = i; j < endIndex; j++)
				{
					BaseNode node = nodeList[j];
					if (node != null && node.IsValidPlacedNode())
					{
						pendingNodes.Add(node);

						// Subscribe to completion
						void CompletionHandler(BaseNode completedNode)
						{
							pendingNodes.Remove(completedNode);
							completedNode.OnDetectionComplete -= CompletionHandler;
						}

						node.OnDetectionComplete += CompletionHandler;
						node.StartConnectionDetection();
					}
				}

				// Wait for completion or timeout
				float batchTimeout = Time.realtimeSinceStartup + 5f; // Generous timeout

				while (pendingNodes.Count > 0 && Time.realtimeSinceStartup < batchTimeout)
				{
					yield return null; // Keep yielding frames

					// Optional: Remove nodes that are taking too long
					if (Time.frameCount % 60 == 0) // Check every second
					{
						var stuckNodes = pendingNodes.Where(n => n != null && n.gameObject != null).ToList();
						foreach (var stuck in stuckNodes)
						{
							try
							{
								if (stuck != null && stuck.gameObject != null)
								{
									Logger.LogWarning($"[ItemConduit] Node {stuck.name} detection taking long...");
								}
							}
							catch (MissingReferenceException)
							{
								// Node was destroyed, remove from pending
								pendingNodes.Remove(stuck);
							}
						}
					}
				}

				// Handle any remaining timeouts
				if (pendingNodes.Count > 0)
				{
					foreach (var stuck in pendingNodes.ToList()) // Use ToList to avoid modification during iteration
					{
						try
						{
							if (stuck != null && stuck.gameObject != null)
							{
								Logger.LogError($"[ItemConduit] Force completing: {stuck.name}");
								stuck.OnDetectionComplete -= null; // This line also looks wrong
							}
						}
						catch (MissingReferenceException)
						{
							// Node was destroyed
						}
					}
					pendingNodes.Clear();
				}
			}
		}

		/// <summary>
		/// Build networks from connected components efficiently
		/// </summary>
		private IEnumerator BuildNetworksOptimized(HashSet<BaseNode> nodes, System.Action<int> onComplete)
		{
			HashSet<BaseNode> visited = new HashSet<BaseNode>();
			List<ConduitNetwork> newNetworks = new List<ConduitNetwork>();
			float frameStart = Time.realtimeSinceStartup;

			foreach (var node in nodes)
			{
				if (node == null || !node.IsValidPlacedNode() || visited.Contains(node))
					continue;

				// Build network using BFS
				ConduitNetwork network = BuildNetworkFromNode(node, visited);

				if (network != null && network.Nodes.Count > 0)
				{
					newNetworks.Add(network);

					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Logger.LogInfo($"[ItemConduit] Created network {network.NetworkId.Substring(0, 8)} with {network.Nodes.Count} nodes");
					}
				}

				// Check if we need to yield
				float elapsedMs = (Time.realtimeSinceStartup - frameStart) * 1000f;
				if (elapsedMs > MAX_MS_PER_FRAME)
				{
					yield return null;
					frameStart = Time.realtimeSinceStartup;
				}
			}

			// Register all new networks
			foreach (var network in newNetworks)
			{
				NetworkManager.Instance.RegisterNetwork(network);
			}

			onComplete?.Invoke(newNetworks.Count);
		}

		/// <summary>
		/// Build a network using BFS from a starting node
		/// </summary>
		private ConduitNetwork BuildNetworkFromNode(BaseNode startNode, HashSet<BaseNode> visited)
		{
			if (startNode == null || !startNode.IsValidPlacedNode()) return null;

			ConduitNetwork network = new ConduitNetwork();
			network.NetworkId = Guid.NewGuid().ToString();

			Queue<BaseNode> queue = new Queue<BaseNode>();
			queue.Enqueue(startNode);
			visited.Add(startNode);

			while (queue.Count > 0)
			{
				BaseNode currentNode = queue.Dequeue();
				if (currentNode == null || !currentNode.IsValidPlacedNode()) continue;

				// Add to network
				network.AddNode(currentNode);
				currentNode.SetNetworkId(network.NetworkId);

				// Add connected nodes to queue
				foreach (var connectedNode in currentNode.GetConnectedNodes())
				{
					if (connectedNode != null &&
						connectedNode.IsValidPlacedNode() &&
						!visited.Contains(connectedNode))
					{
						visited.Add(connectedNode);
						queue.Enqueue(connectedNode);
					}
				}
			}

			return network;
		}

		/// <summary>
		/// Activate networks with valid configurations
		/// </summary>
		private IEnumerator ActivateNetworks(HashSet<BaseNode> nodes)
		{
			int processed = 0;
			foreach (var node in nodes)
			{
				if (node != null && node.IsValidPlacedNode())
				{
					// Check if the node's network is valid
					string networkId = node.NetworkId;
					if (!string.IsNullOrEmpty(networkId))
					{
						node.SetActive(true);
					}

					processed++;

					// Yield periodically
					if (processed % NETWORKS_PER_FRAME == 0)
					{
						yield return null;
					}
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Activated {processed} nodes");
			}
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
			public int TotalNetworksCreated { get; private set; }
			public float TotalRebuildTime { get; private set; }
			public float AverageRebuildTime { get; private set; }
			public DateTime LastRebuildTime { get; private set; }
			public float FastestRebuild { get; private set; } = float.MaxValue;
			public float SlowestRebuild { get; private set; } = 0f;

			public void RecordRebuild(int nodes, int networks, float time)
			{
				TotalRebuilds++;
				TotalNodesRebuilt += nodes;
				TotalNetworksCreated += networks;
				TotalRebuildTime += time;
				AverageRebuildTime = TotalRebuildTime / TotalRebuilds;
				LastRebuildTime = DateTime.Now;

				if (time < FastestRebuild)
					FastestRebuild = time;
				if (time > SlowestRebuild)
					SlowestRebuild = time;
			}

			public override string ToString()
			{
				return $"Rebuilds: {TotalRebuilds}, Nodes: {TotalNodesRebuilt}, Networks: {TotalNetworksCreated}\n" +
					   $"Avg Time: {AverageRebuildTime:F2}s, Fastest: {FastestRebuild:F2}s, Slowest: {SlowestRebuild:F2}s";
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
	}
}