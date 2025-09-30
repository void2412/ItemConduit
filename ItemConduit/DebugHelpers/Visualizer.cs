using ItemConduit.Config;
using ItemConduit.Core;
using ItemConduit.Nodes;
using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Debug
{
	public class BoundsVisualizer : MonoBehaviour
	{
		private GameObject colliderWireframe;
		private List<LineRenderer> colliderLines = new List<LineRenderer>();

		public void Initialize(Color colliderColor)
		{
			// Create collider wireframe with 12 edges  
			colliderWireframe = CreateWireframeBox("ColliderWireframe", colliderColor, 0.025f, colliderLines);
			this.SetVisible(VisualConfig.nodeWireframe.Value);
		}

		private GameObject CreateWireframeBox(string name, Color color, float width, List<LineRenderer> linesList)
		{
			GameObject wireframeParent = new GameObject(name);
			wireframeParent.transform.SetParent(transform);

			// Create 12 line renderers for 12 edges of a box
			for (int i = 0; i < 12; i++)
			{
				GameObject edge = new GameObject($"Edge_{i}");
				edge.transform.SetParent(wireframeParent.transform);

				LineRenderer lr = edge.AddComponent<LineRenderer>();
				lr.material = new Material(Shader.Find("Sprites/Default"));
				lr.startColor = lr.endColor = color;
				lr.startWidth = lr.endWidth = width;
				lr.useWorldSpace = true;
				lr.positionCount = 2; // Each edge has 2 points

				linesList.Add(lr);
			}

			return wireframeParent;
		}

		public void UpdateCollider(Collider collider)
		{
			if (colliderLines.Count == 0 || collider == null) return;

			// Get the local bounds of the collider
			Bounds localBounds;
			Vector3[] worldCorners = new Vector3[8];

			if (collider is BoxCollider boxCollider)
			{
				// For box colliders, use center and size
				Vector3 center = boxCollider.center;
				Vector3 size = boxCollider.size;

				localBounds = new Bounds(center, size);
				Vector3[] localCorners = GetBoundsCorners(localBounds);

				// Transform to world space
				for (int i = 0; i < 8; i++)
				{
					worldCorners[i] = collider.transform.TransformPoint(localCorners[i]);
				}
			}
			else
			{
				// For other colliders
				localBounds = GetColliderLocalBounds(collider);
				Vector3[] localCorners = GetBoundsCorners(localBounds);

				for (int i = 0; i < 8; i++)
				{
					worldCorners[i] = collider.transform.TransformPoint(localCorners[i]);
				}
			}

			DrawWireframeBox(colliderLines, worldCorners);
		}

		private Bounds GetColliderLocalBounds(Collider collider)
		{
			if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
			{
				return meshCollider.sharedMesh.bounds;
			}
			else if (collider is BoxCollider boxCollider)
			{
				return new Bounds(boxCollider.center, boxCollider.size);
			}
			else if (collider is CapsuleCollider capsuleCollider)
			{
				float radius = capsuleCollider.radius;
				float height = capsuleCollider.height;
				return new Bounds(capsuleCollider.center, new Vector3(radius * 2, height, radius * 2));
			}
			else
			{
				// Fallback - convert world bounds to local
				Bounds worldBounds = collider.bounds;
				Vector3 localCenter = collider.transform.InverseTransformPoint(worldBounds.center);
				Vector3 localSize = collider.transform.InverseTransformVector(worldBounds.size);
				return new Bounds(localCenter, localSize);
			}
		}

		private Vector3[] GetBoundsCorners(Bounds bounds)
		{
			Vector3 min = bounds.min;
			Vector3 max = bounds.max;


			return new Vector3[]
			{
				new Vector3(min.x, min.y, min.z), // 0
                new Vector3(max.x, min.y, min.z), // 1
                new Vector3(max.x, min.y, max.z), // 2
                new Vector3(min.x, min.y, max.z), // 3
                new Vector3(min.x, max.y, min.z), // 4
                new Vector3(max.x, max.y, min.z), // 5
                new Vector3(max.x, max.y, max.z), // 6
                new Vector3(min.x, max.y, max.z)  // 7
            };
		}

		private void DrawWireframeBox(List<LineRenderer> lines, Vector3[] corners)
		{
			if (lines.Count < 12) return;

			// Bottom face
			lines[0].SetPositions(new Vector3[] { corners[0], corners[1] });
			lines[1].SetPositions(new Vector3[] { corners[1], corners[2] });
			lines[2].SetPositions(new Vector3[] { corners[2], corners[3] });
			lines[3].SetPositions(new Vector3[] { corners[3], corners[0] });

			// Top face
			lines[4].SetPositions(new Vector3[] { corners[4], corners[5] });
			lines[5].SetPositions(new Vector3[] { corners[5], corners[6] });
			lines[6].SetPositions(new Vector3[] { corners[6], corners[7] });
			lines[7].SetPositions(new Vector3[] { corners[7], corners[4] });

			// Vertical edges
			lines[8].SetPositions(new Vector3[] { corners[0], corners[4] });
			lines[9].SetPositions(new Vector3[] { corners[1], corners[5] });
			lines[10].SetPositions(new Vector3[] { corners[2], corners[6] });
			lines[11].SetPositions(new Vector3[] { corners[3], corners[7] });
		}

		public void SetVisible(bool visible)
		{
			if (colliderWireframe != null)
				colliderWireframe.SetActive(visible);
		}

		private void OnDestroy()
		{
			if (colliderWireframe != null)
				Destroy(colliderWireframe);
		}
	}
	public class SnapConnectionVisualizer : MonoBehaviour
	{
		private List<GameObject> snapMarkers = new List<GameObject>();
		private List<LineRenderer> connectionLines = new List<LineRenderer>();
		private BaseNode node;

		// Same threshold as in UnifiedDetectionCoroutine
		private const float SNAP_CONNECTION_THRESHOLD = 0.2f;
		private const float SNAP_DETECTION_RADIUS = 0.15f;

		public void Initialize(BaseNode baseNode)
		{
			node = baseNode;
			CreateSnapMarkers();
			this.SetVisible(VisualConfig.snappointSphere.Value);
		}

		private void CreateSnapMarkers()
		{
			// Find all snappoints on this node
			foreach (Transform child in transform)
			{
				if (child.tag == "snappoint")
				{
					// Create a visual marker for each snappoint
					GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					marker.name = $"SnapMarker_{child.name}";
					marker.transform.SetParent(transform);
					marker.transform.position = child.position;
					marker.transform.localScale = Vector3.one * 0.1f;

					// Remove collider
					Destroy(marker.GetComponent<Collider>());

					// Set material
					MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
					renderer.material = new Material(Shader.Find("Sprites/Default"));
					renderer.material.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange, semi-transparent

					snapMarkers.Add(marker);
				}
			}
		}

		public void UpdateConnections()
		{
			if (node == null || !node.IsValidPlacedNode()) return;

			// Clear old connection lines
			foreach (var line in connectionLines)
			{
				if (line != null) Destroy(line.gameObject);
			}
			connectionLines.Clear();

			// Get our snappoints
			var ourSnapPoints = GetSnapPoints();

			foreach (var snapPoint in ourSnapPoints)
			{
				// Use same detection logic as UnifiedDetectionCoroutine
				Collider[] snapOverlaps = Physics.OverlapSphere(
					snapPoint.position,
					SNAP_DETECTION_RADIUS,
					LayerMask.GetMask("piece", "piece_nonsolid")
				);

				foreach (var col in snapOverlaps)
				{
					if (col == null || col.transform == transform) continue;

					BaseNode otherNode = col.GetComponentInParent<BaseNode>();
					if (otherNode != null && otherNode != node && !otherNode.isGhostPiece)
					{
						var otherSnaps = GetSnapPoints(otherNode.transform);
						foreach (var otherSnap in otherSnaps)
						{
							float dist = Vector3.Distance(snapPoint.position, otherSnap.position);
							if (dist < SNAP_CONNECTION_THRESHOLD)
							{
								// Create connection line
								CreateConnectionLine(snapPoint.position, otherSnap.position, dist);
								break; // Only show one connection per snappoint
							}
						}
					}
				}
			}
		}

		private void CreateConnectionLine(Vector3 start, Vector3 end, float distance)
		{
			GameObject lineObj = new GameObject("SnapConnection");
			lineObj.transform.SetParent(transform);

			LineRenderer line = lineObj.AddComponent<LineRenderer>();
			line.material = new Material(Shader.Find("Sprites/Default"));

			// Color based on connection quality
			if (distance < 0.01f)
			{
				line.startColor = line.endColor = Color.green; // Perfect snap
			}
			else if (distance < 0.1f)
			{
				line.startColor = line.endColor = Color.yellow; // Good snap
			}
			else
			{
				line.startColor = line.endColor = new Color(1f, 0.5f, 0f); // Marginal snap
			}

			line.startWidth = line.endWidth = 0.03f;
			line.positionCount = 2;
			line.SetPosition(0, start);
			line.SetPosition(1, end);

			connectionLines.Add(line);
		}

		private Transform[] GetSnapPoints(Transform target = null)
		{
			if (target == null) target = transform;

			List<Transform> snapPoints = new List<Transform>();
			foreach (Transform child in target)
			{
				if (child.tag == "snappoint")
				{
					snapPoints.Add(child);
				}
			}
			snapPoints.Sort((a, b) => a.name.CompareTo(b.name));
			return snapPoints.ToArray();
		}

		public void SetVisible(bool visible)
		{
			foreach (var marker in snapMarkers)
			{
				if (marker != null) marker.SetActive(visible);
			}

			foreach (var line in connectionLines)
			{
				if (line != null) line.gameObject.SetActive(visible);
			}
		}

		private void OnDestroy()
		{
			foreach (var marker in snapMarkers)
			{
				if (marker != null) Destroy(marker);
			}

			foreach (var line in connectionLines)
			{
				if (line != null) Destroy(line.gameObject);
			}
		}
	}

	// <summary>
	/// Manages wireframe visualization for all containers
	/// Independent of node system, always visible
	/// </summary>
	public class ContainerWireframeManager : MonoBehaviour
	{
		private static ContainerWireframeManager _instance;

		public static ContainerWireframeManager Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_ContainerWireframes");
					_instance = go.AddComponent<ContainerWireframeManager>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		private Dictionary<Container, ContainerWireframe> wireframes = new Dictionary<Container, ContainerWireframe>();
		private bool isEnabled = true;

		public void RegisterContainer(Container container)
		{
			if (container == null || wireframes.ContainsKey(container)) return;

			// Create wireframe component on the container
			var wireframe = container.gameObject.AddComponent<ContainerWireframe>();
			wireframe.Initialize();
			wireframe.SetVisible(VisualConfig.containerWireframe.Value);
			wireframes[container] = wireframe;

			

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[ItemConduit] Added wireframe to container: {container.m_name}");
			}
		}

		public void UnregisterContainer(Container container)
		{
			if (container == null || !wireframes.ContainsKey(container)) return;

			if (wireframes[container] != null)
			{
				Destroy(wireframes[container]);
			}
			wireframes.Remove(container);
		}

		public void SetWireframesVisible(bool visible)
		{
			isEnabled = visible;
			foreach (var wireframe in wireframes.Values)
			{
				if (wireframe != null)
				{
					wireframe.SetVisible(visible);
				}
			}
		}

		private void OnDestroy()
		{
			foreach (var wireframe in wireframes.Values)
			{
				if (wireframe != null)
				{
					Destroy(wireframe);
				}
			}
			wireframes.Clear();
		}

		public void InitializeExistingContainers()
		{
			// Find all existing containers in the world
			Container[] existingContainers = UnityEngine.Object.FindObjectsOfType<Container>();
			foreach (var container in existingContainers)
			{
				RegisterContainer(container);
			}

			Logger.LogInfo($"[ItemConduit] Initialized wireframes for {existingContainers.Length} existing containers");
		}
	}

	/// <summary>
	/// Component that renders wireframe for a single container
	/// </summary>
	public class ContainerWireframe : MonoBehaviour
	{
		private List<LineRenderer> lines = new List<LineRenderer>();
		private GameObject wireframeRoot;
		private Collider targetCollider;

		// High contrast magenta
		private static readonly Color WIREFRAME_COLOR = new Color(1f, 0f, 1f, 1f);
		private static readonly Color WIREFRAME_EMISSION = new Color(1f, 0f, 1f, 0.5f);
		private const float LINE_WIDTH = 0.04f;

		public void Initialize()
		{
			DebugColliders();
			CreateWireframe();
			UpdateWireframe();
			DebugTransformHierarchy();
		}

		private void CreateWireframe()
		{
			wireframeRoot = new GameObject("ContainerWireframe");
			wireframeRoot.transform.SetParent(transform);
			wireframeRoot.transform.localPosition = Vector3.zero;
			wireframeRoot.transform.localRotation = Quaternion.identity;

			// Get ONLY the collider on the container GameObject itself, not children
			targetCollider = GetComponent<Collider>();

			if (targetCollider == null)
			{
				// Try to find a non-trigger collider in children
				Collider[] colliders = GetComponentsInChildren<Collider>();
				foreach (var col in colliders)
				{
					if (!col.isTrigger)
					{
						targetCollider = col;
						Logger.LogInfo($"[ItemConduit] Using collider from child: {col.name}");
						break;
					}
				}
			}

			if (targetCollider == null)
			{
				Logger.LogWarning($"[ItemConduit] No suitable collider found on container {name}");
				return;
			}

			Logger.LogInfo($"[ItemConduit] Using collider: {targetCollider.name}, Type: {targetCollider.GetType().Name}");

			// Get a reference material from the game
			Material baseMaterial = GetBaseMaterial();

			// Create 12 edges for box wireframe
			for (int i = 0; i < 12; i++)
			{
				GameObject lineObj = new GameObject($"Edge_{i}");
				lineObj.transform.SetParent(wireframeRoot.transform);

				LineRenderer lr = lineObj.AddComponent<LineRenderer>();

				Material mat = new Material(baseMaterial);
				mat.color = WIREFRAME_COLOR;

				if (mat.HasProperty("_EmissionColor"))
				{
					mat.EnableKeyword("_EMISSION");
					mat.SetColor("_EmissionColor", WIREFRAME_EMISSION);
				}

				lr.material = mat;
				lr.startColor = WIREFRAME_COLOR;
				lr.endColor = WIREFRAME_COLOR;
				lr.startWidth = LINE_WIDTH;
				lr.endWidth = LINE_WIDTH;
				lr.useWorldSpace = true;
				lr.positionCount = 2;
				lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				lr.receiveShadows = false;
				lr.sortingOrder = 100;

				lines.Add(lr);
			}

			// Add corner spheres for extra visibility
			AddCornerMarkers();
		}

		private void DebugColliders()
		{
			Collider[] allColliders = GetComponentsInChildren<Collider>();
			Logger.LogInfo($"[ItemConduit] Container {name} has {allColliders.Length} colliders:");

			foreach (var col in allColliders)
			{
				string type = col.GetType().Name;
				bool isTrigger = col.isTrigger;
				Bounds bounds = col.bounds;
				Logger.LogInfo($"  - {col.name}: {type}, Trigger: {isTrigger}, Size: {bounds.size}");
			}
		}

		private Material GetBaseMaterial()
		{
			// Try different shader options that should exist in Valheim
			Shader shader = Shader.Find("Sprites/Default");

			if (shader == null)
				shader = Shader.Find("Standard");

			if (shader == null)
				shader = Shader.Find("Unlit/Transparent");

			if (shader == null)
				shader = Shader.Find("Legacy Shaders/Diffuse");

			if (shader == null)
			{
				// Last resort: try to get shader from an existing renderer
				MeshRenderer existingRenderer = FindObjectOfType<MeshRenderer>();
				if (existingRenderer != null && existingRenderer.sharedMaterial != null)
				{
					return new Material(existingRenderer.sharedMaterial);
				}

				// Absolute fallback
				Logger.LogError("[ItemConduit] Could not find any shader for wireframe!");
				return new Material(Shader.Find("Hidden/InternalErrorShader"));
			}

			return new Material(shader);
		}

		private void AddCornerMarkers()
		{
			Material baseMaterial = GetBaseMaterial();

			for (int i = 0; i < 8; i++)
			{
				GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				marker.name = $"Corner_{i}";
				marker.transform.SetParent(wireframeRoot.transform);
				marker.transform.localScale = Vector3.one * 0.1f;

				Destroy(marker.GetComponent<Collider>());

				MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
				Material mat = new Material(baseMaterial);
				mat.color = WIREFRAME_COLOR;

				if (mat.HasProperty("_EmissionColor"))
				{
					mat.EnableKeyword("_EMISSION");
					mat.SetColor("_EmissionColor", WIREFRAME_COLOR * 1.5f);
				}

				renderer.material = mat;
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			}
		}

		private void UpdateWireframe()
		{
			if (targetCollider == null || wireframeRoot == null) return;

			// Debug: Compare different bounds
			if (targetCollider is MeshCollider meshCol)
			{
				Logger.LogWarning($"[DEBUG] MeshCollider bounds comparison for {name}:");
				Logger.LogWarning($"  - collider.bounds: {meshCol.bounds.size}");
				Logger.LogWarning($"  - sharedMesh.bounds: {meshCol.sharedMesh.bounds.size}");
				Logger.LogWarning($"  - Actual position: {meshCol.transform.position}");
			}

			Vector3[] corners = GetColliderCorners(targetCollider);

			//Collider collider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
			//if (collider == null) return;

			//Vector3[] corners = GetColliderCorners(collider);

			// Update line positions
			if (lines.Count >= 12)
			{
				// Bottom face
				lines[0].SetPositions(new Vector3[] { corners[0], corners[1] });
				lines[1].SetPositions(new Vector3[] { corners[1], corners[2] });
				lines[2].SetPositions(new Vector3[] { corners[2], corners[3] });
				lines[3].SetPositions(new Vector3[] { corners[3], corners[0] });

				// Top face
				lines[4].SetPositions(new Vector3[] { corners[4], corners[5] });
				lines[5].SetPositions(new Vector3[] { corners[5], corners[6] });
				lines[6].SetPositions(new Vector3[] { corners[6], corners[7] });
				lines[7].SetPositions(new Vector3[] { corners[7], corners[4] });

				// Vertical edges
				lines[8].SetPositions(new Vector3[] { corners[0], corners[4] });
				lines[9].SetPositions(new Vector3[] { corners[1], corners[5] });
				lines[10].SetPositions(new Vector3[] { corners[2], corners[6] });
				lines[11].SetPositions(new Vector3[] { corners[3], corners[7] });
			}

			// Update corner positions
			int cornerIndex = 0;
			foreach (Transform child in wireframeRoot.transform)
			{
				if (child.name.StartsWith("Corner_") && cornerIndex < 8)
				{
					child.position = corners[cornerIndex];
					cornerIndex++;
				}
			}
		}
		private void DebugTransformHierarchy()
		{
			Logger.LogWarning($"[DEBUG] Transform hierarchy for {name}:");
			Logger.LogWarning($"  Container position: {transform.position}");
			Logger.LogWarning($"  Container rotation: {transform.rotation.eulerAngles}");

			if (targetCollider != null)
			{
				Logger.LogWarning($"  Collider on: {targetCollider.gameObject.name}");
				Logger.LogWarning($"  Collider local position: {targetCollider.transform.localPosition}");
				Logger.LogWarning($"  Collider local rotation: {targetCollider.transform.localRotation.eulerAngles}");
				Logger.LogWarning($"  Collider world position: {targetCollider.transform.position}");

				if (targetCollider is MeshCollider mc && mc.sharedMesh != null)
				{
					Logger.LogWarning($"  Mesh bounds center: {mc.sharedMesh.bounds.center}");
					Logger.LogWarning($"  Mesh bounds size: {mc.sharedMesh.bounds.size}");
				}
			}
		}

		private Vector3[] GetColliderCorners(Collider collider)
		{
			Vector3[] worldCorners = new Vector3[8];

			if (collider is BoxCollider box)
			{
				// Get local corners of the box collider
				Vector3 center = box.center;
				Vector3 size = box.size;

				Vector3[] localCorners = new Vector3[]
				{
					center + new Vector3(-size.x, -size.y, -size.z) * 0.5f,
					center + new Vector3(size.x, -size.y, -size.z) * 0.5f,
					center + new Vector3(size.x, -size.y, size.z) * 0.5f,
					center + new Vector3(-size.x, -size.y, size.z) * 0.5f,
					center + new Vector3(-size.x, size.y, -size.z) * 0.5f,
					center + new Vector3(size.x, size.y, -size.z) * 0.5f,
					center + new Vector3(size.x, size.y, size.z) * 0.5f,
					center + new Vector3(-size.x, size.y, size.z) * 0.5f
				};

				// Transform to world space using the collider's transform
				for (int i = 0; i < 8; i++)
				{
					worldCorners[i] = collider.transform.TransformPoint(localCorners[i]);
				}
			}
			else if (collider is MeshCollider meshCollider)
			{
				if (meshCollider.sharedMesh != null)
				{
					// Get the mesh bounds in local space
					Bounds meshBounds = meshCollider.sharedMesh.bounds;
					Vector3 meshSize = meshBounds.size;

					// Calculate half extents
					Vector3 halfSize = meshSize * 0.5f;
					// Create corners around origin (0,0,0), NOT around meshBounds.center
					// This gives us the actual mesh corners in local space
					Vector3[] localCorners = new Vector3[8];

					// Bottom corners (min Y)
					localCorners[0] = meshBounds.center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
					localCorners[1] = meshBounds.center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
					localCorners[2] = meshBounds.center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
					localCorners[3] = meshBounds.center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);

					// Top corners (max Y)
					localCorners[4] = meshBounds.center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
					localCorners[5] = meshBounds.center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
					localCorners[6] = meshBounds.center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
					localCorners[7] = meshBounds.center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

					// Transform to world space using the collider's transform
					for (int i = 0; i < 8; i++)
					{
						worldCorners[i] = collider.transform.TransformPoint(localCorners[i]);
					}
				}
			}
			else
			{
				// Fallback for other collider types
				Bounds bounds = collider.bounds;
				Vector3 min = bounds.min;
				Vector3 max = bounds.max;

				worldCorners[0] = new Vector3(min.x, min.y, min.z);
				worldCorners[1] = new Vector3(max.x, min.y, min.z);
				worldCorners[2] = new Vector3(max.x, min.y, max.z);
				worldCorners[3] = new Vector3(min.x, min.y, max.z);
				worldCorners[4] = new Vector3(min.x, max.y, min.z);
				worldCorners[5] = new Vector3(max.x, max.y, min.z);
				worldCorners[6] = new Vector3(max.x, max.y, max.z);
				worldCorners[7] = new Vector3(min.x, max.y, max.z);
			}

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogWarning($"[DEBUG] World corners Y values: Bottom={worldCorners[0].y}, Top={worldCorners[4].y}");
				Logger.LogWarning($"[DEBUG] Expected Y range: {collider.bounds.min.y} to {collider.bounds.max.y}");
			}
			return worldCorners;
		}

		public void SetVisible(bool visible)
		{
			if (wireframeRoot != null)
				wireframeRoot.SetActive(visible);
		}

		private void OnDestroy()
		{
			if (wireframeRoot != null)
				Destroy(wireframeRoot);
		}
	}
}