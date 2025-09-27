using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;
using Logger = Jotunn.Logger;
namespace ItemConduit.Nodes
{
	/// <summary>
	/// Extract node implementation
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

		/// <summary>Search radius for finding containers - increased for better detection</summary>
		private const float CONTAINER_SEARCH_RADIUS = 5f;

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
			FindTargetContainer();
		}

		#endregion

		#region Container Management

		/// <summary>
		/// Enhanced container finding with better debugging and detection
		/// </summary>
		private void FindTargetContainer()
		{
			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogWarning($"[ItemConduit] {name} searching for containers...");
				Logger.LogInfo($"[ItemConduit] Node position: {transform.position}");
				Logger.LogInfo($"[ItemConduit] Search radius: {CONTAINER_SEARCH_RADIUS}");
			}

			// Find all colliders within search radius
			Collider[] colliders = Physics.OverlapSphere(transform.position, CONTAINER_SEARCH_RADIUS);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogWarning($"[ItemConduit] Found {colliders.Length} colliders in range");
			}

			float nearestDistance = float.MaxValue;
			Container nearestContainer = null;

			foreach (Collider col in colliders)
			{
				if (col == null || col.gameObject == null) continue;

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Logger.LogWarning($"[ItemConduit] Checking collider: {col.name} on object: {col.gameObject.name}");
					Logger.LogInfo($"[ItemConduit] Collider position: {col.transform.position}");
					Logger.LogInfo($"[ItemConduit] Distance: {Vector3.Distance(transform.position, col.transform.position):F2}m");
					Logger.LogInfo($"[ItemConduit] Container layer: {col.gameObject.layer}");
					Logger.LogInfo($"[ItemConduit] Container layer name: {LayerMask.LayerToName(col.gameObject.layer)}");
				}

				// Check for Container component on the collider's GameObject
				Container container = col.GetComponent<Container>();

				// If not found, check parent objects (some containers have nested colliders)
				if (container == null)
				{
					container = col.GetComponentInParent<Container>();
				}

				// If still not found, check children (some containers have child colliders)
				if (container == null)
				{
					container = col.GetComponentInChildren<Container>();
				}

				if (container != null)
				{
					float distance = Vector3.Distance(transform.position, container.transform.position);

					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Logger.LogWarning($"[ItemConduit] Found Container: {container.name}");
						Logger.LogInfo($"[ItemConduit] Container m_name: {container.m_name}");
						Logger.LogInfo($"[ItemConduit] Container position: {container.transform.position}");
						Logger.LogInfo($"[ItemConduit] Distance to container: {distance:F2}m");

						// Check if container has inventory
						Inventory inv = container.GetInventory();
						if (inv != null)
						{
							Logger.LogInfo($"[ItemConduit] Container has inventory: {inv.GetWidth()}x{inv.GetHeight()} slots");
							Logger.LogInfo($"[ItemConduit] Container items: {inv.GetAllItems().Count}");
						}
						else
						{
							Logger.LogWarning($"[ItemConduit] Container has NO inventory!");
						}
					}

					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestContainer = container;
					}
				}
				else
				{
					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Logger.LogWarning($"[ItemConduit] No Container component found on {col.gameObject.name}");

						// List all components on this object for debugging
						Component[] components = col.GetComponents<Component>();
						Logger.LogInfo($"[ItemConduit] Components on {col.gameObject.name}:");
						foreach (var comp in components)
						{
							if (comp != null)
							{
								Logger.LogInfo($"[ItemConduit]   - {comp.GetType().Name}");
							}
						}
					}
				}
			}

			targetContainer = nearestContainer;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				if (targetContainer != null)
				{
					Logger.LogWarning($"[ItemConduit] *** {name} FOUND container: {targetContainer.name} at distance {nearestDistance:F2}m ***");
				}
				else
				{
					Logger.LogWarning($"[ItemConduit] *** {name} found NO containers within {CONTAINER_SEARCH_RADIUS}m ***");

					// Additional debugging - check if there are any containers in the scene at all
					DebugAllContainers();
				}
			}
		}

		/// <summary>
		/// Debug method to list all containers in the scene
		/// </summary>
		private void DebugAllContainers()
		{
			Container[] allContainers = FindObjectsOfType<Container>();
			Logger.LogWarning($"[ItemConduit] === All containers in scene ({allContainers.Length}) ===");

			foreach (var container in allContainers)
			{
				if (container != null)
				{
					float distance = Vector3.Distance(transform.position, container.transform.position);
					Logger.LogInfo($"[ItemConduit] Container: {container.name} ({container.m_name}) at {distance:F2}m");
					Logger.LogInfo($"[ItemConduit]   Position: {container.transform.position}");
					Logger.LogInfo($"[ItemConduit]   Has Inventory: {container.GetInventory() != null}");
					Logger.LogInfo($"[ItemConduit]   Layer: {container.gameObject.layer} ({LayerMask.LayerToName(container.gameObject.layer)})");
				}
			}
		}

		/// <summary>
		/// Alternative method using raycast detection for more precise container finding
		/// </summary>
		private Container FindContainerWithRaycast()
		{
			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] {name} trying raycast detection...");
			}

			// Cast rays in multiple directions to find containers
			Vector3[] directions = {
				Vector3.forward,
				Vector3.back,
				Vector3.left,
				Vector3.right,
				Vector3.up,
				Vector3.down
			};

			float maxDistance = CONTAINER_SEARCH_RADIUS;

			foreach (Vector3 direction in directions)
			{
				Vector3 worldDirection = transform.TransformDirection(direction);

				if (Physics.Raycast(transform.position, worldDirection, out RaycastHit hit, maxDistance))
				{
					if (ItemConduitMod.ShowDebugInfo.Value)
					{
						Logger.LogWarning($"[ItemConduit] Raycast hit: {hit.collider.name} at {hit.distance:F2}m in direction {direction}");
					}

					Container container = hit.collider.GetComponent<Container>();
					if (container == null)
					{
						container = hit.collider.GetComponentInParent<Container>();
					}
					if (container == null)
					{
						container = hit.collider.GetComponentInChildren<Container>();
					}

					if (container != null)
					{
						if (ItemConduitMod.ShowDebugInfo.Value)
						{
							Logger.LogWarning($"[ItemConduit] *** Raycast found container: {container.name} ***");
						}
						return container;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Enhanced GetTargetContainer method with fallback detection
		/// </summary>
		public Container GetTargetContainer()
		{
			// First try the normal detection
			if (targetContainer == null)
			{
				FindTargetContainer();
			}

			// If still no container found, try raycast detection
			if (targetContainer == null)
			{
				targetContainer = FindContainerWithRaycast();
			}

			// If still no container, try increasing search radius temporarily
			if (targetContainer == null && ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogWarning($"[ItemConduit] {name} trying expanded search...");

				// Temporarily increase search radius
				const float expandedRadius = 10f; // Increase to 10 meters

				Collider[] expandedColliders = Physics.OverlapSphere(transform.position, expandedRadius);
				Logger.LogWarning($"[ItemConduit] Expanded search found {expandedColliders.Length} colliders");

				foreach (Collider col in expandedColliders)
				{
					if (col == null) continue;

					Container container = col.GetComponent<Container>();
					if (container == null)
						container = col.GetComponentInParent<Container>();
					if (container == null)
						container = col.GetComponentInChildren<Container>();

					if (container != null)
					{
						float distance = Vector3.Distance(transform.position, container.transform.position);
						Logger.LogWarning($"[ItemConduit] Found container '{container.name}' at {distance:F2}m (outside normal range)");
					}
				}
			}

			return targetContainer;
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
				Logger.LogWarning($"[ItemConduit] Extract node {name} channel set to: {ChannelId}");
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
				Logger.LogWarning($"[ItemConduit] Extract node {name} filter set to {mode} with {filter.Count} items");
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