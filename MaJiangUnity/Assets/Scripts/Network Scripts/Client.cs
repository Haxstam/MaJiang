using MaJiangLib;
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
    // Update is called once per frame
    void Update()
    {
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
                new() { new(MaJiangLib.Color.Bamboo, 3) },
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
    public async void TimeOutConnectAsync(string IP, int Port)
    {
        IPAddress IPAddress = IPAddress.Parse(IP);
        // ��ʱTask,���10s����δ���ӳɹ�,����false
        Task timeoutTask = Task.Delay(10000);
        Task connectTask = MainClient.ConnectAsync(IPAddress, Port);
        Task finishedTask = await Task.WhenAny(timeoutTask, connectTask);
        if (finishedTask == connectTask)
        {
            Debug.Log("Client Connected!");
            ClientStream = MainClient.GetStream();

            //return true;
        }
        else
        {
            // [TODO] Ŀǰ�趨�ǵ����ﳬʱʱ��/������������ʱ,ֱ�ӹر�MainClient,�Ӷ���ֹConnect,������������
            // [TODO] ���ٴ����Ӿͱ�������Client
            MainClient.Close();
            Debug.Log("TimeOut!");
            //return false;
        }
    }
}
