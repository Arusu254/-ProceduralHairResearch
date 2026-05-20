using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class HairGeneratorCore
{
    // 可配置的发根Y轴阈值（比例），由窗口传入
    public static float yThresholdRatio = 0.7f;

    public static Mesh GenerateHairMesh(
        Mesh scalpMesh,
        Vector3[] rootPoints,
        Vector3[] rootNormals,
        Vector3[] rootDirections,
        int hairCount,
        float length,
        float curl,
        float thickness,
        Vector3[] rootModifiers,
        bool[] rootActive)
    {
        if (scalpMesh == null || rootPoints == null) return null;

        List<Mesh> hairMeshes = new List<Mesh>();

        for (int i = 0; i < rootPoints.Length; i++)
        {
            if (rootActive != null && i < rootActive.Length && !rootActive[i])
                continue;

            Vector3 root = rootPoints[i];
            Vector3 direction = (rootDirections != null && i < rootDirections.Length) ? rootDirections[i] : Vector3.up;

            float modLen = (rootModifiers != null && i < rootModifiers.Length) ? rootModifiers[i].x : 1f;
            float modCurl = (rootModifiers != null && i < rootModifiers.Length) ? rootModifiers[i].y : 1f;
            float modThick = (rootModifiers != null && i < rootModifiers.Length) ? rootModifiers[i].z : 1f;

            float finalLen = length * modLen;
            float finalCurl = curl * modCurl;
            float finalThick = thickness * modThick;

            Vector3 p0 = root;
            Vector3 p1 = root + direction * (finalLen * 0.4f) + Random.insideUnitSphere * finalCurl * 0.2f;
            Vector3 p2 = root + direction * (finalLen * 0.8f) + Random.insideUnitSphere * finalCurl * 0.35f;
            Vector3 p3 = root + direction * finalLen;

            Vector3[] baseCurve = BezierCurve.GetCurvePoints(p0, p1, p2, p3, 8);
            Mesh strandMesh = HairStrandMesh.GenerateMesh(baseCurve, finalThick, 5);
            if (strandMesh != null && strandMesh.vertexCount > 0)
                hairMeshes.Add(strandMesh);
        }

        if (hairMeshes.Count == 0) return null;

        CombineInstance[] combine = new CombineInstance[hairMeshes.Count];
        for (int i = 0; i < hairMeshes.Count; i++)
        {
            combine[i].mesh = hairMeshes[i];
            combine[i].transform = Matrix4x4.identity;
        }
        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combine, true, false);
        combinedMesh.RecalculateBounds();
        return combinedMesh;
    }

    public static Vector3[] SamplePointsOnScalp(Mesh scalpMesh, int count, out Vector3[] normals)
    {
        Vector3[] vertices = scalpMesh.vertices;
        Vector3[] meshNormals = scalpMesh.normals;
        List<Vector3> validPoints = new List<Vector3>();
        List<Vector3> validNormals = new List<Vector3>();

        float minY = GetMeshMinY(vertices);
        float maxY = GetMeshMaxY(vertices);
        float thresholdY = minY + (maxY - minY) * yThresholdRatio;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].y > thresholdY)
            {
                Vector3 n = (meshNormals.Length > i && meshNormals[i] != Vector3.zero) ? meshNormals[i].normalized : Vector3.up;
                validPoints.Add(vertices[i] + n * 0.02f);
                validNormals.Add(n);
            }
        }

        if (validPoints.Count == 0)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = (meshNormals.Length > i && meshNormals[i] != Vector3.zero) ? meshNormals[i].normalized : Vector3.up;
                validPoints.Add(vertices[i] + n * 0.02f);
                validNormals.Add(n);
            }
        }

        Vector3[] results = new Vector3[count];
        normals = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, validPoints.Count);
            results[i] = validPoints[idx];
            normals[i] = validNormals[idx];
        }
        return results;
    }

    private static float GetMeshMinY(Vector3[] verts)
    {
        float min = float.MaxValue;
        foreach (var v in verts) if (v.y < min) min = v.y;
        return min;
    }

    private static float GetMeshMaxY(Vector3[] verts)
    {
        float max = float.MinValue;
        foreach (var v in verts) if (v.y > max) max = v.y;
        return max;
    }
}