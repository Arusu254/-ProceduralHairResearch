using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GuideCurve : MonoBehaviour
{
    public List<Vector3> controlPoints = new List<Vector3>();
    public float radius = 0.3f;
    public float weight = 1f;

    public static System.Action OnCurveChanged;

    private void OnDrawGizmosSelected()
    {
        if (controlPoints.Count < 4) return;
        Gizmos.color = Color.cyan;
        Vector3 prev = transform.TransformPoint(controlPoints[0]);
        for (int i = 1; i <= 32; i++)
        {
            float t = i / 32f;
            Vector3 p = BezierCurve.GetPoint(controlPoints[0], controlPoints[1], controlPoints[2], controlPoints[3], t);
            p = transform.TransformPoint(p);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.color = Color.white;
        foreach (var cp in controlPoints)
        {
            Gizmos.DrawSphere(transform.TransformPoint(cp), 0.05f);
        }
    }

    public Vector3 GetWorldPoint(float t)
    {
        Vector3 local = BezierCurve.GetPoint(controlPoints[0], controlPoints[1], controlPoints[2], controlPoints[3], t);
        return transform.TransformPoint(local);
    }

    public Vector3 GetClosestPoint(Vector3 worldPoint)
    {
        float bestT = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 p = GetWorldPoint(t);
            float d = Vector3.Distance(p, worldPoint);
            if (d < bestDist) { bestDist = d; bestT = t; }
        }
        return GetWorldPoint(bestT);
    }

#if UNITY_EDITOR
    private void OnSceneGUI()
    {
        if (controlPoints == null || controlPoints.Count < 4) return;

        Handles.DrawBezier(
            transform.TransformPoint(controlPoints[0]),
            transform.TransformPoint(controlPoints[3]),
            transform.TransformPoint(controlPoints[1]),
            transform.TransformPoint(controlPoints[2]),
            Color.cyan, null, 4f);

        for (int i = 0; i < controlPoints.Count; i++)
        {
            Vector3 worldPos = transform.TransformPoint(controlPoints[i]);
            float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.2f;
            EditorGUI.BeginChangeCheck();
            var fmh_70_68_639147165895212850 = Quaternion.identity; Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move Control Point");
                controlPoints[i] = transform.InverseTransformPoint(newWorldPos);
                if (OnCurveChanged != null)
                    OnCurveChanged();
            }
        }
    }
#endif
}