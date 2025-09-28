using ItemConduit.Core;
using ItemConduit.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Base class for all conduit nodes
	/// Handles both connection detection and container detection
	/// </summary>
	public abstract class BaseNode : MonoBehaviour, Interactable, Hoverable
	{
		#region Properties

		/// <summary>Type of this node (Conduit/Extract/Insert)</summary>
		public NodeType NodeType { get; set; }

		/// <summary>Length of the node in meters</summary>
		private float nodeLength = 0f;
		public float NodeLength
		{
			get { return nodeLength; }
			set { nodeLength = value; }
		}

		/// <summary>ID of the network this node belongs to</summary>
		public string NetworkId { get; set; }

		/// <summary>Whether this node is currently active</summary>
		public bool IsActive { get; protected set; }

		/// <summary>List of nodes connected to this one</summary>
		protected List<BaseNode> connectedNodes = new List<BaseNode>();

		/// <summary>Network view component for multiplayer sync</summary>
		protected ZNetView zNetView;

		/// <summary>Piece component for building system integration</summary>
		protected Piece piece;

		/// <summary>Flag to prevent multiple detection updates</summary>
		private bool isUpdatingDetection = false;

		/// <summary>Coroutine reference for unified detection</summary>
		private Coroutine detectionCoroutine;

		/// <summary>Flag to indicate if this is a ghost/preview piece</summary>
		private bool isGhostPiece = false;

		#endregion

		#region Container Management

		/// <summary>Reference to connected container (null for Conduit nodes)</summary>
		protected Container targetContainer;

		/// <summary>Whether this node type can connect to containers</summary>
		protected virtual bool CanConnectToContainers => false;

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Initialize component references and setup
		/// </summary>
		protected virtual void Awake()
		{
			// Get required components
			zNetView = GetComponent<ZNetView>();
			piece = GetComponent<Piece>();

			// Check if this is a ghost/preview piece
			CheckIfGhost();

			// Initialize node length if not set
			if (nodeLength == 0)
			{
				// Try to determine from the object name
				if (name.Contains("1m"))
				{
					nodeLength = 1f;
				}
				else if (name.Contains("2m"))
				{
					nodeLength = 2f;
				}
				else if (name.Contains("4m"))
				{
					nodeLength = 4f;
				}
				else
				{
					nodeLength = 1f; // Default to 1 meter
				}
			}

			// Register network RPCs if valid and not a ghost
			if (!isGhostPiece && zNetView != null && zNetView.IsValid())
			{
				// Register RPC for network ID synchronization
				zNetView.Register<string>("RPC_UpdateNetworkId", RPC_UpdateNetworkId);
				// Register RPC for active state synchronization
				zNetView.Register<bool>("RPC_SetActive", RPC_SetActive);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Node {name} awakened (Type: {NodeType}, Length: {nodeLength}m, Ghost: {isGhostPiece})");
			}
		}

		/// <summary>
		/// Check if this is a ghost/preview piece
		/// </summary>
		private void CheckIfGhost()
		{
			// Check for ghost layer
			if (gameObject.layer == LayerMask.NameToLayer("ghost"))
			{
				isGhostPiece = true;
				return;
			}

			// Check for placement ghost indicator
			if (name.Contains("(Clone)") && GetComponent<ZNetView>() != null && !GetComponent<ZNetView>().IsValid())
			{
				isGhostPiece = true;
				return;
			}

			// Check if ZNetView is invalid (common for ghosts)
			if (zNetView == null || !zNetView.IsValid())
			{
				// But only if we're not still loading
				if (Time.time > 1f)
				{
					isGhostPiece = true;
				}
			}
		}

		/// <summary>
		/// Register node with network manager on start
		/// </summary>
		protected virtual void Start()
		{
			// Don't process ghost pieces
			if (isGhostPiece)
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping initialization for ghost piece: {name}");
				}
				return;
			}

			// Add a small delay to ensure everything is initialized
			StartCoroutine(DelayedStart());
		}

		/// <summary>
		/// Delayed start to ensure proper initialization
		/// </summary>
		private IEnumerator DelayedStart()
		{
			// Wait a frame to ensure everything is set up
			yield return null;

			// Re-check ghost status after delay
			CheckIfGhost();
			if (isGhostPiece)
			{
				yield break;
			}

			// Only register on server
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				// Register with network manager
				NetworkManager.Instance.RegisterNode(this);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {name} registered with NetworkManager");
				}
			}

			// Start unified detection after a short delay
			yield return new WaitForSeconds(0.5f);
			StartUnifiedDetection();
		}

		/// <summary>
		/// Cleanup on node destruction
		/// </summary>
		protected virtual void OnDestroy()
		{
			// Skip cleanup for ghost pieces
			if (isGhostPiece) return;

			// Stop any running coroutines
			if (detectionCoroutine != null)
			{
				StopCoroutine(detectionCoroutine);
				detectionCoroutine = null;
			}

			// Remove this node from all connected nodes
			foreach (var connectedNode in connectedNodes.ToList())
			{
				if (connectedNode != null)
				{
					connectedNode.RemoveConnection(this);
				}
			}

			// Clear container reference
			targetContainer = null;

			// Unregister from network manager
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				NetworkManager.Instance.UnregisterNode(this);
			}
		}

		#endregion

		#region Unified Detection System

		/// <summary>
		/// Start unified detection for both connections and containers
		/// </summary>
		public void StartUnifiedDetection()
		{
			if (isGhostPiece) return;

			if (detectionCoroutine != null)
			{
				StopCoroutine(detectionCoroutine);
			}
			detectionCoroutine = StartCoroutine(UnifiedDetectionCoroutine());
		}

		/// <summary>
		/// Unified coroutine for detecting both connections and containers
		/// </summary>
		private IEnumerator UnifiedDetectionCoroutine()
		{
			if (isUpdatingDetection) yield break;
			isUpdatingDetection = true;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Starting unified detection for {name} (Type: {NodeType})");
			}

			// Small delay to let physics settle after placement
			yield return new WaitForSeconds(0.1f);

			// Get bounds once for both detections
			Bounds nodeBounds = GetNodeBounds();

			// Perform single physics overlap query for efficiency
			Collider[] overlaps = Physics.OverlapBox(
				nodeBounds.center,
				nodeBounds.extents * 1.2f, // Slightly expanded
				transform.rotation,
				LayerMask.GetMask("piece", "piece_nonsolid", "item", "Default_small")
			);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Found {overlaps.Length} overlapping colliders");
			}

			// Process node connections (all node types)
			yield return StartCoroutine(ProcessNodeConnections(overlaps, nodeBounds));

			// Process container connections (only Extract/Insert nodes)
			if (CanConnectToContainers)
			{
				yield return StartCoroutine(ProcessContainerConnection(overlaps, nodeBounds));
			}

			// Update visual state based on connections
			bool hasConnections = connectedNodes.Count > 0 || (CanConnectToContainers && targetContainer != null);
			UpdateVisualState(hasConnections);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} detection complete: {connectedNodes.Count} nodes, " +
							 $"container: {(targetContainer != null ? targetContainer.m_name : "none")}");
			}

			isUpdatingDetection = false;
		}

		/// <summary>
		/// Process node-to-node connections
		/// </summary>
		private IEnumerator ProcessNodeConnections(Collider[] overlaps, Bounds nodeBounds)
		{
			// Store old connections for cleanup
			var oldConnections = new List<BaseNode>(connectedNodes);
			connectedNodes.Clear();

			// Method 1: Check snappoint overlaps
			var snapPoints = GetSnapPoints();

			foreach (var snapPoint in snapPoints)
			{
				// Use a small overlap sphere at each snappoint
				Collider[] snapOverlaps = Physics.OverlapSphere(
					snapPoint.position,
					0.15f, // Very small radius for precise snap detection
					LayerMask.GetMask("piece", "piece_nonsolid")
				);

				foreach (var col in snapOverlaps)
				{
					if (col == null || col.transform == transform) continue;

					BaseNode otherNode = col.GetComponentInParent<BaseNode>();
					if (otherNode != null && otherNode != this && !otherNode.isGhostPiece)
					{
						// Check if their snappoints are aligned
						var otherSnaps = otherNode.GetSnapPoints();
						foreach (var otherSnap in otherSnaps)
						{
							float dist = Vector3.Distance(snapPoint.position, otherSnap.position);
							if (dist < 0.2f) // Tight threshold for snapped connections
							{
								if (!connectedNodes.Contains(otherNode))
								{
									EstablishConnection(otherNode);
									if (ItemConduitMod.ShowDebugInfo.Value)
									{
										Logger.LogInfo($"[ItemConduit] Snap connection: {name} <-> {otherNode.name} (dist: {dist:F3}m)");
									}
								}
								break;
							}
						}
					}
				}

				// Yield every few snappoints to prevent frame drops
				yield return null;
			}

			// Method 2: Check physical overlaps if no snap connections found
			if (connectedNodes.Count == 0)
			{
				foreach (var col in overlaps)
				{
					if (col == null || col.transform == transform) continue;

					BaseNode otherNode = col.GetComponentInParent<BaseNode>();
					if (otherNode != null && otherNode != this && !otherNode.isGhostPiece && !connectedNodes.Contains(otherNode))
					{
						if (CanConnectTo(otherNode))
						{
							Bounds otherBounds = otherNode.GetNodeBounds();
							if (nodeBounds.Intersects(otherBounds))
							{
								EstablishConnection(otherNode);
								if (ItemConduitMod.ShowDebugInfo.Value)
								{
									Logger.LogInfo($"[ItemConduit] Bounds connection: {name} <-> {otherNode.name}");
								}
							}
						}
					}
				}
			}

			// Notify old connections that are no longer connected
			foreach (var oldNode in oldConnections)
			{
				if (oldNode != null && !connectedNodes.Contains(oldNode))
				{
					oldNode.RemoveConnection(this);
				}
			}

			yield return null;
		}

		/// <summary>
		/// Process container connections (virtual for override in subclasses)
		/// </summary>
		protected virtual IEnumerator ProcessContainerConnection(Collider[] overlaps, Bounds nodeBounds)
		{
			// Base implementation - find container but don't store it
			Container bestContainer = FindBestOverlappingContainer(overlaps, nodeBounds);

			if (bestContainer != null && ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} found container '{bestContainer.m_name}' but not connecting (node type: {NodeType})");
			}

			yield return null;
		}

		/// <summary>
		/// Find the best overlapping container from collision results
		/// </summary>
		protected Container FindBestOverlappingContainer(Collider[] overlaps, Bounds nodeBounds)
		{
			float closestDistance = float.MaxValue;
			Container bestContainer = null;

			foreach (var col in overlaps)
			{
				if (col == null || col.transform == transform) continue;

				// Try multiple ways to find container
				Container container = col.GetComponent<Container>()
					?? col.GetComponentInParent<Container>()
					?? col.GetComponentInChildren<Container>();

				if (container != null)
				{
					// Get the container's collider for bounds checking
					Collider containerCollider = container.GetComponent<Collider>()
						?? container.GetComponentInChildren<Collider>();

					if (containerCollider != null)
					{
						Bounds containerBounds = containerCollider.bounds;

						// Check for actual intersection
						if (nodeBounds.Intersects(containerBounds))
						{
							float distance = Vector3.Distance(transform.position, container.transform.position);

							if (ItemConduitMod.ShowDebugInfo.Value)
							{
								Logger.LogInfo($"[ItemConduit] Found overlapping container: {container.name} ({container.m_name}) at distance {distance:F2}m");

								Inventory inv = container.GetInventory();
								if (inv != null)
								{
									Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots, {inv.GetAllItems().Count} items");
								}
							}

							if (distance < closestDistance)
							{
								closestDistance = distance;
								bestContainer = container;
							}
						}
					}
				}
			}

			// Also check raycast downward if no container found
			if (bestContainer == null)
			{
				RaycastHit hit;
				if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f,
					LayerMask.GetMask("piece", "piece_nonsolid", "Default_small")))
				{
					Container container = hit.collider.GetComponent<Container>()
						?? hit.collider.GetComponentInParent<Container>();

					if (container != null && container.GetInventory() != null)
					{
						bestContainer = container;

						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Logger.LogInfo($"[ItemConduit] Found container below node: {container.name} ({container.m_name})");
						}
					}
				}
			}

			return bestContainer;
		}

		#endregion

		#region Connection Management

		/// <summary>
		/// Legacy FindConnections method - now uses unified detection
		/// </summary>
		public virtual void FindConnections()
		{
			StartUnifiedDetection();
		}

		/// <summary>
		/// Start connection detection (redirects to unified detection)
		/// </summary>
		public void StartConnectionDetection()
		{
			StartUnifiedDetection();
		}

		/// <summary>
		/// Get the bounds of this node for collision detection
		/// </summary>
		protected Bounds GetNodeBounds()
		{
			// Get all colliders on this node
			Collider[] colliders = GetComponentsInChildren<Collider>();

			if (colliders.Length == 0)
			{
				// Fallback to calculated bounds based on node length
				return new Bounds(transform.position, new Vector3(0.3f, 0.3f, nodeLength));
			}

			// Find first non-trigger collider
			Collider mainCollider = null;
			foreach (var col in colliders)
			{
				if (!col.isTrigger)
				{
					mainCollider = col;
					break;
				}
			}

			if (mainCollider != null)
			{
				return mainCollider.bounds;
			}

			// If all colliders are triggers, use the first one
			return colliders[0].bounds;
		}

		/// <summary>
		/// Establish a bidirectional connection between nodes
		/// </summary>
		private void EstablishConnection(BaseNode otherNode)
		{
			if (otherNode == null || otherNode.isGhostPiece) return;

			AddConnection(otherNode);
			otherNode.AddConnection(this);

			// Visual feedback for successful connection
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				CreateConnectionEffect(transform.position, otherNode.transform.position);
			}
		}

		/// <summary>
		/// Get connection points for this node
		/// </summary>
		public virtual Vector3[] GetConnectionPoints()
		{
			// Find snap points
			Transform[] snapPoints = GetSnapPoints();

			if (snapPoints.Length > 0)
			{
				// Return world positions of snap points
				Vector3[] points = new Vector3[snapPoints.Length];
				for (int i = 0; i < snapPoints.Length; i++)
				{
					points[i] = snapPoints[i].position;
				}
				return points;
			}

			// Fallback: create connection points based on node length and orientation
			Vector3[] fallbackPoints = new Vector3[2];
			Vector3 forward = transform.forward;

			// Start point (back)
			fallbackPoints[0] = transform.position - forward * (nodeLength / 2f);

			// End point (front)
			fallbackPoints[1] = transform.position + forward * (nodeLength / 2f);

			return fallbackPoints;
		}

		/// <summary>
		/// Get snap point transforms if they exist
		/// </summary>
		private Transform[] GetSnapPoints()
		{
			List<Transform> snapPoints = new List<Transform>();

			foreach (Transform child in transform)
			{
				if (child.tag == "snappoint")
				{
					snapPoints.Add(child);
				}
			}

			// Sort by name to ensure consistent ordering
			snapPoints.Sort((a, b) => a.name.CompareTo(b.name));

			return snapPoints.ToArray();
		}

		/// <summary>
		/// Add a connection to another node (without reciprocating)
		/// </summary>
		public void AddConnection(BaseNode node)
		{
			if (node != null && node != this && !node.isGhostPiece && !connectedNodes.Contains(node))
			{
				connectedNodes.Add(node);
			}
		}

		/// <summary>
		/// Remove a connection to another node (without reciprocating)
		/// </summary>
		public void RemoveConnection(BaseNode node)
		{
			if (node != null)
			{
				connectedNodes.Remove(node);
			}
		}

		/// <summary>
		/// Check if this node can connect to another node based on type rules
		/// </summary>
		protected virtual bool CanConnectTo(BaseNode other)
		{
			if (other == null || other == this || other.isGhostPiece) return false;

			// Connection rules based on node type
			switch (NodeType)
			{
				case NodeType.Conduit:
					// Conduits connect to everything
					return true;

				case NodeType.Extract:
					// Extract nodes connect to conduits and insert nodes
					return other.NodeType == NodeType.Conduit || other.NodeType == NodeType.Insert;

				case NodeType.Insert:
					// Insert nodes connect to conduits and extract nodes
					return other.NodeType == NodeType.Conduit || other.NodeType == NodeType.Extract;

				default:
					return false;
			}
		}

		/// <summary>
		/// Get a copy of the connected nodes list
		/// </summary>
		public List<BaseNode> GetConnectedNodes()
		{
			return new List<BaseNode>(connectedNodes.Where(n => n != null && !n.isGhostPiece));
		}

		/// <summary>
		/// Check if this is a valid placed node (not a ghost)
		/// </summary>
		public bool IsValidPlacedNode()
		{
			return !isGhostPiece && zNetView != null && zNetView.IsValid();
		}

		#endregion

		#region Container Management

		/// <summary>
		/// Get the target container (virtual for override)
		/// </summary>
		public virtual Container GetTargetContainer()
		{
			// Base implementation returns null (Conduit nodes don't have containers)
			return null;
		}

		/// <summary>
		/// Force refresh all detections
		/// </summary>
		public void RefreshDetection()
		{
			if (IsValidPlacedNode())
			{
				StartUnifiedDetection();
			}
		}

		#endregion

		#region Visual Effects

		/// <summary>
		/// Create a visual effect for successful connections
		/// </summary>
		private void CreateConnectionEffect(Vector3 point1, Vector3 point2)
		{
			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Create a temporary particle effect or flash at the connection point
			Vector3 connectionPoint = (point1 + point2) / 2f;

			GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			effect.name = "connection_flash";
			effect.transform.position = connectionPoint;
			effect.transform.localScale = Vector3.one * 0.3f;

			Destroy(effect.GetComponent<Collider>());

			var renderer = effect.GetComponent<Renderer>();
			if (renderer != null)
			{
				renderer.material = new Material(Shader.Find("Sprites/Default"));
				renderer.material.color = Color.yellow;
			}

			// Destroy after a short time
			Destroy(effect, 0.5f);
		}

		/// <summary>
		/// Update visual indicators based on active state
		/// </summary>
		protected virtual void UpdateVisualState(bool active)
		{
			// Skip for ghost pieces
			if (isGhostPiece) return;

			IsActive = active;

			// Base implementation - can be overridden for visual effects
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				// Update material or particle effects based on active state
				MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
				if (renderer != null)
				{
					if (active)
					{
						renderer.material.EnableKeyword("_EMISSION");
						// Use appropriate color based on node type
						Color emissionColor = NodeType switch
						{
							NodeType.Extract => Color.green * 0.3f,
							NodeType.Insert => Color.blue * 0.3f,
							NodeType.Conduit => Color.gray * 0.5f,
							_ => Color.white * 0.3f
						};
						renderer.material.SetColor("_EmissionColor", emissionColor);
					}
					else
					{
						renderer.material.DisableKeyword("_EMISSION");
					}
				}
			}
		}

		#endregion

		#region Network Management

		/// <summary>
		/// Set the network ID for this node and sync to clients
		/// </summary>
		public virtual void SetNetworkId(string networkId)
		{
			NetworkId = networkId;

			// Sync to all clients
			if (!isGhostPiece && zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdateNetworkId", networkId ?? "");
			}
		}

		/// <summary>
		/// RPC handler for network ID updates from server
		/// </summary>
		protected virtual void RPC_UpdateNetworkId(long sender, string networkId)
		{
			NetworkId = string.IsNullOrEmpty(networkId) ? null : networkId;
		}

		/// <summary>
		/// Set the active state of this node and sync to clients
		/// </summary>
		public virtual void SetActive(bool active)
		{
			IsActive = active;

			// Sync to all clients
			if (!isGhostPiece && zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_SetActive", active);
			}

			// Visual feedback for active state
			UpdateVisualState(active);
		}

		/// <summary>
		/// RPC handler for active state updates from server
		/// </summary>
		protected virtual void RPC_SetActive(long sender, bool active)
		{
			IsActive = active;
			UpdateVisualState(active);
		}

		#endregion

		#region Hoverable Interface

		/// <summary>
		/// Get hover text displayed when looking at node
		/// </summary>
		public virtual string GetHoverText()
		{
			if (isGhostPiece) return "";

			string status = IsActive ? "<color=green>Active</color>" : "<color=red>Inactive</color>";
			string network = !string.IsNullOrEmpty(NetworkId) ? NetworkId.Substring(0, Math.Min(8, NetworkId.Length)) : "No Network";

			// Show connection count
			int connectionCount = connectedNodes.Count(n => n != null && !n.isGhostPiece);
			string connectionInfo = connectionCount > 0 ? $"Connections: {connectionCount}" : "No Connections";

			return $"{GetNodeTypeName()}\n[Network: {network}]\n[Status: {status}]\n[{connectionInfo}]";
		}

		/// <summary>
		/// Get hover name for the node
		/// </summary>
		public virtual string GetHoverName()
		{
			return GetNodeTypeName();
		}

		/// <summary>
		/// Get display name for node type
		/// </summary>
		protected string GetNodeTypeName()
		{
			return NodeType switch
			{
				NodeType.Conduit => $"<color=gray>Conduit Node</color>",
				NodeType.Extract => $"<color=green>Extract Node</color>",
				NodeType.Insert => $"<color=blue>Insert Node</color>",
				_ => "Unknown Node"
			};
		}

		#endregion

		#region Interactable Interface

		/// <summary>
		/// Handle player interaction with the node
		/// </summary>
		public virtual bool Interact(Humanoid user, bool hold, bool alt)
		{
			// Base nodes don't have interactions
			return false;
		}

		/// <summary>
		/// Handle item use on node
		/// </summary>
		public virtual bool UseItem(Humanoid user, ItemDrop.ItemData item)
		{
			return false;
		}

		#endregion

		#region Debug Helpers

		/// <summary>
		/// Draw debug gizmos in editor/debug mode
		/// </summary>
		protected virtual void OnDrawGizmos()
		{
			if (!ItemConduitMod.ShowDebugInfo.Value) return;

			// Draw node center position
			Gizmos.color = NodeType switch
			{
				NodeType.Conduit => Color.gray,
				NodeType.Extract => Color.green,
				NodeType.Insert => Color.blue,
				_ => Color.white
			};
			Gizmos.DrawWireSphere(transform.position, 0.2f);

			// Draw connections to other nodes
			if (connectedNodes != null)
			{
				Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Semi-transparent yellow
				foreach (var node in connectedNodes)
				{
					if (node != null)
					{
						Gizmos.DrawLine(transform.position, node.transform.position);
					}
				}
			}

			// Draw container connection for Extract/Insert nodes
			if (targetContainer != null && CanConnectToContainers)
			{
				Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan for container connections
				Gizmos.DrawLine(transform.position, targetContainer.transform.position);
			}

			// Draw node bounds
			Bounds bounds = GetNodeBounds();
			Gizmos.color = new Color(0, 1, 0, 0.3f);
			Gizmos.DrawWireCube(bounds.center, bounds.size);
		}

		#endregion
	}
}