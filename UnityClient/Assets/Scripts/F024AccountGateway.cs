using System;
using System.Collections.Generic;
using System.IO;

public sealed class F024AccountGateway : IDisposable
{
    sealed class PendingRequest
    {
        public string requestId, operation, json;
    }

    readonly Queue<PendingRequest> waiting = new Queue<PendingRequest>();
    readonly Dictionary<string, PendingRequest> pending = new Dictionary<string, PendingRequest>();
    Connection connection;
    bool ready, disposed, failed;

    public event Action<F024AccountResponse> ResponseReceived;
    public event Action<string> ConnectionFailed;
    public bool IsReady { get { return ready && connection != null && connection.Connected; } }
    public bool IsFailed { get { return failed; } }

    public void Connect(string host, int port)
    {
        if (disposed) throw new ObjectDisposedException("F024AccountGateway");
        CloseConnection();
        failed = false;
        connection = new Connection();
        connection.Connect(host, port);
    }

    public void Send(string requestId, string operation, string json)
    {
        if (disposed) throw new ObjectDisposedException("F024AccountGateway");
        if (String.IsNullOrEmpty(requestId) || String.IsNullOrEmpty(operation) || String.IsNullOrEmpty(json))
            throw new ArgumentException("F024 account request is incomplete");
        var request = new PendingRequest { requestId = requestId, operation = operation, json = json };
        if (waiting.Count > 0 || !IsReady) waiting.Enqueue(request);
        else SendNow(request);
    }

    public void Pump()
    {
        if (disposed || connection == null) return;
        string line;
        while (connection.Incoming.TryDequeue(out line))
        {
            F024AccountResponse response;
            try { response = UnityEngine.JsonUtility.FromJson<F024AccountResponse>(line); }
            catch (Exception error) { Fail("账号服务响应无法解析：" + error.Message); continue; }
            if (response == null) continue;
            if (response.type == "hello")
            {
                var hello = UnityEngine.JsonUtility.FromJson<F024AccountHello>(line);
                if (hello == null || hello.capabilities == null || Array.IndexOf(hello.capabilities, "accounts_v1") < 0)
                {
                    Fail("服务器未启用账号服务（accounts_v1）");
                    CloseConnection();
                    break;
                }
                ready = true;
                FlushWaiting();
            }
            else if (response.type == "account_result")
            {
                pending.Remove(response.request_id ?? "");
                var handler = ResponseReceived;
                if (handler != null) handler(response);
            }
            else if (response.type == "error")
            {
                PendingRequest item;
                if (!String.IsNullOrEmpty(response.request_id) && pending.TryGetValue(response.request_id, out item))
                {
                    pending.Remove(response.request_id);
                    response.operation = item.operation;
                    response.ok = false;
                    response.error = String.IsNullOrEmpty(response.error) ? response.message : response.error;
                    var handler = ResponseReceived;
                    if (handler != null) handler(response);
                }
                else Fail(String.IsNullOrEmpty(response.message) ? "账号服务返回错误" : response.message);
            }
            else if (response.type == "disconnected")
            {
                Fail(String.IsNullOrEmpty(response.message) ? "账号服务连接已断开" : response.message);
                CloseConnection();
                break;
            }
        }
    }

    void FlushWaiting()
    {
        while (ready && waiting.Count > 0)
        {
            var request = waiting.Dequeue();
            SendNow(request);
        }
    }

    void SendNow(PendingRequest request)
    {
        try
        {
            pending[request.requestId] = new PendingRequest { requestId = request.requestId, operation = request.operation };
            connection.Send(request.json);
            request.json = null;
        }
        catch (Exception error)
        {
            pending.Remove(request.requestId);
            Fail(error.Message);
        }
    }

    void Fail(string message)
    {
        ready = false;
        failed = true;
        waiting.Clear();
        pending.Clear();
        var handler = ConnectionFailed;
        if (handler != null) handler(message);
    }

    void CloseConnection()
    {
        ready = false;
        if (connection != null) connection.Dispose();
        connection = null;
        waiting.Clear();
        pending.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CloseConnection();
    }
}
