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

		/// <summary>Delay before rebuilding to batch multiple node placements</summary>
		private const float REBUILD_DELAY = 0.5f;

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
				Debug.Log("[ItemConduit] NetworkManager transfer loop started");
			}
			else
			{
				Debug.Log("[ItemConduit] NetworkManager initialized (client mode - no transfers)");
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

			// Clear all data
			if (networks != null)
				networks.Clear();

			if (allNodes != null)
				allNodes.Clear();

			Debug.Log("[ItemConduit] NetworkManager shutdown complete");
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
					Debug.Log($"[ItemConduit] Registered node: {node.name} (Total: {allNodes.Count})");
				}

				RequestNetworkRebuild();
			}
		}

		/// <summary>
		/// Unregister a node from the network manager
		/// </summary>
		public void UnregisterNode(BaseNode node)
		{
			if (node == null) return;
			if (!ZNet.instance.IsServer()) return;

			if (allNodes != null && allNodes.Remove(node))
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Unregistered node: {node.name} (Remaining: {allNodes.Count})");
				}

				RequestNetworkRebuild();
			}
		}

		#endregion

		#region Network Building

		/// <summary>
		/// Request a network rebuild
		/// </summary>
		public void RequestNetworkRebuild()
		{
			if (!ZNet.instance.IsServer()) return;

			rebuildRequested = true;

			// Start rebuild coroutine if not already running
			if (!isRebuildingNetworks)
			{
				StartCoroutine(RebuildNetworksCoroutine());
			}
		}

		/// <summary>
		/// Coroutine to rebuild all networks
		/// </summary>
		private IEnumerator RebuildNetworksCoroutine()
		{
			// Wait for batch delay to collect multiple node placements
			yield return new WaitForSeconds(REBUILD_DELAY);

			// Check if rebuild is still needed
			if (!rebuildRequested || isRebuildingNetworks) yield break;

			rebuildRequested = false;
			isRebuildingNetworks = true;

			Debug.Log("[ItemConduit] ========================================");
			Debug.Log("[ItemConduit] Starting network rebuild...");
			Debug.Log("[ItemConduit] ========================================");

			try
			{
				// Ensure collections exist
				if (networks == null)
					networks = new Dictionary<string, ConduitNetwork>();

				if (allNodes == null)
					allNodes = new HashSet<BaseNode>();

				// Log all registered nodes
				Debug.Log($"[ItemConduit] Registered nodes count: {allNodes.Count}");
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						Debug.Log($"[ItemConduit] Registered: {node.name} - Type: {node.NodeType}, Pos: {node.transform.position}");
					}
				}

				// Deactivate all nodes during rebuild
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						node.SetActive(false);
						node.SetNetworkId(null); // Clear network assignment
					}
				}

				// Clear existing networks
				networks.Clear();

				// Remove any null nodes
				allNodes.RemoveWhere(n => n == null);

				// Step 1: Have all nodes find their connections
				Debug.Log($"[ItemConduit] === STEP 1: Finding connections for {allNodes.Count} nodes ===");
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						Debug.Log($"[ItemConduit] Processing node: {node.name}");
						node.FindConnections();
					}
				}

				// Step 2: Build networks from connected components
				Debug.Log("[ItemConduit] === STEP 2: Building networks from connected components ===");
				HashSet<BaseNode> visited = new HashSet<BaseNode>();
				int networkCount = 0;

				foreach (var node in allNodes)
				{
					if (node != null && !visited.Contains(node))
					{
						Debug.Log($"[ItemConduit] Building network from unvisited node: {node.name}");
						ConduitNetwork network = BuildNetworkFromNode(node, visited);

						if (network != null && network.Nodes.Count > 0)
						{
							string networkId = Guid.NewGuid().ToString();
							network.NetworkId = networkId;
							networks[networkId] = network;
							networkCount++;

							// Set network ID on all nodes
							foreach (var netNode in network.Nodes)
							{
								if (netNode != null)
								{
									netNode.SetNetworkId(networkId);
								}
							}

							Debug.Log($"[ItemConduit] Created network {networkId.Substring(0, 8)} with {network.Nodes.Count} nodes " +
									 $"({network.ExtractNodes.Count} extract, {network.InsertNodes.Count} insert, {network.ConduitNodes.Count} conduit)");

							// Log all nodes in this network
							foreach (var netNode in network.Nodes)
							{
								if (netNode != null)
								{
									Debug.Log($"[ItemConduit]   - {netNode.name} (Type: {netNode.NodeType})");
								}
							}
						}
					}
				}

				// Step 3: Reactivate all nodes
				Debug.Log("[ItemConduit] === STEP 3: Reactivating nodes ===");
				foreach (var node in allNodes)
				{
					if (node != null)
					{
						node.SetActive(true);
					}
				}

				Debug.Log("[ItemConduit] ========================================");
				Debug.Log($"[ItemConduit] Network rebuild complete. {networkCount} networks active with {allNodes.Count} total nodes.");
				Debug.Log("[ItemConduit] ========================================");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ItemConduit] Error during network rebuild: {ex.Message}\n{ex.StackTrace}");
			}
			finally
			{
				isRebuildingNetworks = false;
			}
		}

		/// <summary>
		/// Build a network starting from a specific node using BFS
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

				// Add to network
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

			// Log the network composition for debugging
			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Built network from {startNode.name}: " +
						 $"{network.Nodes.Count} nodes total");
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
				// Check if network has both extract and insert nodes
				if (network.ExtractNodes.Count == 0 || network.InsertNodes.Count == 0)
				{
					return;
				}

				// Process each extract node
				foreach (var extractNode in network.ExtractNodes)
				{
					if (extractNode == null || !extractNode.IsActive) continue;

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
										Debug.Log($"[ItemConduit] Transferred 1x {item.m_shared.m_name} from {extractNode.name} to {insertNode.name}");
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
				Debug.LogError($"[ItemConduit] Error processing network transfers: {ex.Message}");
			}
		}

		#endregion
	}
}