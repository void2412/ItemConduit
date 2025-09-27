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
	/// Optimized with physics-based connection detection
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

		/// <summary>Colliders used for connection detection</summary>
		private List<SphereCollider> connectionColliders = new List<SphereCollider>();

		/// <summary>Flag to prevent multiple connection updates</summary>
		private bool isUpdatingConnections = false;

		/// <summary>Coroutine reference for connection finding</summary>
		private Coroutine connectionCoroutine;

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Initialize component references and setup connection detection
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

			// Setup connection detection colliders
			SetupConnectionDetection();

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Node {name} awakened (Type: {NodeType}, Length: {nodeLength}m)");
			}
		}

		/// <summary>
		/// Register node with network manager on start
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
					Logger.LogInfo($"[ItemConduit] Node {name} registered with NetworkManager");
				}
			}

			// Start connection detection after a short delay
			yield return new WaitForSeconds(0.5f);
			StartConnectionDetection();
		}

		/// <summary>
		/// Cleanup on node destruction
		/// </summary>
		protected virtual void OnDestroy()
		{
			// Stop any running coroutines
			if (connectionCoroutine != null)
			{
				StopCoroutine(connectionCoroutine);
				connectionCoroutine = null;
			}

			// Remove this node from all connected nodes
			foreach (var connectedNode in connectedNodes.ToList())
			{
				if (connectedNode != null)
				{
					connectedNode.RemoveConnection(this);
				}
			}

			// Cleanup connection colliders
			foreach (var collider in connectionColliders)
			{
				if (collider != null)
					Destroy(collider.gameObject);
			}
			connectionColliders.Clear();

			// Unregister from network manager
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				ItemConduit.Network.NetworkManager.Instance.UnregisterNode(this);
			}
		}

		#endregion

		#region Physics-Based Connection Detection

		/// <summary>
		/// Setup trigger colliders for connection detection
		/// </summary>
		private void SetupConnectionDetection()
		{
			// Get existing snappoints
			Transform[] snapPoints = GetSnapPoints();

			if (snapPoints.Length > 0)
			{
				// Create trigger colliders at each snappoint
				foreach (var snapPoint in snapPoints)
				{
					GameObject triggerObj = new GameObject($"ConnectionTrigger_{snapPoint.name}");
					triggerObj.transform.SetParent(snapPoint);
					triggerObj.transform.localPosition = Vector3.zero;
					triggerObj.transform.localRotation = Quaternion.identity;
					triggerObj.layer = gameObject.layer;

					// Add sphere collider as trigger
					SphereCollider trigger = triggerObj.AddComponent<SphereCollider>();
					trigger.isTrigger = true;
					trigger.radius = 0.3f; // 30cm radius for connection detection

					// Add connection detector component
					ConnectionDetector detector = triggerObj.AddComponent<ConnectionDetector>();
					detector.parentNode = this;

					connectionColliders.Add(trigger);
				}
			}
			else
			{
				// Fallback: create triggers at node endpoints
				Vector3 forward = transform.forward;
				float halfLength = nodeLength / 2f;

				// Front trigger
				CreateConnectionTrigger("front", forward * halfLength);

				// Back trigger
				CreateConnectionTrigger("back", -forward * halfLength);
			}
		}

		/// <summary>
		/// Create a connection trigger at specified local position
		/// </summary>
		private void CreateConnectionTrigger(string name, Vector3 localPosition)
		{
			GameObject triggerObj = new GameObject($"ConnectionTrigger_{name}");
			triggerObj.transform.SetParent(transform);
			triggerObj.transform.localPosition = localPosition;
			triggerObj.transform.localRotation = Quaternion.identity;
			triggerObj.layer = gameObject.layer;

			SphereCollider trigger = triggerObj.AddComponent<SphereCollider>();
			trigger.isTrigger = true;
			trigger.radius = 0.3f;

			ConnectionDetector detector = triggerObj.AddComponent<ConnectionDetector>();
			detector.parentNode = this;

			connectionColliders.Add(trigger);
		}

		/// <summary>
		/// Start the connection detection process
		/// </summary>
		public void StartConnectionDetection()
		{
			if (connectionCoroutine != null)
			{
				StopCoroutine(connectionCoroutine);
			}
			connectionCoroutine = StartCoroutine(FindConnectionsCoroutine());
		}

		/// <summary>
		/// Optimized coroutine-based connection finding
		/// </summary>
		private IEnumerator FindConnectionsCoroutine()
		{
			if (isUpdatingConnections)
			{
				yield break;
			}

			isUpdatingConnections = true;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogWarning($"[ItemConduit] Starting connection detection for {name}");
			}

			// Small delay to let physics settle
			yield return new WaitForSeconds(0.1f);

			// Clear existing connections (but notify connected nodes)
			foreach (var node in connectedNodes.ToList())
			{
				if (node != null)
				{
					node.RemoveConnection(this);
				}
			}
			connectedNodes.Clear();

			// Use physics overlap to find nearby nodes
			float searchRadius = nodeLength + ItemConduitMod.ConnectionRange.Value;
			Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, searchRadius);

			int nodesChecked = 0;
			const int nodesPerFrame = 5; // Process 5 nodes per frame to avoid spikes

			foreach (Collider col in nearbyColliders)
			{
				if (col == null) continue;

				BaseNode otherNode = col.GetComponent<BaseNode>();
				if (otherNode == null)
				{
					// Check parent for node component
					otherNode = col.GetComponentInParent<BaseNode>();
				}

				if (otherNode != null && otherNode != this && !connectedNodes.Contains(otherNode))
				{
					// Check if nodes are touching or very close
					if (CheckNodeConnection(otherNode))
					{
						EstablishConnection(otherNode);
					}

					nodesChecked++;

					// Yield every few nodes to prevent frame drops
					if (nodesChecked % nodesPerFrame == 0)
					{
						yield return null; // Wait one frame
					}
				}
			}

			// Update visual state after connections are established
			UpdateVisualState(connectedNodes.Count > 0);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} connection detection complete: {connectedNodes.Count} connections");
			}

			isUpdatingConnections = false;
		}

		/// <summary>
		/// Check if this node should connect to another based on proximity
		/// </summary>
		private bool CheckNodeConnection(BaseNode otherNode)
		{
			if (otherNode == null || !CanConnectTo(otherNode))
				return false;

			// Get connection points for both nodes
			Vector3[] myPoints = GetConnectionPoints();
			Vector3[] otherPoints = otherNode.GetConnectionPoints();

			float connectionThreshold = ItemConduitMod.EndpointConnectionThreshold.Value;

			// Check all point combinations
			foreach (var myPoint in myPoints)
			{
				foreach (var otherPoint in otherPoints)
				{
					float distance = Vector3.Distance(myPoint, otherPoint);

					// Very tight threshold for snapped connections
					if (distance <= connectionThreshold)
					{
						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Logger.LogInfo($"[ItemConduit] Connection detected: {name} <-> {otherNode.name} (dist: {distance:F3}m)");
						}
						return true;
					}
				}
			}

			// Also check if the nodes are overlapping (colliding)
			Bounds myBounds = GetNodeBounds();
			Bounds otherBounds = otherNode.GetNodeBounds();

			if (myBounds.Intersects(otherBounds))
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Collision connection: {name} <-> {otherNode.name}");
				}
				return true;
			}

			return false;
		}

		/// <summary>
		/// Get the bounds of this node for collision detection
		/// </summary>
		private Bounds GetNodeBounds()
		{
			// Get all colliders on this node
			Collider[] colliders = GetComponentsInChildren<Collider>();

			if (colliders.Length == 0)
			{
				// Fallback to calculated bounds
				return new Bounds(transform.position, Vector3.one * nodeLength);
			}

			// Combine all collider bounds
			Bounds bounds = colliders[0].bounds;
			for (int i = 1; i < colliders.Length; i++)
			{
				if (!colliders[i].isTrigger) // Skip trigger colliders
				{
					bounds.Encapsulate(colliders[i].bounds);
				}
			}

			return bounds;
		}

		/// <summary>
		/// Establish a bidirectional connection between nodes
		/// </summary>
		private void EstablishConnection(BaseNode otherNode)
		{
			if (otherNode == null) return;

			AddConnection(otherNode);
			otherNode.AddConnection(this);

			// Visual feedback for successful connection
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				CreateConnectionEffect(transform.position, otherNode.transform.position);
			}
		}

		/// <summary>
		/// Handle trigger events for connection detection
		/// </summary>
		public void OnConnectionTriggerEnter(BaseNode otherNode)
		{
			if (otherNode == null || otherNode == this || connectedNodes.Contains(otherNode))
				return;

			if (CanConnectTo(otherNode))
			{
				EstablishConnection(otherNode);

				// Update visual state
				UpdateVisualState(true);
			}
		}

		/// <summary>
		/// Handle trigger exit events
		/// </summary>
		public void OnConnectionTriggerExit(BaseNode otherNode)
		{
			// Optionally disconnect when nodes move apart
			// For now, connections persist until node is destroyed or rebuilt
		}

		#endregion

		#region Connection Management

		/// <summary>
		/// Legacy FindConnections method - now uses coroutine
		/// </summary>
		public virtual void FindConnections()
		{
			StartConnectionDetection();
		}

		/// <summary>
		/// Get connection points for this node
		/// </summary>
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
		public List<BaseNode> GetConnectedNodes()
		{
			return new List<BaseNode>(connectedNodes);
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
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
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

			// Draw connection triggers
			foreach (var collider in connectionColliders)
			{
				if (collider != null)
				{
					Gizmos.color = Color.yellow * 0.5f;
					Gizmos.DrawWireSphere(collider.transform.position, collider.radius);
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
						Gizmos.DrawLine(transform.position, node.transform.position);
					}
				}
			}
		}

		#endregion
	}

	/// <summary>
	/// Helper component for detecting connections via triggers
	/// </summary>
	public class ConnectionDetector : MonoBehaviour
	{
		public BaseNode parentNode;

		private void OnTriggerEnter(Collider other)
		{
			if (other == null || parentNode == null) return;

			// Check if the other collider has a connection detector
			ConnectionDetector otherDetector = other.GetComponent<ConnectionDetector>();
			if (otherDetector != null && otherDetector.parentNode != null)
			{
				// Notify parent node of potential connection
				parentNode.OnConnectionTriggerEnter(otherDetector.parentNode);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (other == null || parentNode == null) return;

			ConnectionDetector otherDetector = other.GetComponent<ConnectionDetector>();
			if (otherDetector != null && otherDetector.parentNode != null)
			{
				parentNode.OnConnectionTriggerExit(otherDetector.parentNode);
			}
		}
	}
}