using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using StratagemDeck.Mobile.Models;

namespace StratagemDeck.Mobile.Services;

public class UdpDiscoveryService : IDisposable
{
    private readonly int _cmdPort;
    private CancellationTokenSource? _cts;
    private bool _isScanning;

    private int _sentCount;
    private int _responseCount;

    public event Action<DiscoveryInfo>? OnServerDiscovered;
    public event Action<string>? OnLog; // descriptive log for UI

    public UdpDiscoveryService(int cmdPort = 12345)
    {
        _cmdPort = cmdPort;
    }

    public void StartScanning()
    {
        if (_isScanning) return;
        _isScanning = true;
        _cts = new CancellationTokenSource();
        _ = ScanNetwork(_cts.Token);
    }

    public void StopScanning()
    {
        _isScanning = false;
        _cts?.Cancel();
    }

    public async Task<bool> PingServer(string ip, string pin)
    {
        try
        {
            var ping = new { type = "ping", pin };
            var json = JsonSerializer.Serialize(ping);
            var data = Encoding.UTF8.GetBytes(json);

            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 1000;
            await client.SendAsync(data, new IPEndPoint(IPAddress.Parse(ip), _cmdPort));

            var result = await client.ReceiveAsync();
            var response = Encoding.UTF8.GetString(result.Buffer);

            var ok = response.Contains("\"type\":\"pong\"");
            OnLog?.Invoke(ok ? "Connected" : "Invalid response");
            return ok;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke("Ping failed");
            return false;
        }
    }

    private async Task ScanNetwork(CancellationToken ct)
    {
        // Single client for both send and receive so server responses reach us
        using var client = new UdpClient(0);

        var discoverMsg = new { type = "discover" };
        var json = JsonSerializer.Serialize(discoverMsg);
        var data = Encoding.UTF8.GetBytes(json);

        while (!ct.IsCancellationRequested)
        {
            var subnets = GetLocalSubnets();
            if (subnets.Count == 0)
            {
                OnLog?.Invoke("No network");
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            _sentCount = 0;
            _responseCount = 0;

            foreach (var subnet in subnets)
            {
                if (ct.IsCancellationRequested) break;

                var sem = new SemaphoreSlim(20);
                var tasks = new List<Task>();

                for (int i = 1; i < 255; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    var ip = $"{subnet}.{i}";

                    await sem.WaitAsync(ct);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // Use same client so source port matches
                            await client.SendAsync(data, new IPEndPoint(IPAddress.Parse(ip), _cmdPort));
                            Interlocked.Increment(ref _sentCount);
                        }
                        catch { }
                        finally { sem.Release(); }
                    }, ct));
                }

                await Task.WhenAll(tasks);
            }

            OnLog?.Invoke("Scanning...");

            // Listen for responses with 2s timeout per receive
            var seenIps = new HashSet<string>();
            var listenUntil = DateTime.UtcNow.AddSeconds(3);

            while (DateTime.UtcNow < listenUntil && !ct.IsCancellationRequested)
            {
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(2000);

                    var result = await client.ReceiveAsync(timeoutCts.Token);
                    var response = Encoding.UTF8.GetString(result.Buffer);

                    if (!response.Contains("\"type\":\"discovery\"")) continue;

                    var msg = JsonSerializer.Deserialize<DiscoveryMessage>(response);
                    if (msg == null) continue;

                    var ip = result.RemoteEndPoint.Address.ToString();
                    if (!seenIps.Add(ip)) continue;

                    Interlocked.Increment(ref _responseCount);
                    OnLog?.Invoke($"Found {msg.pc}");
                    OnServerDiscovered?.Invoke(new DiscoveryInfo
                    {
                        PcName = msg.pc,
                        IpAddress = ip,
                        Pin = msg.pin
                    });
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { }
                catch { }
            }

            OnLog?.Invoke(_responseCount > 0 ? $"{_responseCount} server(s) found" : "No servers found");

            // Wait before next scan
            try { await Task.Delay(5000, ct); }
            catch (OperationCanceledException) { break; }
        }

        OnLog?.Invoke("Scan stopped");
    }

    private static List<string> GetLocalSubnets()
    {
        var subnets = new HashSet<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bytes = ip.Address.GetAddressBytes();
                if (bytes[0] == 127) continue;
                subnets.Add($"{bytes[0]}.{bytes[1]}.{bytes[2]}");
            }
        }
        return subnets.ToList();
    }

    public void Dispose()
    {
        StopScanning();
    }

    private class DiscoveryMessage
    {
        public string type { get; set; } = string.Empty;
        public string pc { get; set; } = string.Empty;
        public string pin { get; set; } = string.Empty;
    }
}
