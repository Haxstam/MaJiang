using MaJiangLib;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class Server : MonoBehaviour
{
    public TcpListener MainServer { get; set; }
    /// <summary>
    /// 服务端连接缓存字典,当存在新建连接时,将添加连接和其对应的缓存,默认大小8KiB
    /// </summary>
    public Dictionary<TcpClient, byte[]> ClientBufferDictionary { get; set; }
    public bool IsListening { get; set; }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.P))
        {
            StartServer("127.0.0.1", 45008);
        }
    }

    public void StartServer(string IP, int Port)
    {
        IPAddress iPAddress = IPAddress.Parse(IP);
        MainServer = new TcpListener(iPAddress, Port);
        MainServer.Start();
        Debug.Log("MainServer Started!");
    }
    public void StartListen()
    {
        // 启动监听时调用方法
        ListenClientConnectAsync();
        IsListening = true;
    }
    public void StopListen()
    {
        IsListening = false;
    }
    async public void ListenClientConnectAsync()
    {
        while (IsListening)
        {
            // 如果启动监听,将会一直运行并持续监听所有传入连接
            TcpClient connectedClient = await MainServer.AcceptTcpClientAsync();
            // [TODO] 因为异步的原因,即便已经指定停止监听也不能停止AcceptTcpClientAsync(),也不能直接关闭Server
            // [TODO] 因此当事实上已停止监听时,即便有接入连接也直接关闭,考虑添加处理
            if (!IsListening)
            {
                connectedClient.Close();
                break;
            }
            ClientBufferDictionary[connectedClient] = new byte[8192];
            SubReadAsync(connectedClient);
            Debug.Log("Server Connected!");
        }
    }
    async public void SubReadAsync(TcpClient tcpClient)
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        // 写爆缓冲区的唯一可能是长时间内传输的数据持续不完整且已经到了溢出的地步,此时立刻关闭连接
        while (true)
        {
            int startPointer = 0;
            int length = 0;
            // 每当ReadAsync从数据流中读取数据,都会读取最多512字节的数据,并保证缓冲区中同一时间最多到512字节的数据,且当数据足够512字节时进行处理
            length += await tcpClient.GetStream().ReadAsync(ClientBufferDictionary[tcpClient],length + startPointer,512 - length);
            if (length >= 512)
            {
                Debug.Log("Data arrived!");
                length = 0;

            }
        }
    }
}
