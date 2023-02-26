using System.Buffers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Timer = System.Timers.Timer;

namespace SmartPlug;

public sealed class W115 : IDisposable
{
    private readonly string _deviceId;
    private readonly string _deviceToken;
    private readonly ClientWebSocket _socket;
    private readonly Timer _keepAliveTimer = new(30000);

    private W115(string deviceId, string deviceToken, ClientWebSocket socket)
    {
        _deviceId = deviceId;
        _deviceToken = deviceToken;
        _socket = socket;
        _keepAliveTimer.Elapsed += async (_, _) =>
        {
            string message = $$"""
            {
                "command":"keep_alive",
                "sequence_id":69420,
                "local_cid":69420,
                "timestamp":{{DateTimeOffset.Now.ToUnixTimeSeconds()}},
                "client_id":""
            }
            """;
            await Send(message);
            await Receive();
        };
    }

    public static async Task<W115> Connect(string ip, uint pin)
    {
        ClientWebSocket socket = new();
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await socket.ConnectAsync(new($"wss://{ip}:8080/SwitchCamera"), CancellationToken.None);
        
        string message = $$"""
            {
                "command":"sign_in",
                "scope":["user","device:status","device:control","viewing","photo","policy","client","event"],
                "sequence_id":69420,
                "timestamp":{{DateTimeOffset.Now.ToUnixTimeSeconds() + 1}},
                "client_id":""
            }
            """;
        await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, WebSocketMessageFlags.None,
            CancellationToken.None);
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        string response = Encoding.UTF8.GetString(buffer.AsSpan(..result.Count));
        
        JsonElement jsonDocument = JsonSerializer.Deserialize<JsonElement>(response)!;
        string salt = jsonDocument.GetProperty("salt").GetString()!;
        string deviceId = jsonDocument.GetProperty("device_id").GetString()!;
        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(pin + salt));
        string deviceToken = $"{deviceId}-{Convert.ToHexString(hash).ToLower()}";

        return new(deviceId, deviceToken, socket);
    }
    
    public async Task<bool> Get(SmartPlugSetting setting)
    {
        string message = $$"""
        {
            "command":"get_setting",
            "setting":[{"type":{{(int)setting}},"idx":0}],
            "sequence_id":69420,
            "local_cid":69420,
            "timestamp":{{DateTimeOffset.Now.ToUnixTimeSeconds()}},
            "client_id":"",
            "device_id":"{{_deviceId}}",
            "device_token":"{{_deviceToken}}"
        }
        """;
        await Send(message);
        string receive = await Receive();
        JsonElement root = JsonSerializer.Deserialize<JsonElement>(receive);
        return root
            .GetProperty("setting").EnumerateArray()
            .First().GetProperty("metadata")
            .GetProperty("value")
            .GetInt32() == 1;
    }

    public async Task Set(SmartPlugSetting setting, bool value)
    {
        string message = $$"""
        {
            "command":"set_setting",
            "setting":[{"uid":0,"metadata":{"value":{{(value ? 1 : 0)}}},"idx":0,"type":{{(int)setting}}}],
            "sequence_id":69420,
            "local_cid":69420,
            "timestamp":{{DateTimeOffset.Now.ToUnixTimeSeconds()}},
            "client_id":"",
            "device_id":"{{_deviceId}}",
            "device_token":"{{_deviceToken}}"
        }
        """;
        await Send(message);
    }

    private async Task Send(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.None, CancellationToken.None);
    }

    private async Task<string> Receive()
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        WebSocketReceiveResult result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
        string response = Encoding.UTF8.GetString(buffer.AsSpan(..result.Count));
        ArrayPool<byte>.Shared.Return(buffer);
        return response;
    }

    void IDisposable.Dispose()
    {
        _socket.Dispose();
        _keepAliveTimer.Dispose();
    }
}

public enum SmartPlugSetting
{
    State = 16,
    Light = 41
}