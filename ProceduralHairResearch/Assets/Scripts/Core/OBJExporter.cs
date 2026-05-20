using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class OBJExporter
{
    public static void ExportMeshToOBJ(Mesh mesh, string filePath)
    {
        if (mesh == null) return;

#if UNITY_EDITOR
        if (mesh.vertexCount > 500000)
        {
            if (!EditorUtility.DisplayDialog("顶点数过多",
                $"当前毛发顶点数 {mesh.vertexCount}，导出可能非常耗时甚至卡死。\n建议降低发丝数量后再导出。是否继续？",
                "继续", "取消"))
                return;
        }
#endif

        using (StreamWriter sw = new StreamWriter(filePath))
        {
            int vertCount = mesh.vertexCount;
            int triCount = mesh.triangles.Length / 3;

            for (int i = 0; i < vertCount; i++)
            {
                Vector3 v = mesh.vertices[i];
                sw.WriteLine($"v {v.x:F3} {v.y:F3} {v.z:F3}");
                if (i % 5000 == 0)
                {
#if UNITY_EDITOR
                    EditorUtility.DisplayProgressBar("导出 OBJ", $"写入顶点 {i}/{vertCount}", (float)i / vertCount);
#endif
                }
            }

            int[] tris = mesh.triangles;
            for (int i = 0; i < triCount; i++)
            {
                int i1 = tris[i * 3] + 1;
                int i2 = tris[i * 3 + 1] + 1;
                int i3 = tris[i * 3 + 2] + 1;
                sw.WriteLine($"f {i1} {i2} {i3}");
                if (i % 10000 == 0)
                {
#if UNITY_EDITOR
                    EditorUtility.DisplayProgressBar("导出 OBJ", $"写入面 {i}/{triCount}", 0.5f + 0.5f * i / triCount);
#endif
                }
            }
        }

#if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
        Debug.Log($"导出完成：{filePath}，顶点数 {mesh.vertexCount}");
#endif
    }
}