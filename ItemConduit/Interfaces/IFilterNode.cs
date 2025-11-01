using System.Collections.Generic;

namespace ItemConduit.Interfaces
{
	/// <summary>
	/// Common contract for nodes that expose filterable channel configuration in the GUI.
	/// </summary>
	public interface IFilterNode
	{
		string ChannelId { get; }
		HashSet<string> ItemFilter { get; }
		bool IsWhitelist { get; }

		void SetChannel(string channelId);
		void SetFilter(HashSet<string> filter);
		void SetWhitelist(bool isWhitelist);
	}
}


