using UnityEngine;

// Source ShaderTime service: global shader animation is independent of cue seeks.
public sealed class NativeShaderTime
{
    public float Value{get;private set;}
    public bool Enabled=true;
    public void Update(float deltaTime,Vector3 gravity)
    {
        if(!Enabled)Value=1;
        else
        {
            Value+=deltaTime/20f;
            // The source subtracts once, with a strict comparison (not modulo).
            if(Value>65535f){Value-=65535f;if(Value<=0)Value=.0001f;}
        }
        Shader.SetGlobalFloat("_ShaderTime",Value);
        Shader.SetGlobalVector("_Gyroscope",new Vector4(gravity.x,gravity.y,gravity.z,0));
    }
    public void Shutdown(){Shader.SetGlobalFloat("_ShaderTime",0);}
}
