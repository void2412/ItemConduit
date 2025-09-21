using System.Collections.Generic;
using ItemConduit.Nodes;

namespace ItemConduit.Network
{
	/// <summary>
	/// Represents a connected network of conduit nodes
	/// Tracks all nodes and provides categorized access
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

		/// <summary>Insert nodes in this network</summary>
		public List<InsertNode> InsertNodes { get; private set; } = new List<InsertNode>();

		/// <summary>Conduit nodes in this network</summary>
		public List<ConduitNode> ConduitNodes { get; private set; } = new List<ConduitNode>();

		/// <summary>Whether this network is currently active</summary>
		public bool IsActive { get; set; } = true;

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
						break;
					case ConduitNode conduitNode:
						ConduitNodes.Add(conduitNode);
						break;
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
		}

		#endregion

		#region Network Analysis

		/// <summary>
		/// Check if this network is valid (has both extract and insert nodes)
		/// </summary>
		/// <returns>True if the network can transfer items</returns>
		public bool IsValid()
		{
			return ExtractNodes.Count > 0 && InsertNodes.Count > 0;
		}

		/// <summary>
		/// Get channels active in this network
		/// </summary>
		/// <returns>Set of channel IDs</returns>
		public HashSet<string> GetActiveChannels()
		{
			HashSet<string> channels = new HashSet<string>();

			foreach (var extract in ExtractNodes)
			{
				channels.Add(extract.ChannelId);
			}

			foreach (var insert in InsertNodes)
			{
				channels.Add(insert.ChannelId);
			}

			return channels;
		}

		/// <summary>
		/// Get statistics about this network
		/// </summary>
		/// <returns>Network statistics string</returns>
		public string GetStatistics()
		{
			var channels = GetActiveChannels();
			return $"Network {NetworkId}: {Nodes.Count} nodes " +
				   $"({ExtractNodes.Count} extract, {InsertNodes.Count} insert, {ConduitNodes.Count} conduit), " +
				   $"{channels.Count} channels";
		}

		#endregion
	}
}