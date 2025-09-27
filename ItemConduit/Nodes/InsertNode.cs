using System.Collections;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;
using Logger = Jotunn.Logger;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Insert node implementation with optimized container detection
	/// Receives items from the network and inserts them into attached containers
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

		/// <summary>Reference to the container this node inserts into</summary>
		private Container targetContainer;

		/// <summary>GUI component for configuration</summary>
		private InsertNodeGUI gui;

		/// <summary>Coroutine for container finding</summary>
		private Coroutine containerCoroutine;

		/// <summary>Time since last insertion</summary>
		private float lastInsertionTime;

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

		/// <summary>
		/// Find target container on start
		/// </summary>
		protected override void Start()
		{
			base.Start();

			// Only start container detection for valid placed nodes
			if (IsValidPlacedNode())
			{
				StartContainerDetection();
			}
		}

		/// <summary>
		/// Cleanup
		/// </summary>
		protected override void OnDestroy()
		{
			if (containerCoroutine != null)
			{
				StopCoroutine(containerCoroutine);
				containerCoroutine = null;
			}

			base.OnDestroy();
		}

		#endregion

		#region Optimized Container Management

		/// <summary>
		/// Start container detection process
		/// </summary>
		public void StartContainerDetection()
		{
			if (!IsValidPlacedNode()) return;

			if (containerCoroutine != null)
			{
				StopCoroutine(containerCoroutine);
			}
			containerCoroutine = StartCoroutine(FindTargetContainerCoroutine());
		}

		/// <summary>
		/// Optimized container finding using physics overlap detection
		/// Only considers containers that physically overlap with the node
		/// </summary>
		private IEnumerator FindTargetContainerCoroutine()
		{
			// Wait for placement to settle
			yield return new WaitForSeconds(0.2f);

			targetContainer = null;

			// Get the bounds of this node
			Bounds nodeBounds = GetNodeBounds();

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} searching for overlapping containers...");
				Logger.LogInfo($"[ItemConduit] Node bounds center: {nodeBounds.center}, size: {nodeBounds.size}");
			}

			// Use OverlapBox for precise detection of overlapping objects
			Collider[] overlaps = Physics.OverlapBox(
				nodeBounds.center,
				nodeBounds.extents * 1.2f, // Slightly expanded to catch edge cases
				transform.rotation,
				LayerMask.GetMask("piece", "piece_nonsolid", "item", "Default_small")
			);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Found {overlaps.Length} overlapping colliders");
			}

			float closestDistance = float.MaxValue;
			Container bestContainer = null;
			int foundContainerCount = 0;

			foreach (var col in overlaps)
			{
				if (col == null || col.transform == transform) continue;

				// Try multiple ways to find container
				Container container = col.GetComponent<Container>();
				if (container == null)
					container = col.GetComponentInParent<Container>();
				if (container == null)
					container = col.GetComponentInChildren<Container>();

				if (container != null)
				{
					// Get the container's collider for bounds checking
					Collider containerCollider = container.GetComponent<Collider>();
					if (containerCollider == null)
					{
						// Try to get collider from children
						containerCollider = container.GetComponentInChildren<Collider>();
					}

					if (containerCollider != null)
					{
						Bounds containerBounds = containerCollider.bounds;

						// Check for actual intersection between node and container
						if (nodeBounds.Intersects(containerBounds))
						{
							float distance = Vector3.Distance(transform.position, container.transform.position);
							foundContainerCount++;

							if (ItemConduitMod.ShowDebugInfo.Value)
							{
								Logger.LogWarning($"[ItemConduit] Found overlapping container: {container.name} ({container.m_name}) at distance {distance:F2}m");

								Inventory inv = container.GetInventory();
								if (inv != null)
								{
									Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots");
									Logger.LogInfo($"[ItemConduit]   Empty slots: {inv.GetEmptySlots()}");
									Logger.LogInfo($"[ItemConduit]   Items: {inv.GetAllItems().Count}");
								}
							}

							if (distance < closestDistance)
							{
								closestDistance = distance;
								bestContainer = container;
							}
						}
						else
						{
							if (ItemConduitMod.ShowDebugInfo.Value)
							{
								Logger.LogInfo($"[ItemConduit] Container {container.name} found but bounds don't intersect");
							}
						}
					}
				}
			}

			// Also check if the node is placed directly on a container using raycast
			if (bestContainer == null)
			{
				RaycastHit hit;
				// Cast ray downward from node center
				if (Physics.Raycast(transform.position, Vector3.down, out hit, 2f, LayerMask.GetMask("piece", "piece_nonsolid", "Default_small")))
				{
					Container container = hit.collider.GetComponent<Container>();
					if (container == null)
						container = hit.collider.GetComponentInParent<Container>();

					if (container != null && container.GetInventory() != null)
					{
						bestContainer = container;

						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Logger.LogWarning($"[ItemConduit] Found container below node: {container.name} ({container.m_name})");
						}
					}
				}
			}

			targetContainer = bestContainer;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				if (targetContainer != null)
				{
					Logger.LogWarning($"[ItemConduit] *** {name} connected to container: {targetContainer.name} ({targetContainer.m_name}) ***");
				}
				else
				{
					Logger.LogWarning($"[ItemConduit] *** {name} found NO overlapping containers ***");
					if (foundContainerCount > 0)
					{
						Logger.LogInfo($"[ItemConduit] Note: Found {foundContainerCount} containers nearby but bounds didn't intersect properly");
					}
				}
			}
		}

		/// <summary>
		/// Get the bounds of this node for overlap detection
		/// </summary>
		private Bounds GetNodeBounds()
		{
			// Get all colliders on this node
			Collider[] colliders = GetComponentsInChildren<Collider>();

			if (colliders.Length == 0)
			{
				// Fallback to calculated bounds
				return new Bounds(transform.position, new Vector3(0.5f, 0.5f, NodeLength));
			}

			// Find the main collider (non-trigger)
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

			// If all colliders are triggers, use the first one but expand it slightly
			Bounds bounds = colliders[0].bounds;
			bounds.Expand(0.1f);
			return bounds;
		}

		/// <summary>
		/// Get the target container (with re-detection if needed)
		/// </summary>
		public Container GetTargetContainer()
		{
			// If we don't have a container, try to find one
			if (targetContainer == null && IsValidPlacedNode())
			{
				StartContainerDetection();
			}

			return targetContainer;
		}

		/// <summary>
		/// Force re-detection of container
		/// </summary>
		public void RefreshContainerConnection()
		{
			if (IsValidPlacedNode())
			{
				StartContainerDetection();
			}
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
				if (ItemConduitMod.EnableVisualEffects.Value)
				{
					// Flash effect on successful insertion
					StartCoroutine(FlashEffect());
				}

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogInfo($"[ItemConduit] Insert node {name} inserted {item.m_shared.m_name} x{item.m_stack}");
				}
			}

			return success;
		}

		/// <summary>
		/// Visual feedback for item insertion
		/// </summary>
		private IEnumerator FlashEffect()
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

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Insert node {name} channel set to: {ChannelId}");
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

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Insert node {name} priority set to: {Priority}");
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
			string priorityColor = Priority > 0 ? "yellow" : "white";
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

			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Apply blue tint when active
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				if (active)
				{
					renderer.material.EnableKeyword("_EMISSION");
					renderer.material.SetColor("_EmissionColor", Color.blue * 0.3f);
				}
				else
				{
					renderer.material.DisableKeyword("_EMISSION");
				}
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
	}
}