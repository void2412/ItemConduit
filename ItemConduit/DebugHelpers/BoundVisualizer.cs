using UnityEngine;
using System.Collections.Generic;

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
}