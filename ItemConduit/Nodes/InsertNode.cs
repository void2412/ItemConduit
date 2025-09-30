using System.Collections;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Config;
using Logger = Jotunn.Logger;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Insert node implementation - pushes items into containers
	/// Container detection is handled by base class
	/// </summary>
	public class InsertNode : BaseNode
	{
		#region Configuration Properties

		/// <summary>Channel ID for receiving items from specific extract nodes</summary>
		public string ChannelId { get; private set; } = "None";

		/// <summary>Priority level for filling order (higher = filled first)</summary>
		public int Priority { get; private set; } = 0;

		#endregion

		#region Private Fields

		/// <summary>GUI component for configuration</summary>
		private InsertNodeGUI gui;

		/// <summary>Time since last insertion</summary>
		private float lastInsertionTime;

		#endregion

		#region Overrides

		/// <summary>
		/// Insert nodes can connect to containers
		/// </summary>
		protected override bool CanConnectToContainers => true;

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Initialize insert node with additional RPCs
		/// </summary>
		protected override void Awake()
		{
			base.Awake();
			NodeType = NodeType.Insert;

			// Register additional RPCs for insert node configuration
			if (zNetView != null && zNetView.IsValid())
			{
				zNetView.Register<string>("RPC_UpdateChannel", RPC_UpdateChannel);
				zNetView.Register<int>("RPC_UpdatePriority", RPC_UpdatePriority);
			}
		}

		#endregion

		#region Container Detection Override

		/// <summary>
		/// Override to store container reference when found
		/// </summary>
		protected override IEnumerator ProcessContainerConnection(Collider[] overlaps)
		{
			// Use base class helper to find best container
			targetContainer = FindBestOverlappingContainer(overlaps);

			if (DebugConfig.showDebug.Value)
			{
				if (targetContainer != null)
				{
					Logger.LogWarning($"[ItemConduit] Insert node {name} connected to container: {targetContainer.m_name}");

					Inventory inv = targetContainer.GetInventory();
					if (inv != null)
					{
						Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots");
						Logger.LogInfo($"[ItemConduit]   Empty slots: {inv.GetEmptySlots()}");
						Logger.LogInfo($"[ItemConduit]   Items: {inv.GetAllItems().Count}");
					}
				}
				else
				{
					Logger.LogWarning($"[ItemConduit] Insert node {name} found NO container");
				}
			}

			yield return null;
		}

		/// <summary>
		/// Override to return stored container reference
		/// </summary>
		public override Container GetTargetContainer()
		{
			return targetContainer;
		}


		#endregion

		#region Item Insertion

		/// <summary>
		/// Check if an item can be inserted into the container
		/// </summary>
		/// <param name="item">The item to check</param>
		/// <returns>True if the item can be inserted</returns>
		public bool CanInsertItem(ItemDrop.ItemData item)
		{
			Container container = GetTargetContainer();
			if (container == null || item == null) return false;

			Inventory inventory = container.GetInventory();
			if (inventory == null) return false;

			// Check if inventory has space
			return inventory.CanAddItem(item);
		}

		/// <summary>
		/// Insert an item into the container
		/// </summary>
		/// <param name="item">The item to insert</param>
		/// <returns>True if insertion was successful</returns>
		public bool InsertItem(ItemDrop.ItemData item)
		{
			if (!CanInsertItem(item)) return false;

			Container container = GetTargetContainer();
			Inventory inventory = container.GetInventory();
			bool success = inventory.AddItem(item);

			if (success)
			{
				lastInsertionTime = Time.time;

				// Visual feedback
				if (VisualConfig.transferVisualEffect.Value)
				{
					StartCoroutine(InsertFlashEffect());
				}

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Insert node {name} inserted {item.m_shared.m_name} x{item.m_stack}");
				}
			}

			return success;
		}

		/// <summary>
		/// Get available space in the container
		/// </summary>
		/// <returns>Number of free slots</returns>
		public int GetAvailableSpace()
		{
			Container container = GetTargetContainer();
			if (container == null) return 0;

			Inventory inventory = container.GetInventory();
			if (inventory == null) return 0;

			return inventory.GetEmptySlots();
		}

		/// <summary>
		/// Check if container is full
		/// </summary>
		/// <returns>True if container has no empty slots</returns>
		public bool IsContainerFull()
		{
			return GetAvailableSpace() == 0;
		}

		#endregion

		#region Configuration

		/// <summary>
		/// Set the channel ID for this insert node
		/// </summary>
		/// <param name="channelId">The channel ID to set</param>
		public void SetChannel(string channelId)
		{
			ChannelId = string.IsNullOrEmpty(channelId) ? "None" : channelId;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdateChannel", ChannelId);
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Insert node {name} channel set to: {ChannelId}");
			}

			// Request network rebuild to update routing
			if (IsValidPlacedNode())
			{
				Network.RebuildManager.Instance.RequestRebuildForNode(this);
			}
		}

		/// <summary>
		/// Set the priority for this insert node
		/// </summary>
		/// <param name="priority">The priority value (higher = filled first)</param>
		public void SetPriority(int priority)
		{
			Priority = priority;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdatePriority", Priority);
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Insert node {name} priority set to: {Priority}");
			}

			// Request network rebuild to update transfer ordering
			if (IsValidPlacedNode())
			{
				Network.RebuildManager.Instance.RequestRebuildForNode(this);
			}
		}

		#endregion

		#region RPC Handlers

		/// <summary>
		/// RPC handler for channel updates
		/// </summary>
		private void RPC_UpdateChannel(long sender, string channelId)
		{
			ChannelId = channelId;
		}

		/// <summary>
		/// RPC handler for priority updates
		/// </summary>
		private void RPC_UpdatePriority(long sender, int priority)
		{
			Priority = priority;
		}

		#endregion

		#region User Interaction

		/// <summary>
		/// Handle player interaction to open configuration GUI
		/// </summary>
		public override bool Interact(Humanoid user, bool hold, bool alt)
		{
			// Only local player can configure
			if (user != Player.m_localPlayer) return false;

			// Don't allow interaction with ghost pieces
			if (!IsValidPlacedNode()) return false;

			// Create GUI if it doesn't exist
			if (gui == null)
			{
				GameObject guiObj = new GameObject("InsertNodeGUI");
				gui = guiObj.AddComponent<InsertNodeGUI>();
				gui.Initialize(this);
			}

			// Show the GUI
			gui.Show();
			return true;
		}

		/// <summary>
		/// Provide detailed hover text for insert nodes
		/// </summary>
		public override string GetHoverText()
		{
			if (!IsValidPlacedNode()) return "";

			string baseText = base.GetHoverText();

			// Add channel info
			string channelInfo = $"[Channel: <color=cyan>{ChannelId}</color>]";

			// Add priority info
			string priorityColor = Priority > 0 ? "yellow" : Priority < 0 ? "gray" : "white";
			string priorityInfo = $"[Priority: <color={priorityColor}>{Priority}</color>]";

			// Add container status
			string containerStatus;
			Container container = GetTargetContainer();
			if (container != null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null)
				{
					int emptySlots = inv.GetEmptySlots();
					int totalSlots = inv.GetWidth() * inv.GetHeight();
					int usedSlots = totalSlots - emptySlots;

					string fullnessColor = emptySlots > 0 ? "green" : "red";
					containerStatus = $"[Container: <color={fullnessColor}>Connected</color> ({usedSlots}/{totalSlots} slots)]";
				}
				else
				{
					containerStatus = "[Container: <color=yellow>Connected (Invalid)</color>]";
				}
			}
			else
			{
				containerStatus = "[Container: <color=red>Not Connected</color>]";
			}

			// Add interaction hint
			string interactionHint = "\n[<color=yellow>E</color>] Configure";

			return $"{baseText}\n{channelInfo}\n{priorityInfo}\n{containerStatus}{interactionHint}";
		}

		#endregion

		#region Visual Updates

		/// <summary>
		/// Update visual state for insert node
		/// </summary>
		protected override void UpdateVisualState(bool active)
		{
			base.UpdateVisualState(active);

			if (!VisualConfig.transferVisualEffect.Value) return;

			// Add blue pulse effect when inserting
			if (active && Time.time - lastInsertionTime < 1f)
			{
				MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
				if (renderer != null)
				{
					// Pulse effect
					float pulse = Mathf.PingPong(Time.time * 2f, 1f);
					renderer.material.SetColor("_EmissionColor", Color.blue * (0.3f + pulse * 0.3f));
				}
			}
		}

		/// <summary>
		/// Visual flash when inserting an item
		/// </summary>
		private IEnumerator InsertFlashEffect()
		{
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				Color originalEmission = renderer.material.GetColor("_EmissionColor");
				renderer.material.SetColor("_EmissionColor", Color.cyan);
				yield return new WaitForSeconds(0.2f);
				renderer.material.SetColor("_EmissionColor", originalEmission);
			}
		}

		/// <summary>
		/// Called when an item is successfully inserted
		/// </summary>
		public void OnItemInserted()
		{
			lastInsertionTime = Time.time;

			// Additional visual or audio feedback could be added here
			if (VisualConfig.transferVisualEffect.Value)
			{
				// Could add particle effect or sound
			}
		}

		#endregion

		#region Comparison Support

		/// <summary>
		/// Compare insert nodes for sorting by priority and distance
		/// Used by the network manager for transfer ordering
		/// </summary>
		public class PriorityComparer : System.Collections.Generic.IComparer<InsertNode>
		{
			private BaseNode sourceNode;

			public PriorityComparer(BaseNode source)
			{
				sourceNode = source;
			}

			public int Compare(InsertNode x, InsertNode y)
			{
				if (x == null || y == null) return 0;

				// First compare by priority (higher first)
				int priorityComparison = y.Priority.CompareTo(x.Priority);
				if (priorityComparison != 0) return priorityComparison;

				// Then compare by distance (closer first)
				if (sourceNode != null)
				{
					float distX = Vector3.Distance(sourceNode.transform.position, x.transform.position);
					float distY = Vector3.Distance(sourceNode.transform.position, y.transform.position);
					return distX.CompareTo(distY);
				}

				return 0;
			}
		}

		#endregion

		#region Debug

		/// <summary>
		/// Force refresh container connection (useful for debugging)
		/// </summary>
		public void RefreshContainerConnection()
		{
			RefreshDetection();
		}

		/// <summary>
		/// Get debug information about this insert node
		/// </summary>
		public string GetDebugInfo()
		{
			Container container = GetTargetContainer();
			string containerInfo = container != null ? $"{container.m_name} ({GetAvailableSpace()} free slots)" : "No container";

			return $"InsertNode: {name}\n" +
				   $"  Channel: {ChannelId}\n" +
				   $"  Priority: {Priority}\n" +
				   $"  Container: {containerInfo}\n" +
				   $"  Network: {NetworkId ?? "None"}\n" +
				   $"  Active: {IsActive}\n" +
				   $"  Connected Nodes: {connectedNodes.Count}";
		}

		#endregion
	}
}