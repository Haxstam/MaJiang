using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

public class Server : MonoBehaviour
{
    /// <summary>
    /// 用户端连接数据类,包含用户端,延迟,对应玩家座次
    /// </summary>
    public class ClientData
    {
        public ClientData(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
            ClientStream = TcpClient.GetStream();
            Ping = 0;
            Closing = false;
        }
        public TcpClient TcpClient { get; set; }
        public NetworkStream ClientStream { get; set; }
        public double Ping { get; set; }
        public int RelativePlayerNumber { get; set; }
        /// <summary>
        /// 连接将要被关闭时为true
        /// </summary>
        public bool Closing { get; set; }
    }
    public TcpListener MainServer { get; set; }
    /// <summary>
    /// 服务端连接缓存字典,当存在新建连接时,将添加连接信息和其对应的缓存,默认大小8KiB
    /// </summary>
    public Dictionary<ClientData, byte[]> ClientBufferDictionary { get; set; }
    public bool IsListening { get; set; }
    /// <summary>
    /// 连接限制,连接数最大为该值,大于该值会拒绝连接
    /// </summary>
    public int ConnectLimit { get; set; }
    public int ConnectCount { get { return ClientBufferDictionary.Count; } }
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
    public void StopConnection()
    {

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
            else
            {
                if (ConnectCount >= ConnectLimit)
                {
                    // [TODO] 连接数过多
                    connectedClient.Close();
                }
                if (connectedClient != null)
                {
                    // 连接创建后,分配数据缓存并激活读取方法
                    ClientData clientData = new(connectedClient);
                    ClientBufferDictionary[clientData] = new byte[8192];
                    SubReadAsync(clientData);
                }
            }

        }
    }
    /// <summary>
    /// 单连接读取方法
    /// </summary>
    /// <param name="tcpClient"></param>
    public async void SubReadAsync(ClientData clientData)
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        byte[] clientBuffer = ClientBufferDictionary[clientData];

        // 表示当前将要写入缓存的位置
        int writeIndex = 0;
        // 读取循环,如果连接没有被关闭就保持读取
        while (!clientData.Closing)
        {
            // 读取循环
            // 每当ReadAsync从数据流中读取数据,都会读取最多1024字节的数据,如果写入指针接近上限,则缩小写入大小
            try
            {
                writeIndex += await clientData.ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
            }
            catch (ObjectDisposedException)
            {
                Debug.Log($"尝试读取已关闭的流:{((IPEndPoint)clientData.TcpClient.Client.RemoteEndPoint).Address}");
            }
            if (writeIndex >= 1024)
            {   // 如果存在不少于1024字节的待处理数据,进行处理
                bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex, clientData);
            }
        }
    }
    /// <summary>
    /// 通过Span匹配字节串的方法,需要主字节串和短字节串
    /// </summary>
    /// <param name="data"></param>
    /// <param name="shortData"></param>
    /// <returns></returns>
    public int MatchBytes(byte[] mainBytes, byte[] shortBytes)
    {
        Span<byte> SpanBytes = mainBytes;
        ReadOnlySpan<byte> ShortSpanBytes = shortBytes;
        return SpanBytes.IndexOf(ShortSpanBytes);
    }
    /// <summary>
    /// 数据缓存处理,在方法内部根据判断修改buffer和写入指针
    /// </summary>
    /// <param name="buffer">对应连接的接收缓存</param>
    /// <param name="packBytes">处理后的包</param>
    /// <param name="writeIndex">当前写入位置</param>
    /// <returns>当本次处理获取到包时,返回True,如果无数据或无合法包,返回False</returns>
    public bool ReadByteSolve(byte[] buffer, ref int writeIndex, ClientData clientData)
    {
        Span<byte> spanBuffer = buffer.AsSpan();
        bool isReceivePack = false;
        while (true)
        {
            // 处理循环
            // 循环判断直到没有任何合法包
            int endIndex = MatchBytes(buffer, PackCoder.PackEndBytes);
            if (endIndex == -1)
            {
                // 未获取到包尾,返回false
                return isReceivePack;
            }
            else if (endIndex >= 0 && endIndex < 1024)
            {
                // 获取到包尾,但位置小于1024,即为残缺的后半个包,修改该标记首位为0x00并将此包尾之后的数据前移
                buffer[endIndex] = 0x00;
                spanBuffer.Slice(endIndex + 8, writeIndex - (endIndex + 8)).CopyTo(spanBuffer);
                writeIndex -= endIndex;
            }
            else
            {
                // 获取到包尾且位置不小于1024,进行包合法性判断
                Span<byte> rawPack = spanBuffer.Slice(endIndex - 1016, 1024);
                if (PackCoder.IsLegalPack(rawPack))
                {
                    // 判断成功,标记返回True和所提取的包
                    byte[] packBytes = rawPack.ToArray();
                    // 记录延迟,仅此处可以更改连接延迟
                    clientData.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

                    isReceivePack = true;
                }
                else
                {
                    // 判断失败
                }
                // 无论成功与否,都修改标记首位为0x00并将此包尾之后的数据前移
                buffer[endIndex] = 0x00;
                spanBuffer.Slice(endIndex + 8, writeIndex - (endIndex + 8)).CopyTo(spanBuffer);
                writeIndex -= 1024;
            }
        }
    }
}
