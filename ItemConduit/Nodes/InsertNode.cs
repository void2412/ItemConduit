using ItemConduit.Config;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Interfaces;
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

		/// <summary>Set of item names to filter (whitelist or blacklist)</summary>
		public HashSet<string> ItemFilter { get; private set; } = new HashSet<string>();

		/// <summary>Whether the filter is a whitelist (true) or blacklist (false)</summary>
		public bool IsWhitelist { get; private set; } = true;

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
				zNetView.Register<string, bool>("RPC_UpdateFilter", RPC_UpdateFilter);
			}
		}

		protected override void Start()
		{
			base.Start();

			if (!isGhostPiece && zNetView != null && zNetView.IsValid()) {
				LoadFromZDO();
			}
		}

		private void LoadFromZDO()
		{
			ZDO zdo = zNetView.GetZDO();
			if (zdo == null) return;

			// Load channel
			string savedChannel = zdo.GetString("ItemConduit_Channel", "None");
			ChannelId = savedChannel;

			// Load priority
			int savedPriority = zdo.GetInt("ItemConduit_Priority", 0);
			Priority = savedPriority;

			// Load filter
			string savedFilter = zdo.GetString("ItemConduit_Filter", "");
			bool savedIsWhitelist = zdo.GetBool("ItemConduit_IsWhitelist", true);

			if (!string.IsNullOrEmpty(savedFilter))
			{
				ItemFilter = new HashSet<string>(
					savedFilter.Split(',')
						.Where(s => !string.IsNullOrEmpty(s))
						.Select(s => s.Trim())
				);
			}
			IsWhitelist = savedIsWhitelist;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Loaded InsertNode config - Channel: {ChannelId}, Priority: {Priority}, Filter items: {ItemFilter.Count}");
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
					Logger.LogWarning($"[ItemConduit] Insert node {name} connected to container: {targetContainer.GetName()}");

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
		public override IContainerInterface GetTargetContainer()
		{
			return targetContainer;
		}

		#endregion

		#region Item Filtering

		/// <summary>
		/// Check if an item can be inserted based on filter settings
		/// </summary>
		/// <param name="item">The item to check</param>
		/// <returns>True if the item can be inserted</returns>
		public bool CanAcceptItem(ItemDrop.ItemData item)
		{
			if (item == null) return false;

			// No filter means accept everything
			if (ItemFilter.Count == 0) return true;

			// Get item name for filtering
			string itemName = item.m_dropPrefab?.name ?? item.m_shared.m_name;
			bool inFilter = ItemFilter.Any(f => f.Equals(itemName, StringComparison.OrdinalIgnoreCase));

			// Apply whitelist/blacklist logic
			return IsWhitelist ? inFilter : !inFilter;
		}

		/// <summary>
		/// Get list of acceptable item types based on filter
		/// </summary>
		/// <returns>List of acceptable item names</returns>
		public List<string> GetAcceptableItems()
		{
			if (ItemFilter.Count == 0)
			{
				// No filter means accept all
				return new List<string> { "All Items" };
			}

			if (IsWhitelist)
			{
				return ItemFilter.ToList();
			}
			else
			{
				// For blacklist, we can't easily list all acceptable items
				return new List<string> { $"All except {ItemFilter.Count} filtered items" };
			}
		}

		#endregion

		#region Item Insertion

		// TODO: Add fixed to be able to work with different types (Smeltery, ...)
		public int CalculateAcceptCapacity(IContainerInterface container, ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (container == null || sourceItem == null || (desiredAmount <= 0 && sourceItem.m_stack <= 0))
				return 0;

			return container.CalculateAcceptCapacity(sourceItem, desiredAmount);
		}



		// TODO: Add fixed to be able to work with different types (Smeltery, ...)
		/// <summary>
		/// Insert an item into the container
		/// </summary>
		/// <param name="item">The item to insert</param>
		/// <returns>True if insertion was successful</returns>
		public bool InsertItem(ItemDrop.ItemData item, Inventory destInventory)
		{
			bool success = destInventory.AddItem(item);

			if (success)
			{
				lastInsertionTime = Time.time;

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


		#endregion

		#region Configuration

		/// <summary>
		/// Set the channel ID for this insert node
		/// </summary>
		/// <param name="channelId">The channel ID to set</param>
		public void SetChannel(string channelId)
		{
			ChannelId = string.IsNullOrEmpty(channelId) ? "" : channelId;

			// Save to ZDO for persistence
			if (zNetView != null && zNetView.IsValid())
			{
				ZDO zdo = zNetView.GetZDO();
				if (zdo != null)
				{
					zdo.Set("ItemConduit_Channel", ChannelId);
				}
			}

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

			// Save to ZDO for persistence
			if (zNetView != null && zNetView.IsValid())
			{
				ZDO zdo = zNetView.GetZDO();
				if (zdo != null)
				{
					zdo.Set("ItemConduit_Priority", Priority);
				}
			}

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

		public void SetFilter(HashSet<string> filter, bool isWhitelist)
		{
			ItemFilter =filter != null ? new HashSet<string>(filter): new HashSet<string>(); 
			IsWhitelist = isWhitelist;

			// Save to ZDO for persistence
			if (zNetView != null && zNetView.IsValid())
			{
				ZDO zdo = zNetView.GetZDO();
				if (zdo != null)
				{
					string filterStr = string.Join(",", filter);
					zdo.Set("ItemConduit_Filter", filterStr);
					zdo.Set("ItemConduit_IsWhitelist", isWhitelist);
				}
			}


			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				string filterStr = string.Join(",", filter);
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdateFilter", filterStr, isWhitelist);
			}

			if (DebugConfig.showDebug.Value)
			{
				string mode = isWhitelist ? "whitelist" : "blacklist";
				Logger.LogInfo($"[ItemConduit] Insert node {name} filter set to {mode} with {filter.Count} items");
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

		/// <summary>
		/// RPC handler for filter updates
		/// </summary>
		private void RPC_UpdateFilter(long sender, string filterStr, bool isWhitelist)
		{
			if (string.IsNullOrEmpty(filterStr))
			{
				ItemFilter = new HashSet<string>();
			}
			else
			{
				// Parse filter string back into HashSet
				ItemFilter = new HashSet<string>(
					filterStr.Split(',')
						.Where(s => !string.IsNullOrEmpty(s))
						.Select(s => s.Trim())
				);
			}
				
			IsWhitelist = isWhitelist;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] {NodeType} node {name} received filter update via RPC: {ItemFilter.Count} items, mode: {(IsWhitelist ? "whitelist" : "blacklist")}");
			}
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
				((InsertNodeGUI)gui).Initialize(this);
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

			// Add filter info
			string filterInfo = "";
			if (ItemFilter.Count > 0)
			{
				string filterMode = IsWhitelist ? "Whitelist" : "Blacklist";
				filterInfo = $"\n[Filter: {filterMode} ({ItemFilter.Count} items)]";
			}

			// Add container status
			string containerStatus;
			IContainerInterface container = GetTargetContainer();
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

			return $"{baseText}\n{channelInfo}\n{priorityInfo}\n{filterInfo}\n{containerStatus}{interactionHint}";
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
		#endregion
	}
}