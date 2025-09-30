using UnityEngine;
using ItemConduit.Core;
using ItemConduit.Config;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Conduit node implementation
	/// Acts as a connection path between extract and insert nodes
	/// Does not interact with containers directly
	/// </summary>
	public class ConduitNode : BaseNode
	{
		// Visual effect components (optional)
		private LineRenderer connectionVisualizer;
		private Material activeMaterial;
		private Material inactiveMaterial;

		/// <summary>
		/// Initialize conduit node type
		/// </summary>
		protected override void Awake()
		{
			base.Awake();
			NodeType = NodeType.Conduit;

			// Initialize visual components if enabled
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				InitializeVisuals();
			}
		}

		/// <summary>
		/// Initialize visual effects for the conduit
		/// </summary>
		private void InitializeVisuals()
		{
			// Add line renderer for connection visualization
			connectionVisualizer = gameObject.AddComponent<LineRenderer>();
			if (connectionVisualizer != null)
			{
				connectionVisualizer.startWidth = 0.05f;
				connectionVisualizer.endWidth = 0.05f;
				connectionVisualizer.enabled = false; // Start disabled

				// Create materials for active/inactive states
				// These would ideally be loaded from asset bundles
				// For now, using basic colors
				connectionVisualizer.material = new Material(Shader.Find("Sprites/Default"));
			}
		}

		/// <summary>
		/// Provide detailed hover information for conduits
		/// </summary>
		public override string GetHoverText()
		{
			string baseText = base.GetHoverText();

			// Add connection count
			int connectionCount = connectedNodes.Count;
			string connectionInfo = connectionCount > 0
				? $"[Connections: {connectionCount}]"
				: "[<color=yellow>No Connections</color>]";

			// Build connection details
			if (connectionCount > 0 && DebugConfig.showDebug.Value)
			{
				connectionInfo += "\nConnected to:";
				foreach (var node in connectedNodes)
				{
					if (node != null)
					{
						string nodeType = node.NodeType.ToString();
						connectionInfo += $"\n  - {nodeType} ({node.name})";
					}
				}
			}

			// Add debug info if enabled
			if (DebugConfig.showDebug.Value)
			{
				connectionInfo += $"\n[Length: {NodeLength}m]";
				connectionInfo += $"\n[Position: {transform.position}]";
			}

			return $"{baseText}\n{connectionInfo}";
		}

		/// <summary>
		/// Update visual state based on active status
		/// </summary>
		protected override void UpdateVisualState(bool active)
		{
			base.UpdateVisualState(active);

			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Update material or particle effects
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				// Apply emissive effect when active
				if (active)
				{
					renderer.material.EnableKeyword("_EMISSION");
					renderer.material.SetColor("_EmissionColor", Color.gray * 0.5f);
				}
				else
				{
					renderer.material.DisableKeyword("_EMISSION");
				}
			}

			// Update line renderer for connections
			UpdateConnectionVisuals();
		}

		/// <summary>
		/// Update visual connections to other nodes
		/// </summary>
		private void UpdateConnectionVisuals()
		{
			if (connectionVisualizer == null || !ItemConduitMod.EnableVisualEffects.Value) return;

			// For now, disable line renderer as it would need more complex handling
			// In a full implementation, this would draw lines to connected nodes
			connectionVisualizer.enabled = false;
		}

		/// <summary>
		/// Override to add conduit-specific connection logic
		/// Conduits can connect to any node type
		/// </summary>
		protected override bool CanConnectTo(BaseNode other)
		{
			// Conduits connect to everything
			return true;
		}

		/// <summary>
		/// Called when connections change
		/// </summary>
		public override void FindConnections()
		{
			base.FindConnections();

			// Update visuals when connections change
			if (ItemConduitMod.EnableVisualEffects.Value)
			{
				UpdateConnectionVisuals();
			}
		}
	}
}