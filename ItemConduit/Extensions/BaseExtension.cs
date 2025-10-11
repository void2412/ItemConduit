using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using ItemConduit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	/// <summary>
	/// Base class for all container extensions with node notification capabilities
	/// </summary>
	public class BaseExtension : MonoBehaviour
	{
		protected ZNetView zNetView;
		protected HashSet<BaseNode> connectedNodes = new HashSet<BaseNode>();

		public bool IsConnected => connectedNodes.Count > 0;

		protected virtual void Awake()
		{
			zNetView = GetComponentInParent<ZNetView>();
		}

		protected virtual void Start()
		{
			// Start detection after a delay to let physics settle
			StartCoroutine(DelayedNodeDetection());
		}

		protected virtual void OnDestroy()
		{
			// Notify all connected nodes that this container is being destroyed
			NotifyNearbyNodesOnRemoval();
			connectedNodes.Clear();
		}

		#region Node Detection and Notification

		/// <summary>
		/// Detect and notify nearby nodes after placement
		/// </summary>
		private IEnumerator DelayedNodeDetection()
		{
			// Wait for physics to settle
			float delay = ContainerEventConfig.containerEventDelay?.Value ?? 0.5f;
			yield return new WaitForSeconds(delay);

			// Find and notify nearby nodes
			NotifyNearbyNodesOnPlacement();
		}

		/// <summary>
		/// Find all nodes within range and notify them of container placement
		/// </summary>
		protected virtual void NotifyNearbyNodesOnPlacement()
		{
			IContainerInterface containerInterface = GetComponent<IContainerInterface>();
			if (containerInterface == null) return;

			float range = ContainerEventConfig.containerDetectionRange.Value;
			Vector3 position = transform.position;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {containerInterface.GetName()} searching for nodes within {range}m");
			}

			// Find all Extract and Insert nodes within range
			BaseNode[] allNodes = FindObjectsOfType<BaseNode>();
			int nodesNotified = 0;

			foreach (var node in allNodes)
			{
				if (node == null || !node.IsValidPlacedNode()) continue;

				// Only notify Extract and Insert nodes (not Conduit nodes)
				if (node.NodeType != NodeType.Extract && node.NodeType != NodeType.Insert) continue;

				float distance = Vector3.Distance(node.transform.position, position);

				if (distance <= range)
				{
					nodesNotified++;

					if (DebugConfig.showDebug.Value)
					{
						Logger.LogInfo($"[ItemConduit] Notifying {node.name} about container placement (distance: {distance:F1}m)");
					}

					// Notify the node about this container
					node.OnNearbyContainerPlaced(containerInterface, distance);

					// Track this node as potentially connected
					if (node.GetTargetContainer() == containerInterface)
					{
						OnNodeConnected(node);
					}
				}
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {containerInterface.GetName()} notified {nodesNotified} nodes");
			}
		}

		/// <summary>
		/// Notify nearby nodes that this container is being removed
		/// </summary>
		protected virtual void NotifyNearbyNodesOnRemoval()
		{
			IContainerInterface containerInterface = GetComponent<IContainerInterface>();
			if (containerInterface == null) return;

			float range = ContainerEventConfig.containerDetectionRange.Value;
			Vector3 position = transform.position;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {containerInterface.GetName()} being removed, notifying nearby nodes");
			}

			// Notify all connected nodes first
			foreach (var node in connectedNodes.ToList())
			{
				if (node != null)
				{
					node.OnNearbyContainerRemoved(containerInterface, 0);
				}
			}

			// Also check for any other nodes that might be affected
			BaseNode[] allNodes = FindObjectsOfType<BaseNode>();

			foreach (var node in allNodes)
			{
				if (node == null || !node.IsValidPlacedNode()) continue;
				if (connectedNodes.Contains(node)) continue; // Already notified

				// Only notify Extract and Insert nodes
				if (node.NodeType != NodeType.Extract && node.NodeType != NodeType.Insert) continue;

				float distance = Vector3.Distance(node.transform.position, position);

				if (distance <= range)
				{
					if (DebugConfig.showDebug.Value)
					{
						Logger.LogInfo($"[ItemConduit] Notifying {node.name} about container removal");
					}

					node.OnNearbyContainerRemoved(containerInterface, distance);
				}
			}
		}

		#endregion

		#region Node Connection Management

		/// <summary>
		/// Called when a node connects to this container
		/// </summary>
		public virtual void OnNodeConnected(BaseNode node)
		{
			if (node != null && (node is ExtractNode || node is InsertNode))
			{
				connectedNodes.Add(node);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {node.name} connected to container. Total connected: {connectedNodes.Count}");
				}
			}
		}

		/// <summary>
		/// Called when a node disconnects from this container
		/// </summary>
		public virtual void OnNodeDisconnected(BaseNode node)
		{
			if (node != null)
			{
				connectedNodes.Remove(node);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {node.name} disconnected from container. Remaining connected: {connectedNodes.Count}");
				}
			}
		}

		/// <summary>
		/// Clean up disconnected or invalid nodes
		/// </summary>
		protected void CleanupDisconnectedNodes()
		{
			connectedNodes.RemoveWhere(node => node == null || !node.IsValidPlacedNode());
		}

		#endregion
	}
}