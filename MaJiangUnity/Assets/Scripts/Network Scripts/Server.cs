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
    /// ��������ӻ����ֵ�,�������½�����ʱ,��������Ӻ����Ӧ�Ļ���,Ĭ�ϴ�С8KiB
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
        // ��������ʱ���÷���
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
            // �����������,����һֱ���в������������д�������
            TcpClient connectedClient = await MainServer.AcceptTcpClientAsync();
            // [TODO] ��Ϊ�첽��ԭ��,�����Ѿ�ָ��ֹͣ����Ҳ����ֹͣAcceptTcpClientAsync(),Ҳ����ֱ�ӹر�Server
            // [TODO] ��˵���ʵ����ֹͣ����ʱ,�����н�������Ҳֱ�ӹر�,������Ӵ���
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
        // ��ΪRead��ȡ�ĸ�д�ԺͶ�ȡʱ���ܵĲ�������,Ŀǰͨ��ѭ����ȡ,ƴ��д�봦��ķ�ʽ��ȡ����
        // д����������Ψһ�����ǳ�ʱ���ڴ�������ݳ������������Ѿ���������ĵز�,��ʱ���̹ر�����
        while (true)
        {
            int startPointer = 0;
            int length = 0;
            // ÿ��ReadAsync���������ж�ȡ����,�����ȡ���512�ֽڵ�����,����֤��������ͬһʱ����ൽ512�ֽڵ�����,�ҵ������㹻512�ֽ�ʱ���д���
            length += await tcpClient.GetStream().ReadAsync(ClientBufferDictionary[tcpClient],length + startPointer,512 - length);
            if (length >= 512)
            {
                Debug.Log("Data arrived!");
                length = 0;

            }
        }
    }
}
