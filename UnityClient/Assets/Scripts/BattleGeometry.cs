using UnityEngine;

// 在设计空间后乘局部旋转；GUIUtility 的屏幕轴旋转会破坏缩放和平移后的枢轴。
public static class BattleGeometry
{
    public static Matrix4x4 Rotation(float angle,Vector2 pivot)
    {return Matrix4x4.TRS(pivot,Quaternion.Euler(0,0,angle),Vector3.one)*Matrix4x4.Translate(-pivot);}
    public static void Rotate(float angle,Vector2 pivot){GUI.matrix=GUI.matrix*Rotation(angle,pivot);}
    public static Vector2 ArrowPoint(Vector2 from,Vector2 to,float t)
    {return Vector2.Lerp(from,to,t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*Mathf.Min(34,Vector2.Distance(from,to)*.1f));}
    public static Vector2 ArrowTangent(Vector2 from,Vector2 to,float t)
    {return to-from+Vector2.up*(Mathf.Cos(t*Mathf.PI)*Mathf.PI*Mathf.Min(34,Vector2.Distance(from,to)*.1f));}
    public static Rect EdgeRect(DesignViewport viewport,bool right,float y,float width,float height,float inset=22)
    {
        float edge=viewport.Matrix.inverse.MultiplyPoint3x4(new Vector3(right?viewport.WindowWidth:0,0,0)).x;
        return new Rect(right?edge-inset-width:edge+inset,y,width,height);
    }
}
