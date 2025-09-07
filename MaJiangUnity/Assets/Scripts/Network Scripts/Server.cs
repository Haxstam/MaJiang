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
    /// ��������ӻ����ֵ�,�������½�����ʱ,��������Ӻ����Ӧ�Ļ���,Ĭ�ϴ�С8KiB
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
        // ��������ʱ���÷���
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
            // �����������,����һֱ���в������������д�������
            TcpClient connectedClient = await MainServer.AcceptTcpClientAsync();
            // [TODO] ��Ϊ�첽��ԭ��,�����Ѿ�ָ��ֹͣ����Ҳ����ֹͣAcceptTcpClientAsync(),Ҳ����ֱ�ӹر�Server
            // [TODO] ��˵���ʵ����ֹͣ����ʱ,�����н�������Ҳֱ�ӹر�,������Ӵ���
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
        // ��ΪRead��ȡ�ĸ�д�ԺͶ�ȡʱ���ܵĲ�������,Ŀǰͨ��ѭ����ȡ,ƴ��д�봦��ķ�ʽ��ȡ����
        // д����������Ψһ�����ǳ�ʱ���ڴ�������ݳ������������Ѿ���������ĵز�,��ʱ���̹ر�����
        while (true)
        {
            int startPointer = 0;
            int length = 0;
            // ÿ��ReadAsync���������ж�ȡ����,�����ȡ���512�ֽڵ�����,����֤��������ͬһʱ����ൽ512�ֽڵ�����,�ҵ������㹻512�ֽ�ʱ���д���
            length += await tcpClient.GetStream().ReadAsync(ClientBufferDictionary[tcpClient], length + startPointer, 512 - length);
            if (length >= 512)
            {
                Debug.Log("Data arrived!");
                byte[] bytes = new byte[512];
                Array.Copy(ClientBufferDictionary[tcpClient], 0, bytes, 0, 512);
                bool isTrue = PackCoder.PlayerActionDecode(bytes, out PlayerActionData playerActionData);
                Debug.Log(isTrue);
                length = 0;
            }
        }
    }
}
