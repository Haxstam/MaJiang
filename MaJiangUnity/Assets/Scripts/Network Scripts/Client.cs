using MaJiangLib;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Color = MaJiangLib.Color;
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
        MainMatchControl mainMatchControl = new MainMatchControl(new List<Player>(),MaJiangLib.MatchType.FourMahjongSouth, true,true);
        mainMatchControl.PlayerFuluList = new()
        {
            new(){
                new Group(GroupType.AnKang,
                Color.Bamboo,
                new()
                {
                    new (Color.Bamboo,1),
                    new (Color.Bamboo,1),
                    new (Color.Bamboo,1)
                },
                1,
                new(Color.Bamboo, 1)
                )
            }
        };
        mainMatchControl.QiPaiList = new()
        {
            new()
            {
                new(Color.Wans,2),
                new(Color.Honor,4),
            },
            new()
            {
                new(Color.Wans,3),
                new(Color.Tungs,5),
            }
            ,
            new()
            {
                new(Color.Tungs,6),
                new(Color.Tungs,7),
            },
            new()
            {
                new(Color.Wans,3),
                new(Color.Honor,4),
            },

        };
        mainMatchControl.Round = 3;
        mainMatchControl.Honba = 3;
        mainMatchControl.MatchType = MaJiangLib.MatchType.FourMahjongEast;
        mainMatchControl.KangCount = 0;
        mainMatchControl.RemainPaiCount = 44;

        mainMatchControl.IsRiichi = new() { false, false, false, false };
        mainMatchControl.IsDoubleRiichi = new() { false, false, false, false };
        mainMatchControl.IsKang = new() { true, false, false, false };
        mainMatchControl.HaveIppatsu = new() { true, false, false, false };

        mainMatchControl.DoraList = new() { new(Color.Tungs, 4) };
        mainMatchControl.UraDoraList = new();
        testBytes = mainMatchControl.GetPublicBytes();
        pubControl = MainMatchControl.PublicBytesTo(testBytes);
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
