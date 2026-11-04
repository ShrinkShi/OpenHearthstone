using System;
using UnityEngine;

// 仅用于已导出的无FSM视觉图，不递归反序列化通用动作描述符。
[Serializable] public sealed class NativeVisualProgram
{
    [Serializable] public sealed class MachineMarker{public string name;}
    public string reference;public NativeNode[] nodes;public MachineMarker[] machines;
    public static NativeProgram Parse(string json)
    {
        var visual=JsonUtility.FromJson<NativeVisualProgram>(json);
        if(visual.nodes==null||visual.nodes.Length==0||visual.machines==null||visual.machines.Length!=0)
            throw new ArgumentException("Only reviewed native visual graphs without FSM are supported");
        return new NativeProgram{reference=visual.reference,nodes=visual.nodes,machines=new NativeMachine[0],animations=new NativeAnimation[0],captures=new NativeCapture[0],unknown_actions=new string[0]};
    }
}
