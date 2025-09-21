using ItemConduit.Core;
using ItemConduit.Network;
using Jotunn.Managers;
using System;
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
		public float NodeLength { get; set; }

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
				Debug.Log($"[ItemConduit] Node {name} awakened (Type: {NodeType})");
			}
		}

		/// <summary>
		/// Register node with network manager on start
		/// Only runs on server/host
		/// </summary>
		protected virtual void Start()
		{
			// Only register on server
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				// Find initial connections
				FindConnections();

				// Register with network manager
				ItemConduit.Network.NetworkManager.Instance.RegisterNode(this);

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Node {name} registered with NetworkManager");
				}
			}
		}

		/// <summary>
		/// Cleanup on node destruction
		/// Unregisters from network manager
		/// </summary>
		protected virtual void OnDestroy()
		{
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
		/// Find and establish connections to nearby nodes
		/// Uses physics overlap to detect nodes within range
		/// </summary>
		public virtual void FindConnections()
		{
			// Clear existing connections
			connectedNodes.Clear();

			// Calculate search radius based on node length and config
			float searchRadius = NodeLength + ItemConduitMod.ConnectionRange.Value;
			Debug.Log($"search radius: {searchRadius}");
			// Find all colliders in range
			Collider[] colliders = Physics.OverlapSphere(transform.position, searchRadius);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] {name} searching for connections in {searchRadius}m radius, found {colliders.Length} colliders");
			}

			foreach (Collider col in colliders)
			{
				// Skip self
				if (col.gameObject == gameObject) continue;

				// Check if it's another node
				BaseNode otherNode = col.GetComponent<BaseNode>();
				if (otherNode != null && CanConnectTo(otherNode))
				{
					// Verify connection distance
					float distance = Vector3.Distance(transform.position, otherNode.transform.position);
					float maxDistance = (NodeLength + otherNode.NodeLength) / 2f + ItemConduitMod.ConnectionRange.Value;

					if (distance <= maxDistance)
					{
						connectedNodes.Add(otherNode);

						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Debug.Log($"[ItemConduit] {name} connected to {otherNode.name} (distance: {distance:F2}m)");
						}
					}
				}
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] {name} established {connectedNodes.Count} connections");
			}
		}

		/// <summary>
		/// Check if this node can connect to another node based on type rules
		/// </summary>
		/// <param name="other">The other node to check connection compatibility</param>
		/// <returns>True if connection is allowed</returns>
		protected virtual bool CanConnectTo(BaseNode other)
		{
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
			// TODO: Add particle effects, material changes, etc.
		}

		#endregion

		#region Hoverable Interface

		/// <summary>
		/// Get hover text displayed when looking at node
		/// </summary>
		public virtual string GetHoverText()
		{
			string status = IsActive ? "<color=green>Active</color>" : "<color=red>Inactive</color>";
			string network = NetworkId ?? "No Network";
			return $"{GetNodeTypeName()}\n[Network: {network}]\n[Status: {status}]";
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

			// Draw node position
			Gizmos.color = NodeType switch
			{
				NodeType.Conduit => Color.gray,
				NodeType.Extract => Color.green,
				NodeType.Insert => Color.blue,
				_ => Color.white
			};

			Gizmos.DrawWireSphere(transform.position, 0.2f);

			// Draw connections
			if (connectedNodes != null)
			{
				Gizmos.color = Color.yellow;
				foreach (var node in connectedNodes)
				{
					if (node != null)
					{
						Gizmos.DrawLine(transform.position, node.transform.position);
					}
				}
			}

			// Draw search radius
			Gizmos.color = new Color(1, 1, 0, 0.2f);
			Gizmos.DrawWireSphere(transform.position, NodeLength + ItemConduitMod.ConnectionRange.Value);
		}

		#endregion
	}
}