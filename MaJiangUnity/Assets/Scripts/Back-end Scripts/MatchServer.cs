using MaJiangLib;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>Unity房主入口。房间业务在Update中串行处理，网络任务不访问Unity对象。</summary>
public class MatchServer : MonoBehaviour
{
    public string testName;
    public string testIPAddress;
    public string listenIPAddress = "0.0.0.0";
    public int port = 11111;
    public TextMeshProUGUI TextMeshPro;
    public LanRoomServer Server { get; private set; }
    public MainMatchControl MatchControl => Server?.Match?.AuthoritativeState;
    public bool IsListening => Server?.IsRunning == true;
    private Task stopping = Task.CompletedTask;

    private void Awake() => StartServer(listenIPAddress, port);

    public void StartServer(string address, int port) => StartServer(IPAddress.Parse(address), port);

    public void StartServer(IPAddress address, int port)
    {
        if (IsListening || !stopping.IsCompleted) { return; }
        Server = new LanRoomServer();
        Server.MatchStarted += snapshot =>
        {
            Debug.Log("四人房间已开始对局，服务端牌局初始化完成。");
        };
        try
        {
            Server.Start(address, port);
            Debug.Log($"局域网服务端监听 {address}:{Server.Port}");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"服务端启动失败：{exception.Message}");
        }
    }

    private void Update()
    {
        Server?.Pump();
        if (TextMeshPro != null && Server?.Room != null)
        {
            RoomSnapshot snapshot = Server.Room.Snapshot();
            TextMeshPro.text = $"端口 {Server.Port}\n{snapshot.Phase}\n玩家 {snapshot.Members.Count}/4";
        }
    }

    public Task StopServerAsync()
    {
        stopping = Server?.StopAsync() ?? Task.CompletedTask;
        return stopping;
    }
    public void StopServer() => _ = StopServerAsync();
    private void OnDestroy() => _ = StopServerAsync();
}
