using GizmoApp.Models;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GizmoApp.Service
{
    
    public class HomeAssistService
    {
        private readonly Uri _uri;
        private readonly string _token;
        private ClientWebSocket _socket = new();
        private CancellationTokenSource _cts = new();

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnStatusChanged;
        public event Action<string>? OnError;

        public bool IsConnected => _socket?.State == WebSocketState.Open;

        public HomeAssistService(AppConfig config)
        {
            _uri = new Uri(config.BaseUrl + "/api/websocket");
            _token = config.Token;
        }

        //Connect to Homeassist
        public async Task ConnectAsync()
        {
            try
            {
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(_uri, CancellationToken.None);
                OnStatusChanged?.Invoke("Verbunden. Authentifiziere...");

                await AuthenticateAsync();
                OnStatusChanged?.Invoke("✅ Authentifiziert");

                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"❌ Verbindung fehlgeschlagen: {ex.Message}");
                await ReconnectAsync();
            }
        }

        public async Task EnsureConnectedAsync()
        {
            if (_socket.State == WebSocketState.Open) return;

            try
            {
                OnStatusChanged?.Invoke("🔄 Stelle Verbindung wieder her...");
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"❌ Reconnect fehlgeschlagen: {ex.Message}");
            }
        }

        //authenticate
        private async Task AuthenticateAsync()
        {
            var buffer = new byte[4096];
            var result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (!json.Contains("auth_required"))
                throw new Exception("Auth nicht erwartet.");

            var authPayload = JsonSerializer.Serialize(new
            {
                type = "auth",
                access_token = _token
            });

            await SendAsync(authPayload);

            result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
            json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (!json.Contains("auth_ok"))
                throw new Exception("Auth fehlgeschlagen.");
        }
        //listener loop
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (_socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        OnStatusChanged?.Invoke("⚠️ Verbindung getrennt");
                        throw new Exception("Socket geschlossen.");
                    }
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessageReceived?.Invoke(json);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"⚠️ Listener-Fehler: {ex.Message}");
                await ReconnectAsync();
            }
        }
        //Reconnect
        private async Task ReconnectAsync()
        {
            OnStatusChanged?.Invoke("🔄 Verbindung verloren, versuche Reconnect...");
            await Task.Delay(3000);
            await ConnectAsync();
        }
        //send message
        public async Task SendAsync(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        //Disconnect
        public async Task DisconnectAsync()
        {
            _cts.Cancel();

            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);

            OnStatusChanged?.Invoke("❎ Getrennt");
        }
    }
}
