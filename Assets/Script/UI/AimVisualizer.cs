using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AimVisualizer : MonoBehaviour
{
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    [Header("Visual Settings")]
    [SerializeField] private Material coneMaterial; 
    [SerializeField] private int sortingOrder = 0;
    [SerializeField] private Color optimalColor ;  
    [SerializeField] private Color falloffColor ; 
    [SerializeField] private Color lazerColor; 
    [SerializeField] private int resolution = 20; 

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        meshRenderer.sortingOrder = sortingOrder;
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
    
    public void DrawLine(Vector3 origin, Vector2 direction, float length, float width, Color color)
    {
        Vector3[] vertices = new Vector3[4];
        Color[] colors = new Color[4];
        int[] triangles = new int[6];

        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0) * (width / 2f);
        Vector3 endPoint = origin + (Vector3)direction * length;

        vertices[0] = transform.InverseTransformPoint(origin - perpendicular);
        vertices[1] = transform.InverseTransformPoint(origin + perpendicular);
        vertices[2] = transform.InverseTransformPoint(endPoint - perpendicular);
        vertices[3] = transform.InverseTransformPoint(endPoint + perpendicular);

        for (int i = 0; i < 4; i++) { colors[i] = color; }

        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 2; triangles[4] = 1; triangles[5] = 3;
        
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
    public void Hide() 
    {
        mesh.Clear();
    }
}