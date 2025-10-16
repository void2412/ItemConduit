using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using ItemConduit.Patches;
using ItemConduit.Utils;
using Jotunn.Configs;
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
	public class BaseExtension<T> : ExtensionNodeManagement where T : MonoBehaviour
	{
		protected ZNetView zNetView;
		
		public Container m_container;
		protected T component;
		public int m_width = 1;
		public int m_height = 1;

		public bool IsConnected => connectedNodes.Count > 0;

		


		#region Container

		protected void SetupContainer(int width, int height)
		{
			m_container = component.gameObject.AddComponent<Container>();
			m_container.m_width = width;
			m_container.m_height = height;
			m_container.m_inventory = new Inventory($"{component.GetType().ToString()} Output", null, width, height);
			m_container.name = $"{component.GetType().ToString()} Output";
		}

		public void SaveInventoryToZDO()
		{
			if (component == null || m_container == null) return;

			ZNetView znetView = component.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_Inventory", pkg.GetBase64());
		}

		public void LoadInventoryFromZDO()
		{
			if (component == null || m_container == null) return;

			ZNetView znetView = component.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			string data = zdo.GetString("ItemConduit_Inventory", "");
			if (!string.IsNullOrEmpty(data))
			{
				ZPackage pkg = new ZPackage(data);
				m_container.m_inventory.Load(pkg);
			}
		}

		#endregion

		#region Unity Life Cycle

		protected virtual void Awake()
		{
			component = GetComponentInParent<T>();
			if (component == null)
			{
				component = GetComponent<T>();
				if (component == null)
				{
					component = GetComponentInChildren<T>();
				}
			}

			if (component == null)
			{
				Logger.LogError($"[ItemConduit] Extension could not find {component.GetType().ToString()} component!");
				return;
			}

			zNetView = component.GetComponent<ZNetView>();
			if (zNetView == null || !zNetView.IsValid())
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping container creation - invalid ZNetView");
				}
				return;
			}

			SetupContainer(m_width, m_height);
			LoadInventoryFromZDO();
		}
		protected virtual void Start()
		{
			// Start detection after a delay to let physics settle
			StartCoroutine(DelayedNodeDetection());

			if(m_container != null &&  m_container.m_inventory != null)
			{
				InventoryPatches.RegisterInventory(
			m_container.m_inventory,
			OnItemAdded,
			OnItemRemoved
		);
			}
		}

		protected virtual void OnDestroy()
		{
			if (m_container != null && m_container.m_inventory != null)
			{
				InventoryPatches.UnregisterInventory(m_container.m_inventory);
			}
			SaveInventoryToZDO();
			// Notify all connected nodes that this container is being destroyed
			NotifyNearbyNodesOnRemoval();
			connectedNodes.Clear();
		}

		/// <summary>
		/// Called when an item is added to this extension's inventory
		/// </summary>
		protected virtual void OnItemAdded(ItemDrop.ItemData item)
		{
			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Item added to {GetType().Name}: {item.m_shared.m_name} x{item.m_stack}");
			}

			SaveInventoryToZDO();
		}

		/// <summary>
		/// Called when an item is removed from this extension's inventory
		/// </summary>
		protected virtual void OnItemRemoved(ItemDrop.ItemData item, int amount)
		{
			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Item removed from {GetType().Name}: {item.m_shared.m_name} x{amount}");
			}

			SaveInventoryToZDO();
		}

		#endregion

		#region Node Management

		public override void OnNodeDisconnected(BaseNode node)
		{
			SaveInventoryToZDO();
			base.OnNodeDisconnected(node);
		}

		#endregion


	}

	public class ExtensionNodeManagement : MonoBehaviour
	{
		protected HashSet<BaseNode> connectedNodes = new HashSet<BaseNode>();


		#region Node Detection and Notification

		/// <summary>
		/// Detect and notify nearby nodes after placement
		/// </summary>
		protected IEnumerator DelayedNodeDetection()
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
			BaseNode[] allNodes = FindObjectsByType<BaseNode>(FindObjectsSortMode.None);
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
			BaseNode[] allNodes = FindObjectsByType<BaseNode>(FindObjectsSortMode.None);

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