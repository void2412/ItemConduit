using ItemConduit.Config;
using ItemConduit.Core;
using ItemConduit.Debug;
using ItemConduit.Events;
using ItemConduit.Extensions;
using ItemConduit.Interfaces;
using ItemConduit.Network;
using ItemConduit.Utils;
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

		public bool IsDetectionComplete { get; private set; } = true;

		/// <summary>List of nodes connected to this one</summary>
		protected List<BaseNode> connectedNodes = new List<BaseNode>();

		/// <summary>Network view component for multiplayer sync</summary>
		protected ZNetView zNetView;

		/// <summary>Piece component for building system integration</summary>
		protected Piece piece;

		/// <summary>Flag to prevent multiple detection updates</summary>
		protected bool isUpdatingDetection = false;

		/// <summary>Coroutine reference for unified detection</summary>
		private Coroutine detectionCoroutine;

		/// <summary>Flag to indicate if this is a ghost/preview piece</summary>
		public bool isGhostPiece = false;

		private BoundsVisualizer boundsVisualizer;

		private SnapConnectionVisualizer snapVisualizer;

		private DetectionMode currentDetectionMode = DetectionMode.Full;

		public delegate void DetectionCompleteHandler(BaseNode node);
		public event DetectionCompleteHandler OnDetectionComplete;

		#endregion

		#region Container Management Fields

		/// <summary>Reference to connected container (null for Conduit nodes)</summary>
		protected IContainerInterface targetContainer;

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

			if (DebugConfig.showDebug.Value)
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
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping initialization for ghost piece: {name}");
				}
				return;
			}

			if (CanConnectToContainers && !isGhostPiece)
			{
				ContainerEventManager.Instance.RegisterNode(this);
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

			if (CanConnectToContainers && !isGhostPiece)
			{
				ItemConduit.Events.ContainerEventManager.Instance.RegisterNode(this);
				Logger.LogInfo($"[ItemConduit] {name} registered for container events");
			}

			// Only register on server
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				StartUnifiedDetection();

				while (!IsDetectionComplete)
				{
					yield return null;
				}

				// Register with network manager
				NetworkManager.Instance.RegisterNode(this);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {name} registered with NetworkManager");
				}
			}

			InitializeBoundsVisualization();

			
			//if (CanConnectToContainers)
			//{
			//	yield return new WaitForSeconds(0.5f);
			//	StartUnifiedDetection(DetectionMode.ContainersOnly);
			//}
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

			// Unregister from container events
			if (CanConnectToContainers)
			{
				ContainerEventManager.Instance.UnregisterNode(this);
			}

			// Unregister from network manager
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				NetworkManager.Instance.UnregisterNode(this);
			}
		}

		#endregion

		#region Unified Detection System

		public enum DetectionMode
		{
			Full,           // Both connections and containers
			ConnectionsOnly, // Just node connections (for rebuilds)
			ContainersOnly  // Just container detection
		}

		/// <summary>
		/// Start unified detection for both connections and containers
		/// </summary>
		public void StartUnifiedDetection(DetectionMode mode = DetectionMode.Full)
		{
			if (isGhostPiece) return;

			// If already running, check if we need to upgrade the mode
			if (isUpdatingDetection)
			{
				// If we're doing ContainersOnly but Full is requested, we should upgrade
				if (currentDetectionMode == DetectionMode.ContainersOnly && mode == DetectionMode.Full)
				{
					Logger.LogWarning($"[DEBUG] Stopping ContainersOnly to upgrade to Full detection");
					if (detectionCoroutine != null)
					{
						StopCoroutine(detectionCoroutine);
						isUpdatingDetection = false;
						IsDetectionComplete = true;
					}
				}
				else
				{
					return; // Already running appropriate detection
				}
			}

			if (DebugConfig.showDebug.Value)
			{ 

			}

			// If we're already doing a full detection, don't interrupt
			if (isUpdatingDetection && currentDetectionMode == DetectionMode.Full)
				return;

			currentDetectionMode = mode;

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
			float startTime = Time.realtimeSinceStartup;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogWarning($"[TIMING] {name} - Detection started");
			}

			if (isUpdatingDetection) yield break;
			isUpdatingDetection = true;
			IsDetectionComplete = false;

			// Small delay to let physics settle
			yield return new WaitForSeconds(0.1f);

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogWarning($"[TIMING] {name} - After wait: {Time.realtimeSinceStartup - startTime:F3}s");
			}
		

			// Get the main collider to use its oriented bounds
			Collider mainCollider = GetMainCollider();
			if (mainCollider == null)
			{
				Logger.LogWarning($"[ItemConduit] No collider found for {name}");
				isUpdatingDetection = false;
				yield break;
			}

			

			// Get local bounds and transform info from the collider
			Vector3 center;
			Vector3 halfExtents;
			Quaternion rotation;

			if (mainCollider is BoxCollider boxCollider)
			{
				// For box colliders, use their actual dimensions
				center = transform.TransformPoint(boxCollider.center);
				halfExtents = Vector3.Scale(boxCollider.size * 0.5f, mainCollider.transform.lossyScale);
				rotation = transform.rotation;
			}
			else
			{
				// For other colliders, approximate with local bounds
				Bounds localBounds = GetColliderLocalBounds(mainCollider);
				center = transform.TransformPoint(localBounds.center);
				halfExtents = Vector3.Scale(localBounds.extents, transform.lossyScale);
				rotation = transform.rotation;
			}

			

			// Perform ORIENTED overlap check (this properly handles rotation!)
			Collider[] overlaps = Physics.OverlapBox(
				center,
				halfExtents,
				rotation,  // This rotation is now properly applied!
				LayerMask.GetMask("piece", "piece_nonsolid", "item", "Default_small")
			);

			

			if (currentDetectionMode == DetectionMode.Full || currentDetectionMode == DetectionMode.ConnectionsOnly)
			{
				// Process node connections
				yield return StartCoroutine(ProcessNodeConnections(overlaps));
			}

			if (currentDetectionMode == DetectionMode.Full || currentDetectionMode == DetectionMode.ContainersOnly)
			{
				// Process container connections (only Extract/Insert nodes)
				if (CanConnectToContainers)
				{
					yield return StartCoroutine(ProcessContainerConnection(overlaps));
				}
			}

			// Update visual state
			bool hasConnections = connectedNodes.Count > 0 || (CanConnectToContainers && targetContainer != null);
			UpdateVisualState(hasConnections);

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} detection complete: {connectedNodes.Count} nodes, " +
							 $"container: {(targetContainer != null ? targetContainer.GetName() : "none")}");
			}

			isUpdatingDetection = false;
			IsDetectionComplete = true;
			OnDetectionComplete?.Invoke(this);

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogWarning($"[TIMING] {name} - Detection complete: {Time.realtimeSinceStartup - startTime:F3}s");
			}
		}

		private Collider GetMainCollider()
		{
			// First try to get non-trigger collider
			Collider[] colliders = GetComponentsInChildren<Collider>();

			foreach (var col in colliders)
			{
				if (!col.isTrigger)
				{
					return col;
				}
			}

			// If all are triggers, return first one
			if (colliders.Length > 0)
				return colliders[0];

			return null;
		}

		private Bounds GetColliderLocalBounds(Collider collider)
		{
			if (collider is BoxCollider boxCollider)
			{
				return new Bounds(boxCollider.center, boxCollider.size);
			}
			else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
			{
				return meshCollider.sharedMesh.bounds;
			}
			else if (collider is CapsuleCollider capsuleCollider)
			{
				float radius = capsuleCollider.radius;
				float height = capsuleCollider.height;
				return new Bounds(capsuleCollider.center, new Vector3(radius * 2, height, radius * 2));
			}
			else
			{
				// Fallback
				Bounds worldBounds = collider.bounds;
				Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
				Vector3 localSize = transform.InverseTransformVector(worldBounds.size);
				return new Bounds(localCenter, localSize);
			}
		}

		/// <summary>
		/// Process node-to-node connections
		/// </summary>
		private IEnumerator ProcessNodeConnections(Collider[] overlaps)
		{
			var existingConnections = new HashSet<BaseNode>(connectedNodes);
			var detectedConnections = new HashSet<BaseNode>();

			// Method 1: Snappoint detection for straight connections
			var snapPoints = GetSnapPoints();
			foreach (var snapPoint in snapPoints)
			{
				Collider[] snapOverlaps = Physics.OverlapSphere(
					snapPoint.position,
					NetworkConfig.connectionRange.Value,
					LayerMask.GetMask("piece", "piece_nonsolid")
				);

				foreach (var col in snapOverlaps)
				{
					if (col == null || col.transform == transform) continue;

					BaseNode otherNode = col.GetComponentInParent<BaseNode>();
					if (otherNode != null && otherNode != this && !otherNode.isGhostPiece)
					{
						var otherSnaps = otherNode.GetSnapPoints();
						foreach (var otherSnap in otherSnaps)
						{
							float dist = Vector3.Distance(snapPoint.position, otherSnap.position);
							if (dist < 0.2f)
							{
								if (!detectedConnections.Contains(otherNode))
								{
									detectedConnections.Add(otherNode);
									if (DebugConfig.showDebug.Value)
									{
										Logger.LogInfo($"[ItemConduit] Snap connection (straight): {name} <-> {otherNode.name} (dist: {dist:F3}m)");
									}
								}
								break;
							}
						}
					}
				}
				yield return null;
			}

			// Method 2: OBB detection for angled connections (only if no snap connections)
			foreach (var col in overlaps)
			{
				if (col == null || col.transform == transform) continue;

				BaseNode otherNode = col.GetComponentInParent<BaseNode>();
				if (otherNode != null &&
					otherNode != this &&
					!otherNode.isGhostPiece &&
					!detectedConnections.Contains(otherNode))
				{
					if (CanConnectTo(otherNode) && CheckOrientedBoundsOverlap(otherNode))
					{
						detectedConnections.Add(otherNode);
						if (DebugConfig.showDebug.Value)
						{
							Logger.LogInfo($"[ItemConduit] OBB connection (angled): {name} <-> {otherNode.name}");
						}
					}
				}
			}

			// Update connections and handle changes...
			connectedNodes = detectedConnections.ToList();
			// ... rest of the method remains the same
		}


		private bool CheckOrientedBoundsOverlap(BaseNode otherNode)
		{
			Collider myCollider = GetMainCollider();
			Collider otherCollider = otherNode.GetMainCollider();

			if (myCollider == null || otherCollider == null) return false;

			// Get OBB parameters for both nodes
			OBB myOBB = GetOBB(myCollider);
			OBB otherOBB = GetOBB(otherCollider);

			return TestOBBOverlap(myOBB, otherOBB);
		}

		private struct OBB
		{
			public Vector3 center;
			public Vector3 halfExtents;
			public Quaternion rotation;
		}

		private OBB GetOBB(Collider collider)
		{
			OBB obb = new OBB();

			if (collider is BoxCollider boxCollider)
			{
				obb.center = collider.transform.TransformPoint(boxCollider.center);
				obb.halfExtents = Vector3.Scale(boxCollider.size * 0.5f, collider.transform.lossyScale);
				obb.rotation = collider.transform.rotation;
			}
			else
			{
				// Approximate other collider types with their bounds
				Bounds localBounds = GetColliderLocalBounds(collider);
				obb.center = collider.transform.TransformPoint(localBounds.center);
				obb.halfExtents = Vector3.Scale(localBounds.extents, collider.transform.lossyScale);
				obb.rotation = collider.transform.rotation;
			}

			return obb;
		}

		private bool TestOBBOverlap(OBB a, OBB b)
		{
			// Get rotation matrices
			Matrix4x4 matA = Matrix4x4.Rotate(a.rotation);
			Matrix4x4 matB = Matrix4x4.Rotate(b.rotation);

			// Get axes for both OBBs (3 axes each)
			Vector3[] axesA = new Vector3[3];
			Vector3[] axesB = new Vector3[3];

			axesA[0] = matA.GetColumn(0).normalized;
			axesA[1] = matA.GetColumn(1).normalized;
			axesA[2] = matA.GetColumn(2).normalized;

			axesB[0] = matB.GetColumn(0).normalized;
			axesB[1] = matB.GetColumn(1).normalized;
			axesB[2] = matB.GetColumn(2).normalized;

			// Test 15 potential separating axes
			Vector3[] testAxes = new Vector3[15];

			// Face normals of A
			testAxes[0] = axesA[0];
			testAxes[1] = axesA[1];
			testAxes[2] = axesA[2];

			// Face normals of B
			testAxes[3] = axesB[0];
			testAxes[4] = axesB[1];
			testAxes[5] = axesB[2];

			// Cross products of edges
			int index = 6;
			for (int i = 0; i < 3; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					testAxes[index++] = Vector3.Cross(axesA[i], axesB[j]).normalized;
				}
			}

			// Test each axis
			Vector3 distance = b.center - a.center;

			foreach (Vector3 axis in testAxes)
			{
				if (axis.sqrMagnitude < 0.0001f) continue; // Skip degenerate axes

				// Project both OBBs onto the axis
				float projA = ProjectOBB(a, axis);
				float projB = ProjectOBB(b, axis);

				// Project distance between centers
				float projDistance = Mathf.Abs(Vector3.Dot(distance, axis));

				// Check for separation
				if (projDistance > projA + projB)
				{
					return false; // Found a separating axis
				}
			}

			return true; // No separating axis found, boxes overlap
		}

		private float ProjectOBB(OBB obb, Vector3 axis)
		{
			Matrix4x4 mat = Matrix4x4.Rotate(obb.rotation);

			Vector3 xAxis = mat.GetColumn(0).normalized;
			Vector3 yAxis = mat.GetColumn(1).normalized;
			Vector3 zAxis = mat.GetColumn(2).normalized;

			float projX = Mathf.Abs(Vector3.Dot(axis, xAxis)) * obb.halfExtents.x;
			float projY = Mathf.Abs(Vector3.Dot(axis, yAxis)) * obb.halfExtents.y;
			float projZ = Mathf.Abs(Vector3.Dot(axis, zAxis)) * obb.halfExtents.z;

			return projX + projY + projZ;
		}

		/// <summary>
		/// Process container connections (virtual for override in subclasses)
		/// </summary>
		
		protected virtual IEnumerator ProcessContainerConnection(Collider[] overlaps)
		{
			yield return null;
		}

		/// <summary>
		/// Find the best overlapping container from collision results
		/// </summary>
		/// TODO: Add fixes to be able to work with different types (Smeltery, ...)

		protected IContainerInterface FindBestOverlappingContainer(Collider[] overlaps)
		{
			float closestDistance = float.MaxValue;
			IContainerInterface bestContainer = null;

			// Get our own collider for bounds checking
			Collider myCollider = GetMainCollider();
			if (myCollider == null) return null;

			foreach (var col in overlaps)
			{
				if (col == null || col.transform == transform) continue;

				// Try multiple ways to find container
				IContainerInterface containerInterface = col.GetComponent<IContainerInterface>()
					?? col.GetComponentInParent<IContainerInterface>()
					?? col.GetComponentInChildren<IContainerInterface>();

				if (containerInterface != null)
				{
					// Get the container's collider
					Collider containerCollider = col.GetComponent<Collider>()
						?? col.GetComponentInChildren<Collider>();

					if (containerCollider != null)
					{
						// Check for actual intersection using oriented bounds
						if (myCollider.bounds.Intersects(containerCollider.bounds))
						{
							float distance = Vector3.Distance(transform.position, col.transform.position);

							if (DebugConfig.showDebug.Value)
							{
								Logger.LogInfo($"[ItemConduit] Found overlapping container: {col.name} ({containerInterface.GetName()}) at distance {distance:F2}m");

								Inventory inv = containerInterface.GetInventory();
								if (inv != null)
								{
									Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots, {inv.GetAllItems().Count} items");
								}
							}

							if (distance < closestDistance)
							{
								closestDistance = distance;
								bestContainer = containerInterface;
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
					IContainerInterface container = hit.collider.GetComponent<IContainerInterface>()
						?? hit.collider.GetComponentInParent<IContainerInterface>();

					if (container != null && container.GetInventory() != null)
					{
						bestContainer = container;

						if (DebugConfig.showDebug.Value)
						{
							Logger.LogInfo($"[ItemConduit] Found container below node: {container.GetName()}");
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
		/// Get connection points for this node
		/// </summary>
		public virtual Vector3[] GetConnectionPoints()
		{
			
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
			if (node == null || node == this || node.isGhostPiece) return;

			if (!connectedNodes.Contains(node))
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
		public virtual IContainerInterface GetTargetContainer()
		{
			// Base implementation returns null (Conduit nodes don't have containers)
			return null;
		}

		/// <summary>
		/// Force refresh all detections
		/// </summary>
		public void RefreshDetection(DetectionMode mode = DetectionMode.Full)
		{
			if (IsValidPlacedNode())
			{
				StartUnifiedDetection(mode);
			}
		}


		#endregion

		#region Container Event Handling

		/// <summary>
		/// Called when a container is placed nearby
		/// </summary>
		public virtual void OnNearbyContainerPlaced(IContainerInterface container, float distance)
		{
			// Only Extract/Insert nodes care about containers
			if (!CanConnectToContainers) return;

			if (CanConnectToContainers && !isGhostPiece)
			{
				ItemConduit.Events.ContainerEventManager.Instance.RegisterNode(this);
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] {name} registered for container events");
				}
				
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} notified: container placed {distance:F1}m away");
			}

			// Schedule a container detection refresh
			StartCoroutine(DelayedContainerRefresh(0.1f));
		}

		/// <summary>
		/// Called when a container is removed nearby
		/// </summary>
		public virtual void OnNearbyContainerRemoved(IContainerInterface container, float distance)
		{
			// Only Extract/Insert nodes care about containers
			if (!CanConnectToContainers) return;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} notified: container removed {distance:F1}m away");
			}

			// If this was our connected container, clear it immediately
			if (targetContainer == container)
			{
				if (targetContainer is SmelteryExtension smeltery)
				{
					smeltery.OnNodeDisconnected(this);
				}

				targetContainer = null;
				UpdateVisualState(connectedNodes.Count > 0); // Update visual state

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogWarning($"[ItemConduit] {name} lost its connected container!");
				}

				// Try to find another container
				StartCoroutine(DelayedContainerRefresh(0.5f));
			}
			else
			{
				// Still refresh to potentially find better containers
				StartCoroutine(DelayedContainerRefresh(0.3f));
			}
		}

		/// <summary>
		/// Delayed container refresh to let physics settle
		/// </summary>
		private IEnumerator DelayedContainerRefresh(float delay)
		{
			yield return new WaitForSeconds(delay);

			// Only refresh if we're a valid placed node
			if (IsValidPlacedNode() && CanConnectToContainers)
			{
				RefreshDetection(DetectionMode.ContainersOnly);
			}
		}

		#endregion

		#region Visual Effects

		/// <summary>
		/// Update visual indicators based on active state
		/// </summary>
		protected virtual void UpdateVisualState(bool active)
		{
			// Skip for ghost pieces
			if (isGhostPiece) return;

			IsActive = active;

			// Base implementation - can be overridden for visual effects
			if (VisualConfig.transferVisualEffect.Value)
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
				NodeType.Conduit => $"<color=white>Conduit Node</color>",
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


		protected virtual void InitializeBoundsVisualization()
		{
			if (!DebugConfig.showDebug.Value)
				return;

			if (isGhostPiece) return;

			boundsVisualizer = gameObject.AddComponent<BoundsVisualizer>();

			snapVisualizer = gameObject.AddComponent<SnapConnectionVisualizer>();
			snapVisualizer.Initialize(this);
			UpdateSnapVisualization();

			// Color based on node type for the collider wireframe
			Color colliderColor = NodeType switch
			{
				NodeType.Extract => new Color(0, 1, 0, 1f),      // Green
				NodeType.Insert => new Color(0, 0.5f, 1f, 1f),   // Blue  
				NodeType.Conduit => new Color(1f, 1f, 1f, 1f),   // White
				_ => new Color(1, 1, 0, 1f)                      // Yellow
			};

			boundsVisualizer.Initialize(colliderColor);
			UpdateBoundsVisualization();
		}

		protected void UpdateBoundsVisualization()
		{
			if (boundsVisualizer == null) return;

			// Update collider visualization only
			Collider mainCollider = GetMainCollider();
			if (mainCollider != null)
			{
				boundsVisualizer.UpdateCollider(mainCollider);
			}
		}
	
		public void UpdateSnapVisualization()
		{
			if (snapVisualizer != null)
			{
				snapVisualizer.UpdateConnections();
			}
		}
		#endregion
	}
}