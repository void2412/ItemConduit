using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;
using Logger = Jotunn.Logger;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Extract node implementation with optimized container detection
	/// Pulls items from attached containers and sends them through the network
	/// </summary>
	public class ExtractNode : BaseNode
	{
		#region Configuration Properties

		/// <summary>Channel ID for routing items to specific insert nodes</summary>
		public string ChannelId { get; private set; } = "None";

		/// <summary>Set of item names to filter (whitelist or blacklist)</summary>
		public HashSet<string> ItemFilter { get; private set; } = new HashSet<string>();

		/// <summary>Whether the filter is a whitelist (true) or blacklist (false)</summary>
		public bool IsWhitelist { get; private set; } = true;

		#endregion

		#region Private Fields

		/// <summary>Reference to the container this node extracts from</summary>
		private Container targetContainer;

		/// <summary>GUI component for configuration</summary>
		private ExtractNodeGUI gui;

		/// <summary>Coroutine for container finding</summary>
		private Coroutine containerCoroutine;

		/// <summary>Time since last extraction attempt</summary>
		private float lastExtractionTime;

		#endregion

		#region Unity Lifecycle

		/// <summary>
		/// Initialize extract node with additional RPCs
		/// </summary>
		protected override void Awake()
		{
			base.Awake();
			NodeType = NodeType.Extract;

			// Register additional RPCs for extract node configuration
			if (zNetView != null && zNetView.IsValid())
			{
				zNetView.Register<string>("RPC_UpdateChannel", RPC_UpdateChannel);
				zNetView.Register<string, bool>("RPC_UpdateFilter", RPC_UpdateFilter);
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
			List<Container> foundContainers = new List<Container>();

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
						// Try to get collider from children (some containers have colliders on child objects)
						containerCollider = container.GetComponentInChildren<Collider>();
					}

					if (containerCollider != null)
					{
						Bounds containerBounds = containerCollider.bounds;

						// Check for actual intersection between node and container
						if (nodeBounds.Intersects(containerBounds))
						{
							float distance = Vector3.Distance(transform.position, container.transform.position);

							if (ItemConduitMod.ShowDebugInfo.Value)
							{
								Logger.LogWarning($"[ItemConduit] Found overlapping container: {container.name} ({container.m_name}) at distance {distance:F2}m");

								Inventory inv = container.GetInventory();
								if (inv != null)
								{
									Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots, {inv.GetAllItems().Count} items");
								}
							}

							foundContainers.Add(container);

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
					if (foundContainers.Count > 0)
					{
						Logger.LogInfo($"[ItemConduit] Note: Found {foundContainers.Count} containers nearby but bounds didn't intersect properly");
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
				// Fallback to calculated bounds based on node position and size
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
		/// Force re-detection of container (useful after containers are moved)
		/// </summary>
		public void RefreshContainerConnection()
		{
			if (IsValidPlacedNode())
			{
				StartContainerDetection();
			}
		}

		#endregion

		#region Item Filtering

		/// <summary>
		/// Check if an item can be extracted based on filter settings
		/// </summary>
		/// <param name="item">The item to check</param>
		/// <returns>True if the item can be extracted</returns>
		public bool CanExtractItem(ItemDrop.ItemData item)
		{
			if (item == null) return false;

			// No filter means extract everything
			if (ItemFilter.Count == 0) return true;

			// Get item name for filtering
			string itemName = item.m_dropPrefab?.name ?? item.m_shared.m_name;
			bool inFilter = ItemFilter.Contains(itemName);

			// Apply whitelist/blacklist logic
			return IsWhitelist ? inFilter : !inFilter;
		}

		/// <summary>
		/// Get all items that can be extracted from the container
		/// </summary>
		/// <returns>List of extractable items</returns>
		public List<ItemDrop.ItemData> GetExtractableItems()
		{
			Container container = GetTargetContainer();
			if (container == null) return new List<ItemDrop.ItemData>();

			Inventory inventory = container.GetInventory();
			if (inventory == null) return new List<ItemDrop.ItemData>();

			// Filter items based on extraction rules
			return inventory.GetAllItems()
				.Where(item => CanExtractItem(item))
				.ToList();
		}

		#endregion

		#region Configuration

		/// <summary>
		/// Set the channel ID for this extract node
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
				Logger.LogInfo($"[ItemConduit] Extract node {name} channel set to: {ChannelId}");
			}
		}

		/// <summary>
		/// Set the item filter for this extract node
		/// </summary>
		/// <param name="filter">Set of item names to filter</param>
		/// <param name="isWhitelist">Whether to use whitelist or blacklist mode</param>
		public void SetFilter(HashSet<string> filter, bool isWhitelist)
		{
			ItemFilter = new HashSet<string>(filter);
			IsWhitelist = isWhitelist;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				string filterStr = string.Join(",", filter);
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdateFilter", filterStr, isWhitelist);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				string mode = isWhitelist ? "whitelist" : "blacklist";
				Logger.LogInfo($"[ItemConduit] Extract node {name} filter set to {mode} with {filter.Count} items");
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
		/// RPC handler for filter updates
		/// </summary>
		private void RPC_UpdateFilter(long sender, string filterStr, bool isWhitelist)
		{
			// Parse filter string back into HashSet
			ItemFilter = new HashSet<string>(
				filterStr.Split(',')
					.Where(s => !string.IsNullOrEmpty(s))
					.Select(s => s.Trim())
			);
			IsWhitelist = isWhitelist;
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
				GameObject guiObj = new GameObject("ExtractNodeGUI");
				gui = guiObj.AddComponent<ExtractNodeGUI>();
				gui.Initialize(this);
			}

			// Show the GUI
			gui.Show();
			return true;
		}

		/// <summary>
		/// Provide detailed hover text for extract nodes
		/// </summary>
		public override string GetHoverText()
		{
			if (!IsValidPlacedNode()) return "";

			string baseText = base.GetHoverText();

			// Add channel info
			string channelInfo = $"[Channel: <color=cyan>{ChannelId}</color>]";

			// Add container status
			string containerStatus;
			Container container = GetTargetContainer();
			if (container != null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null)
				{
					int itemCount = inv.GetAllItems().Count;
					int extractableCount = GetExtractableItems().Count;
					containerStatus = $"[Container: <color=green>Connected</color> ({extractableCount}/{itemCount} items)]";
				}
				else
				{
					containerStatus = "[Container: <color=yellow>Connected (Empty)</color>]";
				}
			}
			else
			{
				containerStatus = "[Container: <color=red>Not Connected</color>]";
			}

			// Add filter info
			string filterInfo = "";
			if (ItemFilter.Count > 0)
			{
				string filterMode = IsWhitelist ? "Whitelist" : "Blacklist";
				filterInfo = $"\n[Filter: {filterMode} ({ItemFilter.Count} items)]";
			}

			// Add interaction hint
			string interactionHint = "\n[<color=yellow>E</color>] Configure";

			return $"{baseText}\n{channelInfo}\n{containerStatus}{filterInfo}{interactionHint}";
		}

		#endregion

		#region Visual Updates

		/// <summary>
		/// Update visual state for extract node
		/// </summary>
		protected override void UpdateVisualState(bool active)
		{
			base.UpdateVisualState(active);

			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Apply green tint when active
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				if (active)
				{
					renderer.material.EnableKeyword("_EMISSION");
					renderer.material.SetColor("_EmissionColor", Color.green * 0.3f);
				}
				else
				{
					renderer.material.DisableKeyword("_EMISSION");
				}
			}
		}

		#endregion
	}
}