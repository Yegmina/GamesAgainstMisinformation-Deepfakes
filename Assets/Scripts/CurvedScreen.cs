using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class CurvedScreen : MonoBehaviour
{
    [Header("Curvature Settings")]
    [Tooltip("How much the screen curves. Positive = concave (curves inward), Negative = convex (curves outward), 0 = perfectly flat.")]
    [Range(-0.1f, 0.1f)]
    public float depth = -0.1f;

    [Tooltip("The number of segments in the mesh. Higher values make the curve smoother.")]
    [Range(4, 64)]
    public int segments = 32;

    private MeshFilter meshFilter;
    private Mesh generatedMesh;

    private float lastDepth;
    private int lastSegments;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        GenerateCurvedMesh();
    }

    private void Update()
    {
        // Check if values changed in play mode or editor update
        if (!Mathf.Approximately(depth, lastDepth) || segments != lastSegments)
        {
            GenerateCurvedMesh();
        }
    }

    private void OnValidate()
    {
        // Handles real-time updates when sliding values in the Inspector
        GenerateCurvedMesh();
    }

    [ContextMenu("Force Regenerate")]
    public void GenerateCurvedMesh()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshFilter == null) return;

        // Clean up previous generated mesh to prevent memory leaks in editor
        if (generatedMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
        }

        int verticesCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[verticesCount];
        Vector2[] uv = new Vector2[verticesCount];
        Vector3[] normals = new Vector3[verticesCount];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float x = t - 0.5f; // Center vertices on X axis from -0.5 to 0.5

            // Parabolic curvature formula: 0 at edges, -depth at center
            float z = depth * (4f * x * x - 1f);

            // Top vertex
            int topIndex = i * 2;
            vertices[topIndex] = new Vector3(x, 0.5f, z);
            uv[topIndex] = new Vector2(t, 1f);

            // Tangent: dx = 1, dy = 0, dz = 8 * depth * x
            Vector3 tangent = new Vector3(1f, 0f, 8f * depth * x).normalized;
            normals[topIndex] = Vector3.Cross(tangent, Vector3.up).normalized;

            // Bottom vertex
            int bottomIndex = i * 2 + 1;
            vertices[bottomIndex] = new Vector3(x, -0.5f, z);
            uv[bottomIndex] = new Vector2(t, 0f);
            normals[bottomIndex] = normals[topIndex];
        }

        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int tl = i * 2;
            int bl = i * 2 + 1;
            int tr = (i + 1) * 2;
            int br = (i + 1) * 2 + 1;

            triangles[triIndex++] = tl;
            triangles[triIndex++] = tr;
            triangles[triIndex++] = bl;

            triangles[triIndex++] = bl;
            triangles[triIndex++] = tr;
            triangles[triIndex++] = br;
        }

        generatedMesh = new Mesh();
        generatedMesh.name = "CurvedScreen_ProceduralMesh";
        generatedMesh.vertices = vertices;
        generatedMesh.uv = uv;
        generatedMesh.normals = normals;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateBounds();

        meshFilter.sharedMesh = generatedMesh;

        lastDepth = depth;
        lastSegments = segments;
    }

    private void OnDestroy()
    {
        // Clean up when component/object is removed or scene changes
        if (generatedMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }
        }
    }
}
