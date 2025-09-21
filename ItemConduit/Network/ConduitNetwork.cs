using ItemConduit.Core;
using ItemConduit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ItemConduit.Network
{
	/// <summary>
	/// Enhanced ConduitNetwork with priority-based sorting and channel management
	/// </summary>
	public class ConduitNetwork
	{
		#region Properties

		/// <summary>Unique identifier for this network</summary>
		public string NetworkId { get; set; }

		/// <summary>All nodes in this network</summary>
		public HashSet<BaseNode> Nodes { get; private set; } = new HashSet<BaseNode>();

		/// <summary>Extract nodes in this network</summary>
		public List<ExtractNode> ExtractNodes { get; private set; } = new List<ExtractNode>();

		/// <summary>Insert nodes in this network - kept sorted by priority</summary>
		public List<InsertNode> InsertNodes { get; private set; } = new List<InsertNode>();

		/// <summary>Conduit nodes in this network</summary>
		public List<ConduitNode> ConduitNodes { get; private set; } = new List<ConduitNode>();

		/// <summary>Whether this network is currently active</summary>
		public bool IsActive { get; set; } = true;

		/// <summary>Cached insert nodes by channel for fast lookup</summary>
		private Dictionary<string, List<InsertNode>> insertNodesByChannel = new Dictionary<string, List<InsertNode>>();

		/// <summary>Flag to track if insert nodes need re-sorting</summary>
		private bool needsSorting = false;

		#endregion

		#region Node Management

		/// <summary>
		/// Add a node to this network
		/// </summary>
		/// <param name="node">The node to add</param>
		public void AddNode(BaseNode node)
		{
			if (node == null) return;

			if (Nodes.Add(node))
			{
				// Add to type-specific list
				switch (node)
				{
					case ExtractNode extractNode:
						ExtractNodes.Add(extractNode);
						break;
					case InsertNode insertNode:
						InsertNodes.Add(insertNode);
						needsSorting = true; // Mark for re-sorting
						InvalidateChannelCache(); // Clear channel cache
						break;
					case ConduitNode conduitNode:
						ConduitNodes.Add(conduitNode);
						break;
				}

				// Sort insert nodes if needed
				if (needsSorting)
				{
					SortInsertNodesByPriority();
				}
			}
		}

		/// <summary>
		/// Remove a node from this network
		/// </summary>
		/// <param name="node">The node to remove</param>
		public void RemoveNode(BaseNode node)
		{
			if (node == null) return;

			if (Nodes.Remove(node))
			{
				// Remove from type-specific list
				switch (node)
				{
					case ExtractNode extractNode:
						ExtractNodes.Remove(extractNode);
						break;
					case InsertNode insertNode:
						InsertNodes.Remove(insertNode);
						InvalidateChannelCache(); // Clear channel cache
						break;
					case ConduitNode conduitNode:
						ConduitNodes.Remove(conduitNode);
						break;
				}
			}
		}

		/// <summary>
		/// Clear all nodes from this network
		/// </summary>
		public void Clear()
		{
			Nodes.Clear();
			ExtractNodes.Clear();
			InsertNodes.Clear();
			ConduitNodes.Clear();
			InvalidateChannelCache();
		}

		#endregion

		#region Priority and Sorting Management

		/// <summary>
		/// Sort insert nodes by priority (higher priority first, then by distance)
		/// </summary>
		public void SortInsertNodesByPriority()
		{
			if (InsertNodes.Count <= 1)
			{
				needsSorting = false;
				return;
			}

			// Sort by priority (descending), then by name for consistency
			InsertNodes.Sort((a, b) =>
			{
				// First compare by priority (higher first)
				int priorityComparison = b.Priority.CompareTo(a.Priority);
				if (priorityComparison != 0) return priorityComparison;

				// Then by name for consistent ordering of same-priority nodes
				return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
			});

			needsSorting = false;
			InvalidateChannelCache(); // Invalidate cache since order changed

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				string priorities = string.Join(", ", InsertNodes.Select(n => $"{n.name}({n.Priority})"));
				Debug.Log($"[ItemConduit] Sorted insert nodes for network {NetworkId}: {priorities}");
			}
		}

		/// <summary>
		/// Mark insert nodes as needing re-sorting
		/// Called when a node's priority changes
		/// </summary>
		public void MarkForSorting()
		{
			needsSorting = true;
		}

		#endregion

		#region Channel Management

		/// <summary>
		/// Get insert nodes for a specific channel, sorted by priority
		/// Uses caching for performance
		/// </summary>
		/// <param name="channelId">The channel ID to filter by</param>
		/// <returns>List of insert nodes sorted by priority</returns>
		public List<InsertNode> GetSortedInsertNodesForChannel(string channelId)
		{
			// Ensure nodes are sorted
			if (needsSorting)
			{
				SortInsertNodesByPriority();
			}

			// Check cache first
			if (insertNodesByChannel.TryGetValue(channelId, out List<InsertNode> cachedNodes))
			{
				return cachedNodes;
			}

			// Build and cache the filtered list
			var filteredNodes = InsertNodes
				.Where(node => node != null && node.IsActive && node.ChannelId == channelId)
				.ToList();

			insertNodesByChannel[channelId] = filteredNodes;

			if (ItemConduitMod.ShowDebugInfo.Value && filteredNodes.Count > 0)
			{
				string nodeInfo = string.Join(", ", filteredNodes.Select(n => $"{n.name}(P:{n.Priority})"));
				Debug.Log($"[ItemConduit] Channel '{channelId}' insert nodes: {nodeInfo}");
			}

			return filteredNodes;
		}

		/// <summary>
		/// Clear the channel cache (called when nodes are added/removed or priorities change)
		/// </summary>
		private void InvalidateChannelCache()
		{
			insertNodesByChannel.Clear();
		}

		/// <summary>
		/// Get all active channels in this network
		/// </summary>
		/// <returns>Set of channel IDs</returns>
		public HashSet<string> GetActiveChannels()
		{
			HashSet<string> channels = new HashSet<string>();

			foreach (var extract in ExtractNodes)
			{
				if (extract != null && extract.IsActive)
				{
					channels.Add(extract.ChannelId);
				}
			}

			foreach (var insert in InsertNodes)
			{
				if (insert != null && insert.IsActive)
				{
					channels.Add(insert.ChannelId);
				}
			}

			return channels;
		}

		#endregion

		#region Network Analysis

		/// <summary>
		/// Check if this network is valid (has both extract and insert nodes)
		/// </summary>
		/// <returns>True if the network can transfer items</returns>
		public bool IsValid()
		{
			return ExtractNodes.Any(n => n != null && n.IsActive) &&
				   InsertNodes.Any(n => n != null && n.IsActive);
		}

		/// <summary>
		/// Get transfer capacity statistics for this network
		/// </summary>
		/// <returns>Network capacity info</returns>
		public NetworkCapacityInfo GetCapacityInfo()
		{
			var info = new NetworkCapacityInfo();

			// Count active nodes
			info.ActiveExtractNodes = ExtractNodes.Count(n => n != null && n.IsActive);
			info.ActiveInsertNodes = InsertNodes.Count(n => n != null && n.IsActive);
			info.ActiveConduitNodes = ConduitNodes.Count(n => n != null && n.IsActive);

			// Calculate potential throughput
			info.MaxPotentialThroughput = info.ActiveExtractNodes * ItemConduitMod.TransferRate.Value;

			// Count available slots
			info.TotalAvailableSlots = 0;
			foreach (var insertNode in InsertNodes)
			{
				if (insertNode != null && insertNode.IsActive)
				{
					info.TotalAvailableSlots += insertNode.GetAvailableSpace();
				}
			}

			// Get channel distribution
			var channels = GetActiveChannels();
			info.ActiveChannels = channels.Count;
			info.ChannelDistribution = new Dictionary<string, int>();

			foreach (string channel in channels)
			{
				int extractCount = ExtractNodes.Count(n => n != null && n.IsActive && n.ChannelId == channel);
				int insertCount = InsertNodes.Count(n => n != null && n.IsActive && n.ChannelId == channel);
				info.ChannelDistribution[channel] = Math.Min(extractCount, insertCount);
			}

			return info;
		}

		/// <summary>
		/// Get statistics about this network
		/// </summary>
		/// <returns>Network statistics string</returns>
		public string GetStatistics()
		{
			var capacity = GetCapacityInfo();
			var channels = GetActiveChannels();

			return $"Network {NetworkId?.Substring(0, 8) ?? "Unknown"}: " +
				   $"{Nodes.Count} nodes " +
				   $"({capacity.ActiveExtractNodes}E, {capacity.ActiveInsertNodes}I, {capacity.ActiveConduitNodes}C), " +
				   $"{channels.Count} channels, " +
				   $"{capacity.TotalAvailableSlots} slots, " +
				   $"{capacity.MaxPotentialThroughput:F1}/s max throughput";
		}

		#endregion

		#region Performance Optimization

		/// <summary>
		/// Update network state efficiently
		/// Called periodically to maintain performance
		/// </summary>
		public void OptimizeNetwork()
		{
			// Remove any null nodes
			Nodes.RemoveWhere(n => n == null);
			ExtractNodes.RemoveAll(n => n == null);
			InsertNodes.RemoveAll(n => n == null);
			ConduitNodes.RemoveAll(n => n == null);

			// Re-sort if needed
			if (needsSorting)
			{
				SortInsertNodesByPriority();
			}

			// Clean up channel cache if nodes are inactive
			if (insertNodesByChannel.Count > 0)
			{
				var channelsToRemove = new List<string>();
				foreach (var kvp in insertNodesByChannel)
				{
					// Remove inactive nodes from cached lists
					kvp.Value.RemoveAll(n => n == null || !n.IsActive);

					// Mark empty channels for removal
					if (kvp.Value.Count == 0)
					{
						channelsToRemove.Add(kvp.Key);
					}
				}

				foreach (string channel in channelsToRemove)
				{
					insertNodesByChannel.Remove(channel);
				}
			}
		}

		#endregion
	}

	/// <summary>
	/// Network capacity and performance information
	/// </summary>
	public class NetworkCapacityInfo
	{
		public int ActiveExtractNodes { get; set; }
		public int ActiveInsertNodes { get; set; }
		public int ActiveConduitNodes { get; set; }
		public float MaxPotentialThroughput { get; set; }
		public int TotalAvailableSlots { get; set; }
		public int ActiveChannels { get; set; }
		public Dictionary<string, int> ChannelDistribution { get; set; } = new Dictionary<string, int>();
	}
}