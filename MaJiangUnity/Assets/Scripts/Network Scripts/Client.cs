using MaJiangLib;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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
    private byte[] ClientBuffer { get; set; } = new byte[8192];
    /// <summary>
    /// 客户端TCP数据流
    /// </summary>
    private NetworkStream ClientStream { get; set; }

    // Start is called before the first frame update
    void Start()
    {

    }

    public static byte[] testBytes;

    public static IMatchInformation pubControl;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.B))
        {
            Debug.Log(pubControl.RemainPaiCount);
        }
        if (Input.GetKeyUp(KeyCode.L))
        {
            TimeOutConnectAsync("127.0.0.1", 23456);
        }
        if (Input.GetKeyUp(KeyCode.M))
        {
            byte[] bytes = PackCoder.EmptyPack("Haxstam", IPAddress.Parse("127.0.0.1"));
            byte[] testData = PackCoder.PlayerActionPack(bytes, new
                (
                new() { new(MaJiangLib.Color.Bamboo, 4), new(MaJiangLib.Color.Bamboo, 5, true) },
                new(MaJiangLib.Color.Bamboo, 3),
                PlayerAction.Chi
                )
                );
            ClientStream.Write(testData);
        }
        if (Input.GetKeyUp(KeyCode.S))
        {
            Debug.Log(ClientStream);
            Debug.Log(MainClient.Connected);
        }
    }


    /// <summary>
    /// 客户端进行连接的方法,仅在调用此方法并成功连接后才可进行传输,若连接超时则返回false
    /// </summary>
    /// <param name="IP">要连接的IP地址,需要字符串</param>
    /// <param name="Port">所连接的端口</param>
    /// <returns></returns>
    public bool TimeOutConnectAsync(IPAddress IPAddress, int Port)
    {
        // 超时Task,如果10s后仍未连接成功,返回false
        Task timeoutTask = Task.Delay(10000);
        Task connectTask = MainClient.ConnectAsync(IPAddress, Port);
        Task finishedTask = Task.WhenAny(timeoutTask, connectTask);
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
    public bool TimeOutConnectAsync(string IP, int Port) => TimeOutConnectAsync(IPAddress.Parse(IP), Port);
    public async void SubReadAsync()
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        byte[] clientBuffer = ClientBuffer;

        // 表示当前将要写入缓存的位置
        int writeIndex = 0;
        // 读取循环
        while (true)
        {
            // 读取循环
            // 每当ReadAsync从数据流中读取数据,都会读取最多1024字节的数据,如果写入指针接近上限,则缩小写入大小
            writeIndex += await ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
            if (writeIndex >= 1024)
            {   // 如果存在不少于1024字节的待处理数据,进行处理
                bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex);
            }
        }
    }
    /// <summary>
    /// 通过Span匹配字节串的方法,需要主字节串和短字节串
    /// </summary>
    /// <param name="data"></param>
    /// <param name="shortData"></param>
    /// <returns></returns>
    public static int MatchBytes(byte[] mainBytes, byte[] shortBytes)
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
    public static bool ReadByteSolve(byte[] buffer, ref int writeIndex)
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
                    PackCoder.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

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
