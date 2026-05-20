using UnityEngine;

public static class HairStrandMesh
{
    public static Mesh GenerateMesh(Vector3[] points, float radius, int radialSegments = 6)
    {
        if (points.Length < 2) return null;
        int segments = points.Length - 1;
        int verticesPerLayer = radialSegments;
        int totalVertices = points.Length * verticesPerLayer;

        Vector3[] vertices = new Vector3[totalVertices];
        Vector2[] uvs = new Vector2[totalVertices];
        int[] triangles = new int[segments * radialSegments * 6];

        Vector3[] forwardDirs = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (i == 0) forwardDirs[i] = (points[1] - points[0]).normalized;
            else if (i == points.Length - 1) forwardDirs[i] = (points[i] - points[i - 1]).normalized;
            else forwardDirs[i] = (points[i + 1] - points[i - 1]).normalized;
        }

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 center = points[i];
            Vector3 forward = forwardDirs[i];
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f) up = Vector3.forward;
            Vector3 right = Vector3.Cross(forward, up).normalized;
            Vector3 localUp = Vector3.Cross(right, forward).normalized;

            for (int j = 0; j < radialSegments; j++)
            {
                float angle = j * 360f / radialSegments * Mathf.Deg2Rad;
                Vector3 offset = (right * Mathf.Cos(angle) + localUp * Mathf.Sin(angle)) * radius;
                vertices[i * radialSegments + j] = center + offset;
                uvs[i * radialSegments + j] = new Vector2(j / (float)radialSegments, i / (float)(points.Length - 1));
            }
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int nextJ = (j + 1) % radialSegments;
                int i0 = i * radialSegments + j;
                int i1 = i * radialSegments + nextJ;
                int i2 = (i + 1) * radialSegments + j;
                int i3 = (i + 1) * radialSegments + nextJ;
                triangles[triIdx++] = i0;
                triangles[triIdx++] = i1;
                triangles[triIdx++] = i2;
                triangles[triIdx++] = i1;
                triangles[triIdx++] = i3;
                triangles[triIdx++] = i2;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}