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
    /// �û�������������,�����û���,�ӳ�,��Ӧ�������
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
        /// ���ӽ�Ҫ���ر�ʱΪtrue
        /// </summary>
        public bool Closing { get; set; }
    }
    public TcpListener MainServer { get; set; }
    /// <summary>
    /// ��������ӻ����ֵ�,�������½�����ʱ,�����������Ϣ�����Ӧ�Ļ���,Ĭ�ϴ�С8KiB
    /// </summary>
    public Dictionary<ClientData, byte[]> ClientBufferDictionary { get; set; }
    public bool IsListening { get; set; }
    /// <summary>
    /// ��������,���������Ϊ��ֵ,���ڸ�ֵ��ܾ�����
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
        // ��������ʱ���÷���
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
            // �����������,����һֱ���в������������д�������
            TcpClient connectedClient = await MainServer.AcceptTcpClientAsync();
            // [TODO] ��Ϊ�첽��ԭ��,�����Ѿ�ָ��ֹͣ����Ҳ����ֹͣAcceptTcpClientAsync(),Ҳ����ֱ�ӹر�Server
            // [TODO] ��˵���ʵ����ֹͣ����ʱ,�����н�������Ҳֱ�ӹر�,������Ӵ���
            if (!IsListening)
            {
                connectedClient.Close();
                break;
            }
            else
            {
                if (ConnectCount >= ConnectLimit)
                {
                    // [TODO] ����������
                    connectedClient.Close();
                }
                if (connectedClient != null)
                {
                    // ���Ӵ�����,�������ݻ��沢�����ȡ����
                    ClientData clientData = new(connectedClient);
                    ClientBufferDictionary[clientData] = new byte[8192];
                    SubReadAsync(clientData);
                }
            }

        }
    }
    /// <summary>
    /// �����Ӷ�ȡ����
    /// </summary>
    /// <param name="tcpClient"></param>
    public async void SubReadAsync(ClientData clientData)
    {
        // ��ΪRead��ȡ�ĸ�д�ԺͶ�ȡʱ���ܵĲ�������,Ŀǰͨ��ѭ����ȡ,ƴ��д�봦��ķ�ʽ��ȡ����
        byte[] clientBuffer = ClientBufferDictionary[clientData];

        // ��ʾ��ǰ��Ҫд�뻺���λ��
        int writeIndex = 0;
        // ��ȡѭ��,�������û�б��رվͱ��ֶ�ȡ
        while (!clientData.Closing)
        {
            // ��ȡѭ��
            // ÿ��ReadAsync���������ж�ȡ����,�����ȡ���1024�ֽڵ�����,���д��ָ��ӽ�����,����Сд���С
            try
            {
                writeIndex += await clientData.ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
            }
            catch (ObjectDisposedException)
            {
                Debug.Log($"���Զ�ȡ�ѹرյ���:{((IPEndPoint)clientData.TcpClient.Client.RemoteEndPoint).Address}");
            }
            if (writeIndex >= 1024)
            {   // ������ڲ�����1024�ֽڵĴ���������,���д���
                bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex, clientData);
            }
        }
    }
    /// <summary>
    /// ͨ��Spanƥ���ֽڴ��ķ���,��Ҫ���ֽڴ��Ͷ��ֽڴ�
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
    /// ���ݻ��洦��,�ڷ����ڲ������ж��޸�buffer��д��ָ��
    /// </summary>
    /// <param name="buffer">��Ӧ���ӵĽ��ջ���</param>
    /// <param name="packBytes">�����İ�</param>
    /// <param name="writeIndex">��ǰд��λ��</param>
    /// <returns>�����δ����ȡ����ʱ,����True,��������ݻ��޺Ϸ���,����False</returns>
    public bool ReadByteSolve(byte[] buffer, ref int writeIndex, ClientData clientData)
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
                    // ��¼�ӳ�,���˴����Ը��������ӳ�
                    clientData.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

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
