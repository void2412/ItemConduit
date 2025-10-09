using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Events
{
	/// <summary>
	/// Manages container placement/destruction events and notifies interested nodes
	/// </summary>
	public class ContainerEventManager : MonoBehaviour
	{
		#region Singleton
		private static ContainerEventManager _instance;
		public static ContainerEventManager Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_ContainerEventManager");
					_instance = go.AddComponent<ContainerEventManager>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}
		#endregion

		#region Events
		public delegate void ContainerEventHandler(IContainerInterface container, Vector3 position);

		/// <summary>Fired when a container is placed in the world</summary>
		public event ContainerEventHandler OnContainerPlaced;

		/// <summary>Fired when a container is about to be destroyed</summary>
		public event ContainerEventHandler OnContainerRemoved;
		#endregion

		#region Private Fields
		private Dictionary<IContainerInterface, Vector3> trackedContainers = new Dictionary<IContainerInterface, Vector3>();
		private HashSet<BaseNode> subscribedNodes = new HashSet<BaseNode>();
		#endregion

		#region Public Methods

		public void Initialize()
		{
			var containerEventManager = ContainerEventManager.Instance;
		}

		/// <summary>
		/// Notify that a container has been placed
		/// </summary>
		public void NotifyContainerPlaced(IContainerInterface container)
		{
			if (container == null) return;

			if (!trackedContainers.ContainsKey(container))
			{
				trackedContainers[container] = container.GetTransformPosition();

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] ContainerEvent: {container.GetName()} placed at {container.GetTransformPosition()}");
				}

				// Fire event with delay to let physics settle
				StartCoroutine(DelayedContainerPlacedEvent(container));
			}
		}

		/// <summary>
		/// Notify that a container is being removed
		/// </summary>
		public void NotifyContainerRemoved(IContainerInterface container)
		{
			if (container == null) return;

			if (trackedContainers.ContainsKey(container))
			{
				Vector3 position = container.GetTransformPosition();
				trackedContainers.Remove(container);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] ContainerEvent: {container.GetName()} removed from {position}");
				}

				// Fire event immediately before container is destroyed
				OnContainerRemoved?.Invoke(container, position);

				// Notify nearby nodes directly for immediate response
				NotifyNearbyNodesDirectly(position, false, container);
			}
		}

		/// <summary>
		/// Register a node to receive container events
		/// </summary>
		public void RegisterNode(BaseNode node)
		{
			if (node != null && !subscribedNodes.Contains(node))
			{
				subscribedNodes.Add(node);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {node.name} registered for container events");
				}
			}
		}

		/// <summary>
		/// Unregister a node from container events
		/// </summary>
		public void UnregisterNode(BaseNode node)
		{
			if (node != null)
			{
				subscribedNodes.Remove(node);
			}
		}

		/// <summary>
		/// Get all containers near a position
		/// </summary>
		public List<IContainerInterface> GetContainersNearPosition(Vector3 position, float range)
		{
			List<IContainerInterface> nearbyContainers = new List<IContainerInterface>();

			foreach (var kvp in trackedContainers)
			{
				if (Vector3.Distance(kvp.Value, position) <= range)
				{
					nearbyContainers.Add(kvp.Key);
				}
			}

			return nearbyContainers;
		}

		#endregion

		#region Private Methods

		private IEnumerator DelayedContainerPlacedEvent(IContainerInterface container)
		{
			// Wait for physics to settle
			float delay = ContainerEventConfig.containerEventDelay?.Value ?? 0.5f;
			yield return new WaitForSeconds(delay);

			// Verify container still exists
			if (container != null && container.GetInventory() != null)
			{
				OnContainerPlaced?.Invoke(container, container.GetTransformPosition());

				// Also notify nearby nodes directly
				NotifyNearbyNodesDirectly(container.GetTransformPosition(), true, container);
			}
		}

		private void NotifyNearbyNodesDirectly(Vector3 position, bool isPlacement, IContainerInterface container)
		{
			
			float range = ContainerEventConfig.containerDetectionRange.Value;

			Logger.LogInfo($"[ItemConduit] Notifying nodes within {range}m of {position}");
			Logger.LogInfo($"[ItemConduit] Total subscribed nodes: {subscribedNodes.Count}");

			int nodesInRange = 0;

			// Find all nodes within range
			foreach (var node in subscribedNodes)
			{
				if (node == null || !node.IsValidPlacedNode())
				{
					Logger.LogWarning("[ItemConduit] Found null node in subscribed list");
					continue; 
				}

				if (!node.IsValidPlacedNode())
				{
					Logger.LogWarning($"[ItemConduit] Node {node.name} is not valid");
					continue;
				}

				float distance = Vector3.Distance(node.transform.position, position);

				if(DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Node {node.name} distance: {distance:F2}m (range: {range}m)");
				}
				
				if (distance <= range)
				{
					nodesInRange++;
					if(DebugConfig.showDebug.Value)
					{
						Logger.LogInfo($"[ItemConduit] Notifying {node.name} about container {(isPlacement ? "placement" : "removal")}");
					}
					

					// Notify the node about the container event
					if (isPlacement)
					{
						node.OnNearbyContainerPlaced(container, distance);
					}
					else
					{
						node.OnNearbyContainerRemoved(container, distance);
					}
				}
			}
		}

		#endregion

		#region Initialization

		private void Awake()
		{
			if (_instance == null)
			{
				_instance = this;
				DontDestroyOnLoad(gameObject);
			}
			else if (_instance != this)
			{
				Destroy(gameObject);
			}
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		#endregion
	}
}