using UnityEngine;

[System.Serializable]
public class HairRootData
{
    public Vector3 position;      // 发根点位置（局部坐标）
    public Vector3 modifiers;     // x:长度乘数, y:卷曲乘数, z:粗细乘数
    public bool selected;         // 是否被选中

    public HairRootData(Vector3 pos)
    {
        position = pos;
        modifiers = Vector3.one;  // 默认乘数为1
        selected = false;
    }
}