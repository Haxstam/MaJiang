using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server : MonoBehaviour
{
    public TcpListener MainServer { get; set; }
    /// <summary>
    /// 服务端连接缓存字典,当存在新建连接时,将添加连接和其对应的缓存,默认大小8KiB
    /// </summary>
    public Dictionary<TcpClient, byte[]> ClientBufferDictionary { get; set; }
    public bool IsListening { get; set; }

    TcpClient Client { get; set; }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.P))
        {
            StartServer("127.0.0.1", 23456);
        }
        if (Input.GetKeyUp(KeyCode.Q))
        {
            Debug.Log(Client.GetStream().DataAvailable);
        }
    }

    public void StartServer(string IP, int Port)
    {
        IPAddress iPAddress = IPAddress.Parse(IP);
        MainServer = new TcpListener(iPAddress, Port);
        ClientBufferDictionary = new();
        MainServer.Start();
        Debug.Log("MainServer Started!");
        StartListen();
    }
    public void StartListen()
    {
        IsListening = true;
        // 启动监听时调用方法
        ListenClientConnectAsync();

    }
    public void StopListen()
    {
        IsListening = false;
    }
    public async void ListenClientConnectAsync()
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
            if (connectedClient != null)
            {
                Debug.Log("Server Connected!");
                Client = connectedClient;
                ClientBufferDictionary[connectedClient] = new byte[8192];
                SubReadAsync(connectedClient);
            }
        }
    }
    public async void SubReadAsync(TcpClient tcpClient)
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        // 写爆缓冲区的唯一可能是长时间内传输的数据持续不完整且已经到了溢出的地步,此时立刻关闭连接

        // 表示当前数据起始位置,即最后端匹配的"HaxMajPk"字节串
        int startPointer = 0;
        // 表示当前将要写入缓存的位置
        int length = 0;
        while (true)
        {
            
            // 每当ReadAsync从数据流中读取数据,都会读取最多1024字节的数据,并保证缓冲区中同一时间最多到1024字节的有用数据,且当数据足够1024字节时进行处理
            // 如果存在
            length += await tcpClient.GetStream().ReadAsync(ClientBufferDictionary[tcpClient], length + startPointer, 1024 - length);
            if (length >= 1024)
            {
                Debug.Log($"Package Arrived:{DateTime.Now}");
                byte[] bytes = new byte[1024];
                Array.Copy(ClientBufferDictionary[tcpClient], 0, bytes, 0, 1024);
                bool isTrue = PackCoder.PlayerActionDecode(bytes, out PlayerActionData playerActionData);
                Debug.Log(isTrue);
                length = 0;
            }
        }
    }
    public static int MatchBytes(byte[] data, byte[] shortData)
    {
        Span<byte> buffer = data;
        ReadOnlySpan<byte> mB = shortData;
        return buffer.IndexOf(mB);
    }
}
