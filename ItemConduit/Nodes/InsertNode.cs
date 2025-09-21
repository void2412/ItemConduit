using UnityEngine;
using ItemConduit.Core;
using ItemConduit.GUI;

namespace ItemConduit.Nodes
{
	/// <summary>
	/// Insert node implementation
	/// Receives items from the network and inserts them into attached containers
	/// </summary>
	public class InsertNode : BaseNode
	{
		#region Configuration Properties

		/// <summary>Channel ID for receiving items from specific extract nodes</summary>
		public string ChannelId { get; private set; } = "None";

		/// <summary>Priority level for filling order (higher = filled first)</summary>
		public int Priority { get; private set; } = 0;

		#endregion

		#region Private Fields

		/// <summary>Reference to the container this node inserts into</summary>
		private Container targetContainer;

		/// <summary>GUI component for configuration</summary>
		private InsertNodeGUI gui;

		/// <summary>Search radius for finding containers - increased for better detection</summary>
		private const float CONTAINER_SEARCH_RADIUS = 5f;

		/// <summary>Time since last insertion</summary>
		private float lastInsertionTime;

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
				Debug.Log($"[ItemConduit] {name} searching for containers...");
				Debug.Log($"[ItemConduit] Node position: {transform.position}");
				Debug.Log($"[ItemConduit] Search radius: {CONTAINER_SEARCH_RADIUS}");
			}

			// Find all colliders within search radius
			Collider[] colliders = Physics.OverlapSphere(transform.position, CONTAINER_SEARCH_RADIUS);

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Found {colliders.Length} colliders in range");
			}

			float nearestDistance = float.MaxValue;
			Container nearestContainer = null;

			foreach (Collider col in colliders)
			{
				if (col == null || col.gameObject == null) continue;

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Checking collider: {col.name} on object: {col.gameObject.name}");
					Debug.Log($"[ItemConduit] Collider position: {col.transform.position}");
					Debug.Log($"[ItemConduit] Distance: {Vector3.Distance(transform.position, col.transform.position):F2}m");
					Debug.Log($"[ItemConduit] Container layer: {col.gameObject.layer}");
					Debug.Log($"[ItemConduit] Container layer name: {LayerMask.LayerToName(col.gameObject.layer)}");
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
						Debug.Log($"[ItemConduit] Found Container: {container.name}");
						Debug.Log($"[ItemConduit] Container m_name: {container.m_name}");
						Debug.Log($"[ItemConduit] Container position: {container.transform.position}");
						Debug.Log($"[ItemConduit] Distance to container: {distance:F2}m");

						// Check if container has inventory
						Inventory inv = container.GetInventory();
						if (inv != null)
						{
							Debug.Log($"[ItemConduit] Container has inventory: {inv.GetWidth()}x{inv.GetHeight()} slots");
							Debug.Log($"[ItemConduit] Container items: {inv.GetAllItems().Count}");
							Debug.Log($"[ItemConduit] Container empty slots: {inv.GetEmptySlots()}");
						}
						else
						{
							Debug.Log($"[ItemConduit] Container has NO inventory!");
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
						Debug.Log($"[ItemConduit] No Container component found on {col.gameObject.name}");

						// List all components on this object for debugging
						Component[] components = col.GetComponents<Component>();
						Debug.Log($"[ItemConduit] Components on {col.gameObject.name}:");
						foreach (var comp in components)
						{
							if (comp != null)
							{
								Debug.Log($"[ItemConduit]   - {comp.GetType().Name}");
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
					Debug.Log($"[ItemConduit] *** {name} FOUND container: {targetContainer.name} at distance {nearestDistance:F2}m ***");
				}
				else
				{
					Debug.Log($"[ItemConduit] *** {name} found NO containers within {CONTAINER_SEARCH_RADIUS}m ***");

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
			Debug.Log($"[ItemConduit] === All containers in scene ({allContainers.Length}) ===");

			foreach (var container in allContainers)
			{
				if (container != null)
				{
					float distance = Vector3.Distance(transform.position, container.transform.position);
					Debug.Log($"[ItemConduit] Container: {container.name} ({container.m_name}) at {distance:F2}m");
					Debug.Log($"[ItemConduit]   Position: {container.transform.position}");
					Debug.Log($"[ItemConduit]   Has Inventory: {container.GetInventory() != null}");
					Debug.Log($"[ItemConduit]   Layer: {container.gameObject.layer} ({LayerMask.LayerToName(container.gameObject.layer)})");
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
				Debug.Log($"[ItemConduit] {name} trying raycast detection...");
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
						Debug.Log($"[ItemConduit] Raycast hit: {hit.collider.name} at {hit.distance:F2}m in direction {direction}");
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
							Debug.Log($"[ItemConduit] *** Raycast found container: {container.name} ***");
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
				Debug.Log($"[ItemConduit] {name} trying expanded search...");

				// Temporarily increase search radius
				const float expandedRadius = 10f; // Increase to 10 meters

				Collider[] expandedColliders = Physics.OverlapSphere(transform.position, expandedRadius);
				Debug.Log($"[ItemConduit] Expanded search found {expandedColliders.Length} colliders");

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
						Debug.Log($"[ItemConduit] Found container '{container.name}' at {distance:F2}m (outside normal range)");
					}
				}
			}

			return targetContainer;
		}

		#endregion

		#region Item Insertion

		/// <summary>
		/// Check if an item can be inserted into the container
		/// </summary>
		/// <param name="item">The item to check</param>
		/// <returns>True if the item can be inserted</returns>
		public bool CanInsertItem(ItemDrop.ItemData item)
		{
			Container container = GetTargetContainer();
			if (container == null || item == null) return false;

			Inventory inventory = container.GetInventory();
			if (inventory == null) return false;

			// Check if inventory has space
			return inventory.CanAddItem(item);
		}

		/// <summary>
		/// Insert an item into the container
		/// </summary>
		/// <param name="item">The item to insert</param>
		/// <returns>True if insertion was successful</returns>
		public bool InsertItem(ItemDrop.ItemData item)
		{
			if (!CanInsertItem(item)) return false;

			Container container = GetTargetContainer();
			Inventory inventory = container.GetInventory();
			bool success = inventory.AddItem(item);

			if (success)
			{
				lastInsertionTime = Time.time;

				// Visual feedback
				if (ItemConduitMod.EnableVisualEffects.Value)
				{
					// TODO: Add insertion effect (particles, sound, etc.)
				}

				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Insert node {name} inserted {item.m_shared.m_name} x{item.m_stack}");
				}
			}

			return success;
		}

		/// <summary>
		/// Get available space in the container
		/// </summary>
		/// <returns>Number of free slots</returns>
		public int GetAvailableSpace()
		{
			Container container = GetTargetContainer();
			if (container == null) return 0;

			Inventory inventory = container.GetInventory();
			if (inventory == null) return 0;

			return inventory.GetEmptySlots();
		}

		#endregion

		#region Configuration

		/// <summary>
		/// Set the channel ID for this insert node
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
				Debug.Log($"[ItemConduit] Insert node {name} channel set to: {ChannelId}");
			}
		}

		/// <summary>
		/// Set the priority for this insert node
		/// </summary>
		/// <param name="priority">The priority value (higher = filled first)</param>
		public void SetPriority(int priority)
		{
			Priority = priority;

			// Sync to all clients
			if (zNetView != null && zNetView.IsValid() && ZNet.instance.IsServer())
			{
				zNetView.InvokeRPC(ZNetView.Everybody, "RPC_UpdatePriority", Priority);
			}

			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Debug.Log($"[ItemConduit] Insert node {name} priority set to: {Priority}");
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
				GameObject guiObj = new GameObject("InsertNodeGUI");
				gui = guiObj.AddComponent<InsertNodeGUI>();
				gui.Initialize(this);
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
			string baseText = base.GetHoverText();

			// Add channel info
			string channelInfo = $"[Channel: <color=cyan>{ChannelId}</color>]";

			// Add priority info
			string priorityColor = Priority > 0 ? "yellow" : "white";
			string priorityInfo = $"[Priority: <color={priorityColor}>{Priority}</color>]";

			// Add container status
			string containerStatus;
			Container container = GetTargetContainer();
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

			return $"{baseText}\n{channelInfo}\n{priorityInfo}\n{containerStatus}{interactionHint}";
		}

		#endregion

		#region Visual Updates

		/// <summary>
		/// Update visual state for insert node
		/// </summary>
		protected override void UpdateVisualState(bool active)
		{
			base.UpdateVisualState(active);

			if (!ItemConduitMod.EnableVisualEffects.Value) return;

			// Apply blue tint when active
			MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
			if (renderer != null)
			{
				if (active)
				{
					renderer.material.EnableKeyword("_EMISSION");
					renderer.material.SetColor("_EmissionColor", Color.blue * 0.3f);
				}
				else
				{
					renderer.material.DisableKeyword("_EMISSION");
				}
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
	}
}