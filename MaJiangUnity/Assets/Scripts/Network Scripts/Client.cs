using MaJiangLib;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
public class Client : MonoBehaviour
{
    /// <summary>
    /// �û���������ʱ�Ŀͻ���
    /// </summary>
    public TcpClient MainClient { get; set; } = new TcpClient();
    /// <summary>
    /// �ͻ���TCP���ӻ���
    /// </summary>
    private byte[] ClientBuffer { get; set; } = new byte[8192];
    /// <summary>
    /// �ͻ���TCP������
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
    /// �ͻ��˽������ӵķ���,���ڵ��ô˷������ɹ����Ӻ�ſɽ��д���,�����ӳ�ʱ�򷵻�false
    /// </summary>
    /// <param name="IP">Ҫ���ӵ�IP��ַ,��Ҫ�ַ���</param>
    /// <param name="Port">�����ӵĶ˿�</param>
    /// <returns></returns>
    public bool TimeOutConnectAsync(IPAddress IPAddress, int Port)
    {
        // ��ʱTask,���10s����δ���ӳɹ�,����false
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
            // [TODO] Ŀǰ�趨�ǵ����ﳬʱʱ��/������������ʱ,ֱ�ӹر�MainClient,�Ӷ���ֹConnect,������������
            // [TODO] ���ٴ����Ӿͱ�������Client
            MainClient.Close();
            Debug.Log("TimeOut!");
            return false;
        }
    }
    public bool TimeOutConnectAsync(string IP, int Port) => TimeOutConnectAsync(IPAddress.Parse(IP), Port);
    public async void SubReadAsync()
    {
        // ��ΪRead��ȡ�ĸ�д�ԺͶ�ȡʱ���ܵĲ�������,Ŀǰͨ��ѭ����ȡ,ƴ��д�봦��ķ�ʽ��ȡ����
        byte[] clientBuffer = ClientBuffer;

        // ��ʾ��ǰ��Ҫд�뻺���λ��
        int writeIndex = 0;
        // ��ȡѭ��
        while (true)
        {
            // ��ȡѭ��
            // ÿ��ReadAsync���������ж�ȡ����,�����ȡ���1024�ֽڵ�����,���д��ָ��ӽ�����,����Сд���С
            writeIndex += await ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
            if (writeIndex >= 1024)
            {   // ������ڲ�����1024�ֽڵĴ���������,���д���
                bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex);
            }
        }
    }
    /// <summary>
    /// ͨ��Spanƥ���ֽڴ��ķ���,��Ҫ���ֽڴ��Ͷ��ֽڴ�
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
    /// ���ݻ��洦��,�ڷ����ڲ������ж��޸�buffer��д��ָ��
    /// </summary>
    /// <param name="buffer">��Ӧ���ӵĽ��ջ���</param>
    /// <param name="packBytes">�����İ�</param>
    /// <param name="writeIndex">��ǰд��λ��</param>
    /// <returns>�����δ����ȡ����ʱ,����True,��������ݻ��޺Ϸ���,����False</returns>
    public static bool ReadByteSolve(byte[] buffer, ref int writeIndex)
    {
        Span<byte> spanBuffer = buffer.AsSpan();
        bool isReceivePack = false;
        while (true)
        {
            // ����ѭ��
            // ѭ���ж�ֱ��û���κκϷ���
            int endIndex = MatchBytes(buffer, PackCoder.PackEndBytes);
            if (endIndex == -1)
            {
                // δ��ȡ����β,����false
                return isReceivePack;
            }
            else if (endIndex >= 0 && endIndex < 1024)
            {
                // ��ȡ����β,��λ��С��1024,��Ϊ��ȱ�ĺ�����,�޸ĸñ����λΪ0x00�����˰�β֮�������ǰ��
                buffer[endIndex] = 0x00;
                spanBuffer.Slice(endIndex + 8, writeIndex - (endIndex + 8)).CopyTo(spanBuffer);
                writeIndex -= endIndex;
            }
            else
            {
                // ��ȡ����β��λ�ò�С��1024,���а��Ϸ����ж�
                Span<byte> rawPack = spanBuffer.Slice(endIndex - 1016, 1024);
                if (PackCoder.IsLegalPack(rawPack))
                {
                    // �жϳɹ�,��Ƿ���True������ȡ�İ�
                    byte[] packBytes = rawPack.ToArray();
                    PackCoder.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

                    isReceivePack = true;
                }
                else
                {
                    // �ж�ʧ��
                }
                // ���۳ɹ����,���޸ı����λΪ0x00�����˰�β֮�������ǰ��
                buffer[endIndex] = 0x00;
                spanBuffer.Slice(endIndex + 8, writeIndex - (endIndex + 8)).CopyTo(spanBuffer);
                writeIndex -= 1024;
            }
        }
    }
}
