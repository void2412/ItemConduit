using ItemConduit.Core;
using ItemConduit.Network;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemConduit.Nodes
{

	

	/// <summary>
	/// Base class for all conduit nodes
	/// Handles common functionality like connections, network registration, and hover text
	/// </summary>
	public abstract class BaseNode : MonoBehaviour, Interactable, Hoverable
	{
		#region Properties

		/// <summary>Type of this node (Conduit/Extract/Insert)</summary>
		public NodeType NodeType { get; set; }

		/// <summary>Length of the node in meters</summary>
		private float nodeLength = 1f;
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

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Initialize component references and register RPCs
		/// Called when the node is first created
		/// </summary>
		protected virtual void Awake()
		{
			// Get required components
			zNetView = GetComponent<ZNetView>();
			piece = GetComponent<Piece>();

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

			// Register network RPCs if valid
			if (zNetView != null && zNetView.IsValid())
			{
				// Register RPC for network ID synchronization
				zNetView.Register<string>("RPC_UpdateNetworkId", RPC_UpdateNetworkId);

				// Register RPC for active state synchronization
				zNetView.Register<bool>("RPC_SetActive", RPC_SetActive);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Node {name} awakened (Type: {NodeType}, Length: {nodeLength}m)");
			}
		}

		/// <summary>
		/// Register node with network manager on start
		/// Only runs on server/host
		/// </summary>
		protected virtual void Start()
		{
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

			// Only register on server
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				// Register with network manager
				ItemConduit.Network.NetworkManager.Instance.RegisterNode(this);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Node {name} registered with NetworkManager (Type: {NodeType}, Length: {nodeLength}m, Pos: {transform.position})");
				}
			}
		}

		/// <summary>
		/// Cleanup on node destruction
		/// Unregisters from network manager
		/// </summary>
		protected virtual void OnDestroy()
		{
			// Remove this node from all connected nodes
			foreach (var connectedNode in connectedNodes.ToList())
			{
				if (connectedNode != null)
				{
					connectedNode.RemoveConnection(this);
				}
			}

			// Unregister from network manager
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				ItemConduit.Network.NetworkManager.Instance.UnregisterNode(this);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Node {name} unregistered from NetworkManager");
				}
			}
		}

		#endregion

		#region Connection Management
		/// <summary>
		/// Get connection points for this node
		/// These are the positions where other nodes can connect
		/// </summary>
		/// <returns>Array of world positions for connection points</returns>
		public virtual Vector3[] GetConnectionPoints()
		{
			// Find snap points created during node registration
			Transform[] snapPoints = GetSnapPoints();

			if (snapPoints.Length > 0)
			{
				// Return world positions of snap points
				Vector3[] points = new Vector3[snapPoints.Length];
				for (int i = 0; i < snapPoints.Length; i++)
				{
					points[i] = snapPoints[i].position;
				}

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] {name} GetConnectionPoints: Using {points.Length} snappoints");
					for (int i = 0; i < points.Length; i++)
					{
						Debug.Log($"[ItemConduit]   Snappoint {i}: {points[i]}");
					}
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

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] {name} GetConnectionPoints: Using calculated fallback points");
				Debug.Log($"[ItemConduit]   Back point: {fallbackPoints[0]}");
				Debug.Log($"[ItemConduit]   Front point: {fallbackPoints[1]}");
			}

			return fallbackPoints;
		}

		/// <summary>
		/// Find and establish connections to nearby nodes
		/// Any snappoint can connect to any other snappoint
		/// </summary>
		public virtual void FindConnections()
		{
			Debug.Log($"[ItemConduit] === FindConnections START for {name} ===");
			Debug.Log($"[ItemConduit] Node details - Type: {NodeType}, Length: {nodeLength}m, Position: {transform.position}");

			// Clear existing connections (but notify connected nodes)
			foreach (var node in connectedNodes.ToList())
			{
				if (node != null)
				{
					node.RemoveConnection(this);
				}
			}
			connectedNodes.Clear();

			// Get all my connection points (snappoints)
			Vector3[] myPoints = GetConnectionPoints();
			Debug.Log($"[ItemConduit] My connection points: {myPoints.Length} snappoints found");
			for (int i = 0; i < myPoints.Length; i++)
			{
				Debug.Log($"[ItemConduit]   Point {i}: {myPoints[i]}");
			}

			// Find all other nodes in the scene
			BaseNode[] allNodesInScene = FindObjectsOfType<BaseNode>();
			Debug.Log($"[ItemConduit] Total BaseNodes in scene: {allNodesInScene.Length}");

			// Connection threshold - very tight for snap connections
			float connectionThreshold = 0.1f; // 10cm tolerance for snapped connections

			// Check each node for connections
			foreach (var otherNode in allNodesInScene)
			{
				if (otherNode == null || otherNode == this) continue;

				// Skip if already connected
				if (connectedNodes.Contains(otherNode)) continue;

				// Get the other node's connection points
				Vector3[] otherPoints = otherNode.GetConnectionPoints();

				bool connected = false;
				string connectionDetail = "";

				// Check all point combinations - any snappoint can connect to any other
				float closestDistance = float.MaxValue;
				int myClosestIndex = -1;
				int otherClosestIndex = -1;

				for (int i = 0; i < myPoints.Length; i++)
				{
					for (int j = 0; j < otherPoints.Length; j++)
					{
						float distance = Vector3.Distance(myPoints[i], otherPoints[j]);

						if (distance < closestDistance)
						{
							closestDistance = distance;
							myClosestIndex = i;
							otherClosestIndex = j;
						}

						// Check if close enough to connect
						if (distance <= connectionThreshold)
						{
							connectionDetail = $"Point {i} to Point {j} (dist: {distance:F3}m)";

							if (CanConnectTo(otherNode))
							{
								AddConnection(otherNode);
								otherNode.AddConnection(this);
								connected = true;

								// Visual feedback for successful connection
								if (ItemConduitMod.EnableVisualEffects.Value)
								{
									CreateConnectionEffect(myPoints[i], otherPoints[j]);
								}

								Debug.Log($"[ItemConduit] *** CONNECTED: {name} to {otherNode.name} via {connectionDetail} ***");
								break; // Connection made, stop checking this node
							}
						}
					}
					if (connected) break;
				}

				// Log why connection failed if in debug mode
				if (!connected && ItemConduitMod.ShowDebugInfo.Value && closestDistance < 1f)
				{
					if (closestDistance > connectionThreshold)
					{
						Debug.Log($"[ItemConduit] {name} close but not connected to {otherNode.name} (closest: {closestDistance:F3}m > {connectionThreshold}m)");
						if (myClosestIndex >= 0 && otherClosestIndex >= 0)
						{
							Debug.Log($"[ItemConduit]   Closest points: My point {myClosestIndex} to their point {otherClosestIndex}");
						}
					}
				}
			}

			Debug.Log($"[ItemConduit] === FindConnections END: {name} has {connectedNodes.Count} connections ===");

			// Update visual state after connections are established
			UpdateVisualState(connectedNodes.Count > 0);
		}

		/// <summary>
		/// Create a visual effect for successful snappoint connections
		/// </summary>
		private void CreateConnectionEffect(Vector3 point1, Vector3 point2)
		{
			// Create a temporary particle effect or flash at the connection point
			Vector3 connectionPoint = (point1 + point2) / 2f;

			GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			effect.name = "connection_flash";
			effect.transform.position = connectionPoint;
			effect.transform.localScale = Vector3.one * 0.5f;

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
		/// Get the world positions of this node's front and back connection points
		/// Primarily uses snappoints if they exist
		/// </summary>
		/// <returns>Array containing front and back positions</returns>
		public Vector3[] GetNodeEndpoints()
		{
			Vector3[] endpoints = new Vector3[2];

			// Always prefer snappoints if they exist
			Transform[] snapPoints = GetSnapPoints();
			if (snapPoints.Length >= 2)
			{
				// Sort snappoints by their local Z position to determine front/back
				var sortedSnapPoints = snapPoints.OrderBy(sp => sp.localPosition.z).ToArray();
				endpoints[0] = sortedSnapPoints[1].position; // Front (higher Z)
				endpoints[1] = sortedSnapPoints[0].position; // Back (lower Z)

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] {name} using snappoints - Front: {endpoints[0]}, Back: {endpoints[1]}");
				}
				return endpoints;
			}

			// Fallback: calculate based on node orientation if no snappoints
			Vector3 forwardDirection = transform.forward;
			Vector3 halfLength = forwardDirection * (nodeLength / 2f);

			endpoints[0] = transform.position + halfLength; // Front
			endpoints[1] = transform.position - halfLength; // Back

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] {name} calculated endpoints (no snappoints) - Front: {endpoints[0]}, Back: {endpoints[1]}");
			}

			return endpoints;
		}

		/// <summary>
		/// Get the front connection point (forward direction)
		/// Prefers front snappoint if it exists
		/// </summary>
		public Vector3 GetFrontConnectionPoint()
		{
			// Check for front snappoint
			Transform frontSnap = GetSnapPointByName("front");
			if (frontSnap != null)
			{
				return frontSnap.position;
			}

			// Fallback to calculated position
			return transform.position + transform.forward * (nodeLength / 2f);
		}

		/// <summary>
		/// Get the back connection point (backward direction)
		/// Prefers back snappoint if it exists
		/// </summary>
		public Vector3 GetBackConnectionPoint()
		{
			// Check for back snappoint
			Transform backSnap = GetSnapPointByName("back");
			if (backSnap != null)
			{
				return backSnap.position;
			}

			// Fallback to calculated position
			return transform.position - transform.forward * (nodeLength / 2f);
		}

		/// <summary>
		/// Get snap point transforms if they exist
		/// </summary>
		private Transform[] GetSnapPoints()
		{
			List<Transform> snapPoints = new List<Transform>();

			foreach (Transform child in transform)
			{
				if (child.name.Contains("snappoint"))
				{
					snapPoints.Add(child);
				}
			}

			// Sort by name to ensure consistent ordering
			snapPoints.Sort((a, b) => a.name.CompareTo(b.name));

			return snapPoints.ToArray();
		}

		/// <summary>
		/// Get a specific snappoint by name suffix
		/// </summary>
		public Transform GetSnapPointByName(string suffix)
		{
			foreach (Transform child in transform)
			{
				if (child.name.Contains("snappoint") && child.name.Contains(suffix))
				{
					return child;
				}
			}
			return null;
		}

		/// <summary>
		/// Add a connection to another node (without reciprocating)
		/// </summary>
		public void AddConnection(BaseNode node)
		{
			if (node != null && node != this && !connectedNodes.Contains(node))
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
		/// <param name="other">The other node to check connection compatibility</param>
		/// <returns>True if connection is allowed</returns>
		protected virtual bool CanConnectTo(BaseNode other)
		{
			if (other == null || other == this) return false;

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
		/// <returns>List of connected nodes</returns>
		public List<BaseNode> GetConnectedNodes()
		{
			return new List<BaseNode>(connectedNodes);
		}

		#endregion

		#region Network Management

		/// <summary>
		/// Set the network ID for this node and sync to clients
		/// </summary>
		/// <param name="networkId">The network ID to assign</param>
		public virtual void SetNetworkId(string networkId)
		{
			NetworkId = networkId;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdateNetworkId", networkId ?? "");
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] {name} assigned to network: {networkId ?? "None"}");
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
		/// <param name="active">Whether the node should be active</param>
		public virtual void SetActive(bool active)
		{
			IsActive = active;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
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

		/// <summary>
		/// Update visual indicators based on active state
		/// Override in derived classes for custom visuals
		/// </summary>
		protected virtual void UpdateVisualState(bool active)
		{
			// Base implementation - can be overridden for visual effects
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				// Add visual indicators at endpoints
				UpdateEndpointVisuals();
			}
		}

		/// <summary>
		/// Update visual indicators at connection endpoints
		/// </summary>
		private void UpdateEndpointVisuals()
		{
			// Clean up old indicators
			foreach (Transform child in transform)
			{
				if (child.name.Contains("endpoint_indicator"))
				{
					Destroy(child.gameObject);
				}
			}

			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Get all connection points
			Vector3[] connectionPoints = GetConnectionPoints();

			// Create visual indicators at each connection point
			for (int i = 0; i < connectionPoints.Length; i++)
			{
				GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				indicator.name = $"endpoint_indicator_{i}";
				indicator.transform.position = connectionPoints[i];
				indicator.transform.localScale = Vector3.one * 0.3f;
				Destroy(indicator.GetComponent<Collider>());

				// Check if this point is connected to another node
				bool isConnected = false;
				foreach (var connectedNode in connectedNodes)
				{
					if (connectedNode != null)
					{
						Vector3[] otherPoints = connectedNode.GetConnectionPoints();
						foreach (var otherPoint in otherPoints)
						{
							if (Vector3.Distance(connectionPoints[i], otherPoint) < 0.15f)
							{
								isConnected = true;
								break;
							}
						}
						if (isConnected) break;
					}
				}

				// Set indicator color (green if connected, yellow if available)
				var renderer = indicator.GetComponent<Renderer>();
				if (renderer != null)
				{
					renderer.material = new Material(Shader.Find("Sprites/Default"));
					renderer.material.color = isConnected ? Color.green : Color.yellow;
				}

				indicator.transform.SetParent(transform);
			}
		}

		#endregion

		#region Hoverable Interface

		/// <summary>
		/// Get hover text displayed when looking at node
		/// </summary>
		public virtual string GetHoverText()
		{
			string status = IsActive ? "<color=green>Active</color>" : "<color=red>Inactive</color>";
			string network = !string.IsNullOrEmpty(NetworkId) ? NetworkId.Substring(0, Math.Min(8, NetworkId.Length)) : "No Network";

			// Show connection count
			int connectionCount = connectedNodes.Count;
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
		/// Base implementation does nothing - override in derived classes
		/// </summary>
		/// <param name="user">The player interacting</param>
		/// <param name="hold">Whether the interaction is being held</param>
		/// <param name="alt">Whether alt-interaction is used</param>
		/// <returns>True if interaction was handled</returns>
		public virtual bool Interact(Humanoid user, bool hold, bool alt)
		{
			// Base nodes don't have interactions
			return false;
		}

		/// <summary>
		/// Handle item use on node
		/// Base implementation does nothing
		/// </summary>
		/// <param name="user">The player using the item</param>
		/// <param name="item">The item being used</param>
		/// <returns>True if item use was handled</returns>
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

			// Get connection points - handle potential initialization issues
			Vector3[] points = null;
			try
			{
				points = GetConnectionPoints();
			}
			catch
			{
				// If GetConnectionPoints fails, use fallback
				points = new Vector3[2];
				Vector3 halfLength = transform.forward * (nodeLength > 0 ? nodeLength : 1f) / 2f;
				points[0] = transform.position + halfLength;
				points[1] = transform.position - halfLength;
			}

			// Draw connection points in yellow
			Gizmos.color = Color.yellow;
			foreach (var point in points)
			{
				Gizmos.DrawWireSphere(point, 0.3f);
			}

			// Draw lines between connection points to show the node
			if (points.Length >= 2)
			{
				Gizmos.color = NodeType switch
				{
					NodeType.Conduit => Color.gray,
					NodeType.Extract => Color.green,
					NodeType.Insert => Color.blue,
					_ => Color.white
				};

				for (int i = 0; i < points.Length - 1; i++)
				{
					Gizmos.DrawLine(points[i], points[i + 1]);
				}
			}

			// Draw connections to other nodes
			if (connectedNodes != null)
			{
				Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Semi-transparent yellow
				foreach (var node in connectedNodes)
				{
					if (node != null)
					{
						// Find the closest connection points
						Vector3[] myPoints = points;
						Vector3[] otherPoints = null;

						try
						{
							otherPoints = node.GetConnectionPoints();
						}
						catch
						{
							// Fallback if the other node can't provide points
							otherPoints = new Vector3[] { node.transform.position };
						}

						float minDist = float.MaxValue;
						Vector3 myClosest = transform.position;
						Vector3 otherClosest = node.transform.position;

						foreach (var myPoint in myPoints)
						{
							foreach (var otherPoint in otherPoints)
							{
								float dist = Vector3.Distance(myPoint, otherPoint);
								if (dist < minDist)
								{
									minDist = dist;
									myClosest = myPoint;
									otherClosest = otherPoint;
								}
							}
						}

						Gizmos.DrawLine(myClosest, otherClosest);
					}
				}
			}
		}

		#endregion
	}
}