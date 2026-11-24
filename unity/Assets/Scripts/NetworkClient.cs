using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
    public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

    public event Action<string, JObject> OnMessage;

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly ConcurrentQueue<(string type, JObject data)> _inbox = new ConcurrentQueue<(string, JObject)>();
    private string _pendingError;

    public void Connect(string url)
    {
        Disconnect();
        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        _ = RunConnect(url, _cts.Token);
    }

    private async Task RunConnect(string url, CancellationToken ct)
    {
        try
        {
            await _ws.ConnectAsync(new Uri(url), ct);
            _ = ReceiveLoop(ct);
        }
        catch (Exception e)
        {
            _pendingError = "connect failed: " + e.Message;
            _ws = null;
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        try { _ws?.Dispose(); } catch { }
        _ws = null;
    }

    public void Send(string type, JObject data = null)
    {
        if (!IsConnected)
        {
            _pendingError = "not connected";
            return;
        }
        var obj = new JObject { ["type"] = type, ["data"] = data ?? new JObject() };
        var bytes = Encoding.UTF8.GetBytes(obj.ToString(Newtonsoft.Json.Formatting.None));
        try
        {
            _ = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception e)
        {
            _pendingError = "send failed: " + e.Message;
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested && _ws != null)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    var json = sb.ToString();
                    sb.Clear();
                    try
                    {
                        var o = JObject.Parse(json);
                        var type = (string)o["type"] ?? "";
                        var data = o["data"] as JObject ?? new JObject();
                        _inbox.Enqueue((type, data));
                    }
                    catch (Exception e)
                    {
                        _pendingError = "bad json: " + e.Message;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            _pendingError = "receive: " + e.Message;
        }
        _ws = null;
    }

    private void Update()
    {
        if (_pendingError != null)
        {
            Debug.LogWarning("[Net] " + _pendingError);
            _pendingError = null;
        }
        while (_inbox.TryDequeue(out var m))
        {
            OnMessage?.Invoke(m.type, m.data);
        }
    }
}
