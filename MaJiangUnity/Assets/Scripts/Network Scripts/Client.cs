using MaJiangLib;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;

public class Client : MonoBehaviour
{
    /// <summary>
    /// 用户进行连接时的客户端
    /// </summary>
    public TcpClient MainClient { get; set; } = new TcpClient();
    /// <summary>
    /// 客户端TCP连接缓存
    /// </summary>
    private byte[] ClientBuffer {  get; set; } = new byte[8192];
    /// <summary>
    /// 客户端TCP数据流
    /// </summary>
    private NetworkStream ClientStream { get; set; }


    // Start is called before the first frame update
    void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.L))
        {
            TimeOutConnectAsync("127.0.0.1",23456);
        }
        if (Input.GetKeyUp(KeyCode.M))
        {
            byte[] bytes = PackCoder.EmptyPack("Haxstam",IPAddress.Parse("127.0.0.1"));
            ClientStream.Write(bytes);
        }
    }

    /// <summary>
    /// 客户端进行连接的方法,仅在调用此方法并成功连接后才可进行传输,若连接超时则返回false
    /// </summary>
    /// <param name="IP">要连接的IP地址,需要字符串</param>
    /// <param name="Port">所连接的端口</param>
    /// <returns></returns>
    public async Task<bool> TimeOutConnectAsync(string IP, int Port)
    {
        IPAddress IPAddress = IPAddress.Parse(IP);
        // 超时Task,如果10s后仍未连接成功,返回false
        Task timeoutTask = Task.Delay(10000);
        Task connectTask =  MainClient.ConnectAsync(IPAddress, Port);
        Task finishedTask = await Task.WhenAny(timeoutTask, connectTask);
        if (finishedTask == connectTask)
        {
            Debug.Log("Client Connected!");
            ClientStream = MainClient.GetStream();
            return true;
        }
        else
        {
            // [TODO] 目前设定是当到达超时时间/出现连接问题时,直接关闭MainClient,从而终止Connect,避免意外连接
            // [TODO] 但再次连接就必须重启Client
            MainClient.Close();
            Debug.Log("TimeOut!");
            return false;
        }
    }
}
