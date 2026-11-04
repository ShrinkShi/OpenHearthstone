using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public sealed class Connection : IDisposable
{
    public readonly ConcurrentQueue<string> Incoming = new ConcurrentQueue<string>();
    TcpClient client;
    StreamWriter writer;
    readonly object gate = new object();
    volatile bool closing;
    public bool Connected { get { return client != null && client.Connected && !closing; } }

    public void Connect(string host, int port)
    {
        new Thread(() => {
            try
            {
                client = new TcpClient();
                var connect=client.ConnectAsync(host,port);
                if(!connect.Wait(TimeSpan.FromSeconds(5)))throw new IOException("连接超时，请检查服务器 IP 和端口");
                if(closing){client.Close();return;}
                client.NoDelay = true;
                writer = new StreamWriter(client.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
                var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                while (!closing)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Length > 1048576) throw new IOException("Server message too large");
                    Incoming.Enqueue(line);
                }
                if (!closing) Incoming.Enqueue("{\"type\":\"disconnected\",\"message\":\"服务器连接已断开\"}");
            }
            catch (Exception e)
            {
                if (!closing) Incoming.Enqueue("{\"type\":\"disconnected\",\"message\":" + JsonString(e.GetBaseException().Message) + "}");
            }
            finally
            {
                if(client!=null)client.Close();
            }
        }) {IsBackground = true, Name = "AuthoritativeConnection"}.Start();
    }

    // 连接线程可能晚于Unity引擎退出；此处不能调用引擎序列化API。
    static string JsonString(string value)
    {
        var result=new StringBuilder("\"");
        foreach(char c in value)
        {
            if(c=='"'||c=='\\')result.Append('\\').Append(c);
            else if(c<32)result.Append("\\u").Append(((int)c).ToString("x4"));
            else result.Append(c);
        }
        return result.Append('"').ToString();
    }

    public void Send(string json)
    {
        lock (gate)
        {
            if (writer == null || closing) throw new IOException("连接尚未就绪");
            writer.WriteLine(json);
        }
    }

    public void Dispose()
    {
        closing = true;
        if (client != null) client.Close();
    }
}
