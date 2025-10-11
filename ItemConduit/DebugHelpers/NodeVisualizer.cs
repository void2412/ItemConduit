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


	
}