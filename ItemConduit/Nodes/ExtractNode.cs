using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;

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

		/// <summary>Search radius for finding containers</summary>
		private const float CONTAINER_SEARCH_RADIUS = 2f;

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
		/// Find the nearest container to extract items from
		/// Searches for chests, smelters, beehives, etc.
		/// </summary>
		private void FindTargetContainer()
		{
			// Find all colliders within search radius
			Collider[] colliders = Physics.OverlapSphere(transform.position, CONTAINER_SEARCH_RADIUS);

			float nearestDistance = float.MaxValue;
			Container nearestContainer = null;

			foreach (Collider col in colliders)
			{
				// Check for standard container
				Container container = col.GetComponent<Container>();
				if (container != null)
				{
					float distance = Vector3.Distance(transform.position, col.transform.position);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestContainer = container;
					}
				}

				// Check for special containers (smelters, etc.)
				// These are handled by container wrapper components added via patches
			}

			targetContainer = nearestContainer;

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				if (targetContainer != null)
				{
					Debug.Log($"[ItemConduit] Extract node {name} connected to container: {targetContainer.name}");
				}
				else
				{
					Debug.Log($"[ItemConduit] Extract node {name} found no container within {CONTAINER_SEARCH_RADIUS}m");
				}
			}
		}

		/// <summary>
		/// Get the container this node is extracting from
		/// </summary>
		public Container GetTargetContainer()
		{
			// Validate container still exists
			if (targetContainer == null)
			{
				FindTargetContainer();
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
			if (targetContainer == null) return new List<ItemDrop.ItemData>();

			Inventory inventory = targetContainer.GetInventory();
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
				Debug.Log($"[ItemConduit] Extract node {name} channel set to: {ChannelId}");
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
				Debug.Log($"[ItemConduit] Extract node {name} filter set to {mode} with {filter.Count} items");
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
			if (targetContainer != null)
			{
				Inventory inv = targetContainer.GetInventory();
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