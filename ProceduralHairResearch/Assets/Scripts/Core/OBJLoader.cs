using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class OBJLoader
{
    public static Mesh LoadOBJ(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("文件不存在: " + filePath);
            return null;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        CultureInfo ci = CultureInfo.InvariantCulture;

        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed)) continue;
            string[] parts = trimmed.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "v":
                    if (parts.Length >= 4)
                    {
                        float x = ParseFloat(parts[1], ci);
                        float y = ParseFloat(parts[2], ci);
                        float z = ParseFloat(parts[3], ci);
                        vertices.Add(new Vector3(x, y, z));
                    }
                    break;
                case "vt":
                    if (parts.Length >= 3)
                    {
                        float u = ParseFloat(parts[1], ci);
                        float v = ParseFloat(parts[2], ci);
                        uvs.Add(new Vector2(u, v));
                    }
                    break;
                case "vn":
                    if (parts.Length >= 4)
                    {
                        float nx = ParseFloat(parts[1], ci);
                        float ny = ParseFloat(parts[2], ci);
                        float nz = ParseFloat(parts[3], ci);
                        normals.Add(new Vector3(nx, ny, nz));
                    }
                    break;
                case "f":
                    List<int> faceVerts = new List<int>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string[] sub = parts[i].Split('/');
                        int vi = int.Parse(sub[0]) - 1;
                        faceVerts.Add(vi);
                    }
                    for (int i = 1; i < faceVerts.Count - 1; i++)
                    {
                        triangles.Add(faceVerts[0]);
                        triangles.Add(faceVerts[i]);
                        triangles.Add(faceVerts[i + 1]);
                    }
                    break;
            }
        }

        if (vertices.Count == 0) return null;
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        if (uvs.Count == vertices.Count)
            mesh.uv = uvs.ToArray();
        else
            Debug.LogWarning("UV数量不匹配，跳过UV");

        if (normals.Count == vertices.Count)
            mesh.normals = normals.ToArray();
        else
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        return mesh;
    }

    private static float ParseFloat(string s, CultureInfo ci)
    {
        s = s.Replace(',', '.');
        if (float.TryParse(s, NumberStyles.Float, ci, out float result))
            return result;
        Debug.LogWarning("无法解析浮点数: " + s);
        return 0f;
    }
}