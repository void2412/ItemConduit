using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemConduit.Nodes;
using ItemConduit.Core;
using ItemConduit.Config;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// GUI for configuring extract nodes
	/// Allows setting channel ID and item filters
	/// </summary>
	public class ExtractNodeGUI : BaseNodeGUI
	{
		#region Fields

		/// <summary>The extract node being configured</summary>
		private ExtractNode node;

		/// <summary>Channel ID input field</summary>
		private string channelInput = "";

		/// <summary>Item filter text area input</summary>
		private string filterInput = "";

		/// <summary>Whether filter is whitelist or blacklist</summary>
		private bool isWhitelist = true;

		/// <summary>Scroll position for filter text area</summary>
		private Vector2 scrollPosition;

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize the GUI with an extract node
		/// </summary>
		/// <param name="extractNode">The node to configure</param>
		public void Initialize(ExtractNode extractNode)
		{
			node = extractNode;

			// Load current settings
			channelInput = node.ChannelId ?? "None";

			// Convert filter set to string (one item per line)
			if (node.ItemFilter != null && node.ItemFilter.Count > 0)
			{
				filterInput = string.Join("\n", node.ItemFilter);
			}

			isWhitelist = node.IsWhitelist;

			// Set window title
			windowTitle = "Extract Node Configuration";

			// Set window size
			windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 200, 500, 400);
		}

		#endregion

		#region GUI Drawing

		/// <summary>
		/// Draw the configuration GUI
		/// </summary>
		public override void DrawGUI()
		{
			if (!isVisible || node == null) return;

			// Draw window
			windowRect = UnityEngine.GUI.Window(
				GetInstanceID(),
				windowRect,
				DrawWindow,
				windowTitle,
				windowStyle ?? UnityEngine.GUI.skin.window
			);
		}

		/// <summary>
		/// Draw window contents
		/// </summary>
		private void DrawWindow(int windowID)
		{
			// Close button
			DrawCloseButton();

			GUILayout.BeginVertical();

			// Spacing
			GUILayout.Space(10);

			// Channel configuration section
			DrawChannelSection();

			GUILayout.Space(15);

			// Filter configuration section
			DrawFilterSection();

			GUILayout.Space(15);

			// Buttons section
			DrawButtons();

			GUILayout.Space(10);

			// Info section
			DrawInfoSection();

			GUILayout.EndVertical();

			// Make window draggable
			MakeWindowDraggable();
		}

		/// <summary>
		/// Draw channel configuration section
		/// </summary>
		private void DrawChannelSection()
		{
			GUILayout.Label("Channel ID:", labelStyle ?? UnityEngine.GUI.skin.label);
			GUILayout.Label("Items will only be sent to Insert nodes with matching channel",
				new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic });

			channelInput = GUILayout.TextField(channelInput ?? "", GUILayout.Height(25));

			// Quick channel buttons
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("None", GUILayout.Width(60)))
			{
				channelInput = "None";
			}
			if (GUILayout.Button("Ore", GUILayout.Width(60)))
			{
				channelInput = "Ore";
			}
			if (GUILayout.Button("Food", GUILayout.Width(60)))
			{
				channelInput = "Food";
			}
			if (GUILayout.Button("Materials", GUILayout.Width(80)))
			{
				channelInput = "Materials";
			}
			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Draw item filter section
		/// </summary>
		private void DrawFilterSection()
		{
			GUILayout.Label("Item Filter:", labelStyle ?? UnityEngine.GUI.skin.label);
			GUILayout.Label("Enter item names (one per line) to filter extraction",
				new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic });

			// Filter mode toggle
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(isWhitelist, "Whitelist", GUILayout.Width(100)))
			{
				isWhitelist = true;
			}
			if (GUILayout.Toggle(!isWhitelist, "Blacklist", GUILayout.Width(100)))
			{
				isWhitelist = false;
			}
			GUILayout.FlexibleSpace();
			GUILayout.Label(isWhitelist ? "Only extract listed items" : "Extract everything except listed items",
				new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10 });
			GUILayout.EndHorizontal();

			// Filter text area with scroll
			scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(120));
			filterInput = GUILayout.TextArea(filterInput ?? "", GUILayout.ExpandHeight(true));
			GUILayout.EndScrollView();

			// Common item buttons
			GUILayout.Label("Quick add common items:", new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10 });

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Ores", GUILayout.Width(60)))
			{
				AddToFilter("CopperOre\nTinOre\nIronOre\nSilverOre\nBlackMetalScrap");
			}
			if (GUILayout.Button("Metals", GUILayout.Width(60)))
			{
				AddToFilter("Copper\nTin\nBronze\nIron\nSilver\nBlackMetal");
			}
			if (GUILayout.Button("Wood", GUILayout.Width(60)))
			{
				AddToFilter("Wood\nFineWood\nCoreWood\nAncientBark");
			}
			if (GUILayout.Button("Stone", GUILayout.Width(60)))
			{
				AddToFilter("Stone\nFlint\nObsidian\nMarble");
			}
			if (GUILayout.Button("Clear", GUILayout.Width(60)))
			{
				filterInput = "";
			}
			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Add items to filter
		/// </summary>
		private void AddToFilter(string items)
		{
			if (string.IsNullOrEmpty(filterInput))
			{
				filterInput = items;
			}
			else
			{
				filterInput += "\n" + items;
			}
		}

		/// <summary>
		/// Draw action buttons
		/// </summary>
		private void DrawButtons()
		{
			GUILayout.BeginHorizontal();

			// Apply button
			if (GUILayout.Button("Apply", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				ApplySettings();
			}

			// Reset button
			if (GUILayout.Button("Reset", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				ResetSettings();
			}

			// Cancel button
			if (GUILayout.Button("Cancel", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				Hide();
			}

			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Draw information section
		/// </summary>
		private void DrawInfoSection()
		{
			// Network info
			string networkInfo = node.NetworkId != null
				? $"Network: {node.NetworkId.Substring(0, 8)}..."
				: "Network: Not Connected";
			GUILayout.Label(networkInfo, UnityEngine.GUI.skin.box);

			// Container info
			Container container = node.GetTargetContainer();
			if (container != null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null)
				{
					int totalItems = inv.GetAllItems().Count;
					int extractableItems = node.GetExtractableItems().Count;
					string containerInfo = $"Container: {container.m_name} ({extractableItems}/{totalItems} extractable items)";
					GUILayout.Label(containerInfo, UnityEngine.GUI.skin.box);
				}
				else
				{
					GUILayout.Label("Container: Connected (No Inventory)", UnityEngine.GUI.skin.box);
				}
			}
			else
			{
				GUILayout.Label("Container: Not Connected", UnityEngine.GUI.skin.box);
			}
		}

		#endregion

		#region Settings Management

		/// <summary>
		/// Apply the current settings to the node
		/// </summary>
		private void ApplySettings()
		{
			// Set channel
			string channel = string.IsNullOrWhiteSpace(channelInput) ? "None" : channelInput.Trim();
			node.SetChannel(channel);

			// Parse and set filter
			HashSet<string> filter = ParseFilter();
			node.SetFilter(filter, isWhitelist);

			// Log if debug mode
			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Applied settings to extract node: Channel={channel}, " +
						 $"Filter={filter.Count} items ({(isWhitelist ? "whitelist" : "blacklist")})");
			}

			// Close GUI
			Hide();
		}

		/// <summary>
		/// Reset settings to current node values
		/// </summary>
		private void ResetSettings()
		{
			channelInput = node.ChannelId ?? "None";

			if (node.ItemFilter != null && node.ItemFilter.Count > 0)
			{
				filterInput = string.Join("\n", node.ItemFilter);
			}
			else
			{
				filterInput = "";
			}

			isWhitelist = node.IsWhitelist;
		}

		/// <summary>
		/// Parse the filter input into a HashSet
		/// </summary>
		private HashSet<string> ParseFilter()
		{
			HashSet<string> filter = new HashSet<string>();

			if (!string.IsNullOrEmpty(filterInput))
			{
				string[] lines = filterInput.Split('\n');
				foreach (string line in lines)
				{
					string trimmed = line.Trim();
					if (!string.IsNullOrEmpty(trimmed))
					{
						filter.Add(trimmed);
					}
				}
			}

			return filter;
		}

		#endregion
	}
}