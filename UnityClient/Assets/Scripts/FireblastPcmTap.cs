using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 只录本Unity进程AudioListener的引擎混音，不使用系统回环或合成PCM。
public sealed class FireblastPcmTap:MonoBehaviour
{
    readonly object gate=new object();readonly Queue<float[]> blocks=new Queue<float[]>(),pool=new Queue<float[]>();
    BinaryWriter writer;bool recording;int channels;double energy;
    public int Rate{get;private set;}public long SampleFrames{get;private set;}public float Peak{get;private set;}
    public int Channels{get{return channels;}}public int DspBuffer{get;private set;}public int DspBuffers{get;private set;}
    public int Callbacks{get;private set;}public int Overflows{get;private set;}public long ClippedSamples{get;private set;}
    public float Rms{get{return SampleFrames==0?0:(float)Math.Sqrt(energy/(SampleFrames*Math.Max(1,channels)));}}
    public void Begin(string path)
    {
        Rate=AudioSettings.outputSampleRate;if(Rate<=0)throw new InvalidOperationException("No Unity DSP output rate");
        if(AudioSettings.GetConfiguration().speakerMode!=AudioSpeakerMode.Stereo)throw new NotSupportedException("Bounded DSP probe expects stereo output");
        channels=2;int buffer,buffers;AudioSettings.GetDSPBufferSize(out buffer,out buffers);DspBuffer=buffer;DspBuffers=buffers;
        for(int i=0;i<128;i++)pool.Enqueue(new float[buffer*channels]);
        writer=new BinaryWriter(File.Open(path,FileMode.CreateNew,FileAccess.Write));Header(0);lock(gate)recording=true;
    }
    void OnAudioFilterRead(float[] data,int channelCount)
    {
        lock(gate)
        {
            if(!recording)return;
            Callbacks++;
            if(channels!=channelCount||pool.Count==0){Overflows++;return;}
            var copy=pool.Dequeue();if(copy.Length!=data.Length){pool.Enqueue(copy);Overflows++;return;}
            Array.Copy(data,copy,data.Length);blocks.Enqueue(copy);
        }
    }
    void Update(){Flush();}
    void Flush()
    {
        if(writer==null)return;
        while(true)
        {
            float[] block;lock(gate){if(blocks.Count==0)break;block=blocks.Dequeue();}
            foreach(float value in block){Peak=Math.Max(Peak,Math.Abs(value));energy+=(double)value*value;if(Math.Abs(value)>1)ClippedSamples++;writer.Write(value);}SampleFrames+=block.Length/Math.Max(1,channels);
            lock(gate)pool.Enqueue(block);
        }
    }
    void Header(uint size)
    {
        int count=Math.Max(1,channels);writer.Write(0x46464952u);writer.Write(size+36);writer.Write(0x45564157u);writer.Write(0x20746d66u);writer.Write(16u);
        writer.Write((ushort)3);writer.Write((ushort)count);writer.Write(Rate);writer.Write(Rate*count*4);writer.Write((ushort)(count*4));writer.Write((ushort)32);writer.Write(0x61746164u);writer.Write(size);
    }
    public void StopCapture()
    {
        lock(gate)recording=false;if(writer==null)return;
        Flush();writer.BaseStream.Position=0;Header(checked((uint)(SampleFrames*Math.Max(1,channels)*4)));writer.Dispose();writer=null;
    }
    void OnDestroy(){StopCapture();}
}
