using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Core;
using ItemConduit.Config;
using ItemConduit.GUI;
using Logger = Jotunn.Logger;
using System;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Extract node implementation - pulls items from containers
	/// Container detection is handled by base class
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

		/// <summary>GUI component for configuration</summary>
		private ExtractNodeGUI gui;

		/// <summary>Time since last extraction attempt</summary>
		private float lastExtractionTime;

		#endregion

		#region Overrides

		/// <summary>
		/// Extract nodes can connect to containers
		/// </summary>
		protected override bool CanConnectToContainers => true;

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

			string savedChannel = zdo.GetString("ItemConduit_Channel", "None");
			ChannelId = savedChannel;

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
				Logger.LogInfo($"[ItemConduit] Loaded ExtractNode config - Channel: {ChannelId}, Filter items: {ItemFilter.Count}");
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
					Logger.LogWarning($"[ItemConduit] Extract node {name} connected to container: {targetContainer.m_name}");

					Inventory inv = targetContainer.GetInventory();
					if (inv != null)
					{
						Logger.LogInfo($"[ItemConduit]   Inventory: {inv.GetWidth()}x{inv.GetHeight()} slots");
						Logger.LogInfo($"[ItemConduit]   Items: {inv.GetAllItems().Count}");

						// Log extractable items
						var extractable = GetExtractableItems();
						Logger.LogInfo($"[ItemConduit]   Extractable items: {extractable.Count}");
					}
				}
				else
				{
					Logger.LogWarning($"[ItemConduit] Extract node {name} found NO container");
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
			bool inFilter = ItemFilter.Any(f => f.Equals(itemName, StringComparison.OrdinalIgnoreCase));

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
				.OrderBy(item => item.m_gridPos.y * inventory.GetWidth() + item.m_gridPos.x)
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
				Logger.LogInfo($"[ItemConduit] Extract node {name} channel set to: {ChannelId}");
			}

			// Request network rebuild to update routing
			if (IsValidPlacedNode())
			{
				Network.RebuildManager.Instance.RequestRebuildForNode(this);
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
				((ExtractNodeGUI)gui).Initialize(this);
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

			if (!VisualConfig.transferVisualEffect.Value) return;

			// Add green pulse effect when extracting
			if (active && Time.time - lastExtractionTime < 1f)
			{
				MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
				if (renderer != null)
				{
					// Pulse effect
					float pulse = Mathf.PingPong(Time.time * 2f, 1f);
					renderer.material.SetColor("_EmissionColor", Color.green * (0.3f + pulse * 0.3f));
				}
			}
		}

		/// <summary>
		/// Called when an item is successfully extracted
		/// </summary>
		public void OnItemExtracted()
		{
			lastExtractionTime = Time.time;

			// Trigger visual feedback
			if (VisualConfig.transferVisualEffect.Value)
			{
				//StartCoroutine(ExtractFlashEffect());
			}
		}

		/// <summary>
		/// Visual flash when extracting
		/// </summary>
		private IEnumerator ExtractFlashEffect()
		{
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				Color originalEmission = renderer.material.GetColor("_EmissionColor");
				renderer.material.SetColor("_EmissionColor", Color.green);
				yield return new WaitForSeconds(0.2f);
				renderer.material.SetColor("_EmissionColor", originalEmission);
			}
		}

		#endregion

		#region Debug



		#endregion
	}
}