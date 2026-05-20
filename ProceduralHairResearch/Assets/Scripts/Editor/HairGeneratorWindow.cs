using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class HairGeneratorWindow : EditorWindow
{
    private Mesh importedHeadMesh;
    private GameObject previewHead;
    private GameObject previewHair;
    private Mesh generatedHairMesh;
    private Material hairMaterial;

    private int hairCount = 2000;
    private float length = 0.8f;
    private float curl = 0.3f;
    private float thickness = 0.012f;
    private float yThreshold = 0.7f;

    private Vector3[] cachedRootPoints;
    private Vector3[] cachedRootNormals;
    private Vector3[] cachedRootDirections;
    private Vector3[] rootModifiers;   // x=长度乘数, y=卷曲乘数, z=粗细乘数
    private bool[] rootActive;
    private bool hasGeneratedOnce = false;

    // 选区相关
    private bool selectMode = false;
    private float brushRadius = 0.15f;
    private bool[] selectedRoots;
    private bool showSelectedPoints = true;

    // 局部参数调节滑块
    private float localLengthMult = 1f;
    private float localCurlMult = 1f;
    private float localThickMult = 1f;

    [MenuItem("Tools/程序化毛发生成器")]
    public static void ShowWindow()
    {
        GetWindow<HairGeneratorWindow>("毛发编辑器");
    }

    private void OnEnable()
    {
        hairMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HairMaterial.mat");
        if (hairMaterial == null)
        {
            hairMaterial = new Material(Shader.Find("Standard"));
            hairMaterial.color = new Color(0.6f, 0.4f, 0.2f);
            hairMaterial.enableInstancing = true;
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(hairMaterial, "Assets/Materials/HairMaterial.mat");
        }
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("头模导入", EditorStyles.boldLabel);
        if (GUILayout.Button("导入 OBJ 头模"))
        {
            string path = EditorUtility.OpenFilePanel("选择 OBJ 文件", "", "obj");
            if (!string.IsNullOrEmpty(path))
                ImportHeadMesh(path);
        }

        if (importedHeadMesh != null)
        {
            EditorGUILayout.LabelField("当前头模", importedHeadMesh.name);
            EditorGUILayout.LabelField("顶点数", importedHeadMesh.vertexCount.ToString());
            if (GUILayout.Button("清除头模"))
                ClearHeadMesh();
        }

        GUILayout.Space(10);
        GUILayout.Label("毛发参数", EditorStyles.boldLabel);
        int oldCount = hairCount;
        float oldLength = length;
        float oldCurl = curl;
        float oldThick = thickness;
        float oldYThres = yThreshold;

        hairCount = EditorGUILayout.IntSlider("发丝数量", hairCount, 100, 10000);
        length = EditorGUILayout.Slider("长度", length, 0.2f, 2.5f);
        curl = EditorGUILayout.Slider("卷曲度", curl, 0f, 2f);
        thickness = EditorGUILayout.Slider("粗细", thickness, 0.005f, 0.03f);
        yThreshold = EditorGUILayout.Slider("发根Y值阈值（头顶区域比例）", yThreshold, 0.1f, 0.9f);

        bool paramsChanged = (hairCount != oldCount || length != oldLength || curl != oldCurl || thickness != oldThick || yThreshold != oldYThres);

        GUILayout.Space(10);
        GUILayout.Label("选区编辑", EditorStyles.boldLabel);
        selectMode = GUILayout.Toggle(selectMode, "进入选区模式（点击头模选中发根）", "Button");
        if (selectMode)
        {
            brushRadius = EditorGUILayout.Slider("选区半径", brushRadius, 0.05f, 0.5f);
            showSelectedPoints = EditorGUILayout.Toggle("显示选中的发根", showSelectedPoints);
            if (GUILayout.Button("清除选区"))
            {
                if (selectedRoots != null)
                    System.Array.Clear(selectedRoots, 0, selectedRoots.Length);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("删除选中的发根"))
            {
                if (selectedRoots != null)
                {
                    for (int i = 0; i < selectedRoots.Length; i++)
                        if (selectedRoots[i]) rootActive[i] = false;
                    GenerateHair(false);
                }
            }
            if (GUILayout.Button("恢复选中的发根"))
            {
                if (selectedRoots != null)
                {
                    for (int i = 0; i < selectedRoots.Length; i++)
                        if (selectedRoots[i]) rootActive[i] = true;
                    GenerateHair(false);
                }
            }

            GUILayout.Space(5);
            GUILayout.Label("局部参数调节（应用于选中的发根）", EditorStyles.boldLabel);
            localLengthMult = EditorGUILayout.Slider("局部长度乘数", localLengthMult, 0.2f, 2.5f);
            localCurlMult = EditorGUILayout.Slider("局部卷曲乘数", localCurlMult, 0f, 2f);
            localThickMult = EditorGUILayout.Slider("局部粗细乘数", localThickMult, 0.5f, 2f);
            if (GUILayout.Button("应用局部参数到选中的发根"))
            {
                if (selectedRoots != null && rootModifiers != null)
                {
                    for (int i = 0; i < selectedRoots.Length; i++)
                    {
                        if (selectedRoots[i])
                        {
                            rootModifiers[i] = new Vector3(localLengthMult, localCurlMult, localThickMult);
                        }
                    }
                    GenerateHair(false);
                }
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("生成毛发", GUILayout.Height(30)))
        {
            GenerateHair(true);
        }

        if (generatedHairMesh != null)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("导出为 OBJ", GUILayout.Height(25)))
                ExportHair();
        }

        if (hasGeneratedOnce && paramsChanged && importedHeadMesh != null)
        {
            GenerateHair(false);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!selectMode || importedHeadMesh == null || cachedRootPoints == null) return;

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (previewHead != null && previewHead.GetComponent<MeshCollider>() == null)
                previewHead.AddComponent<MeshCollider>();

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == previewHead)
                {
                    Vector3 hitPoint = hit.point;
                    for (int i = 0; i < cachedRootPoints.Length; i++)
                    {
                        float dist = Vector3.Distance(cachedRootPoints[i], hitPoint);
                        if (dist < brushRadius)
                            selectedRoots[i] = true;
                    }
                    e.Use();
                    SceneView.RepaintAll();
                }
            }
        }

        if (showSelectedPoints && selectedRoots != null)
        {
            Handles.BeginGUI();
            for (int i = 0; i < cachedRootPoints.Length; i++)
            {
                if (selectedRoots[i])
                {
                    Vector3 screenPos = HandleUtility.WorldToGUIPoint(cachedRootPoints[i]);
                    Rect rect = new Rect(screenPos.x - 3, screenPos.y - 3, 6, 6);
                    EditorGUI.DrawRect(rect, Color.red);
                }
            }
            Handles.EndGUI();
        }
    }

    private void ImportHeadMesh(string objPath)
    {
        Mesh mesh = OBJLoader.LoadOBJ(objPath);
        if (mesh == null)
        {
            EditorUtility.DisplayDialog("错误", "OBJ 文件解析失败", "确定");
            return;
        }

        mesh = RotateMeshToYUp(mesh);
        string assetPath = "Assets/ImportedHead.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath))
            AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
        importedHeadMesh = mesh;

        if (previewHead == null)
            previewHead = GameObject.Find("PreviewHead");
        if (previewHead == null)
            previewHead = new GameObject("PreviewHead");
        var mf = previewHead.GetComponent<MeshFilter>();
        if (mf == null) mf = previewHead.AddComponent<MeshFilter>();
        var mr = previewHead.GetComponent<MeshRenderer>();
        if (mr == null) mr = previewHead.AddComponent<MeshRenderer>();
        mf.sharedMesh = importedHeadMesh;
        if (mr.sharedMaterial == null)
            mr.sharedMaterial = new Material(Shader.Find("Standard")) { color = Color.gray };

        previewHead.AddComponent<MeshCollider>();

        cachedRootPoints = null;
        cachedRootNormals = null;
        cachedRootDirections = null;
        rootModifiers = null;
        rootActive = null;
        selectedRoots = null;
        hasGeneratedOnce = false;

        if (previewHair != null) DestroyImmediate(previewHair);
        generatedHairMesh = null;

        EditorUtility.DisplayDialog("导入成功", $"头模已导入，顶点数 {mesh.vertexCount}\n请点击“生成毛发”按钮", "确定");
    }

    private void ClearHeadMesh()
    {
        importedHeadMesh = null;
        if (previewHead != null) DestroyImmediate(previewHead);
        if (generatedHairMesh != null) DestroyImmediate(generatedHairMesh);
        if (previewHair != null) DestroyImmediate(previewHair);
        cachedRootPoints = null;
        cachedRootNormals = null;
        cachedRootDirections = null;
        rootModifiers = null;
        rootActive = null;
        selectedRoots = null;
        hasGeneratedOnce = false;
        Debug.Log("头模已清除");
    }

    private Mesh RotateMeshToYUp(Mesh originalMesh)
    {
        Quaternion rotation = Quaternion.Euler(-90, 0, 0);
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = rotation * vertices[i];
        for (int i = 0; i < normals.Length; i++) normals[i] = rotation * normals[i];

        Mesh rotatedMesh = new Mesh();
        rotatedMesh.vertices = vertices;
        rotatedMesh.triangles = originalMesh.triangles;
        if (originalMesh.uv.Length == vertices.Length) rotatedMesh.uv = originalMesh.uv;
        rotatedMesh.normals = normals;
        rotatedMesh.RecalculateBounds();
        return rotatedMesh;
    }

    public void GenerateHair(bool forceResample)
    {
        if (importedHeadMesh == null)
        {
            EditorUtility.DisplayDialog("提示", "请先导入头模", "确定");
            return;
        }

        HairGeneratorCore.yThresholdRatio = yThreshold;

        if (forceResample || cachedRootPoints == null || cachedRootPoints.Length != hairCount)
        {
            cachedRootPoints = HairGeneratorCore.SamplePointsOnScalp(importedHeadMesh, hairCount, out cachedRootNormals);
            cachedRootDirections = GenerateRandomDirections(cachedRootPoints.Length);
            rootModifiers = new Vector3[cachedRootPoints.Length];
            rootActive = new bool[cachedRootPoints.Length];
            selectedRoots = new bool[cachedRootPoints.Length];
            for (int i = 0; i < rootModifiers.Length; i++)
            {
                rootModifiers[i] = Vector3.one;
                rootActive[i] = true;
            }
            hasGeneratedOnce = true;
        }

        EditorUtility.DisplayProgressBar("生成毛发", "正在计算...", 0.5f);
        generatedHairMesh = HairGeneratorCore.GenerateHairMesh(
            importedHeadMesh, cachedRootPoints, cachedRootNormals, cachedRootDirections, hairCount,
            length, curl, thickness, rootModifiers, rootActive);
        EditorUtility.ClearProgressBar();

        if (generatedHairMesh == null) return;

        if (previewHair == null)
            previewHair = GameObject.Find("PreviewHair");
        if (previewHair == null)
            previewHair = new GameObject("PreviewHair");
        var mf = previewHair.GetComponent<MeshFilter>();
        if (mf == null) mf = previewHair.AddComponent<MeshFilter>();
        var mr = previewHair.GetComponent<MeshRenderer>();
        if (mr == null) mr = previewHair.AddComponent<MeshRenderer>();
        mf.sharedMesh = generatedHairMesh;
        mr.sharedMaterial = hairMaterial;

        Debug.Log($"毛发生成完成：{hairCount} 根发丝，顶点数 {generatedHairMesh.vertexCount}");
    }

    private Vector3[] GenerateRandomDirections(int count)
    {
        Vector3[] dirs = new Vector3[count];
        float randomStrength = 0.6f;
        for (int i = 0; i < count; i++)
        {
            dirs[i] = (Vector3.up + Random.onUnitSphere * randomStrength).normalized;
        }
        return dirs;
    }

    private void ExportHair()
    {
        if (generatedHairMesh == null)
        {
            EditorUtility.DisplayDialog("提示", "请先生成毛发", "确定");
            return;
        }
        string savePath = EditorUtility.SaveFilePanel("导出 OBJ 文件", "", "hair", "obj");
        if (string.IsNullOrEmpty(savePath)) return;
        OBJExporter.ExportMeshToOBJ(generatedHairMesh, savePath);
        EditorUtility.DisplayDialog("导出成功", $"已保存到 {savePath}", "确定");
    }
}