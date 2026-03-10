using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AimConeRenderer : MonoBehaviour
{
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    [Header("Visual Settings")]
    public Material coneMaterial; // We will assign a transparent material here
    public Color optimalColor = new Color(1f, 0f, 0f, 0.3f);  // Translucent Red
    public Color falloffColor = new Color(1f, 1f, 0f, 0.15f); // Translucent Yellow
    public int resolution = 20; // How smooth the curved edge is

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
    }

    public void DrawCone(Vector3 origin, Vector2 direction, float arcAngle, float currentRangeX, float currentRangeY)
    {
        // Calculate the starting angle
        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startAngle = aimAngle - (arcAngle / 2f);
        float angleStep = arcAngle / resolution;

        int vertexCount = 1 + (resolution + 1) * 2; 
        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[resolution * 3 + resolution * 6];

        vertices[0] = transform.InverseTransformPoint(origin); 
        colors[0] = optimalColor;

        // 2. Build the Vertices
        for (int i = 0; i <= resolution; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);

            // OPTIMAL VERTEX
            vertices[i + 1] = transform.InverseTransformPoint(origin + dir * currentRangeX);
            colors[i + 1] = optimalColor;

            // FALLOFF VERTEX
            vertices[i + 1 + (resolution + 1)] = transform.InverseTransformPoint(origin + dir * currentRangeY);
            colors[i + 1 + (resolution + 1)] = falloffColor;
        }

        // 3. Build the Triangles
        int t = 0;
        for (int i = 0; i < resolution; i++)
        {
            // Inner Optimal Triangle
            triangles[t++] = 0;
            triangles[t++] = i + 1;
            triangles[t++] = i + 2;

            // Outer Falloff Quad (2 Triangles)
            int opt1 = i + 1;
            int opt2 = i + 2;
            int fall1 = i + 1 + (resolution + 1);
            int fall2 = i + 2 + (resolution + 1);

            triangles[t++] = opt1;
            triangles[t++] = fall1;
            triangles[t++] = fall2;

            triangles[t++] = opt1;
            triangles[t++] = fall2;
            triangles[t++] = opt2;
        }

        // 4. Update the Mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    public void HideCone()
    {
        mesh.Clear();
    }
}