using MaJiangLib;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class MatchServer : MonoBehaviour
{
    private void Start()
    {

    }
    private void Update()
    {

    }
    /// <summary>
    /// 对局服务器的初始化,作为房主创建对局时必须先调用
    /// </summary>
    public void Init()
    {
        SelfServer = new();
    }
    /// <summary>
    /// 对局的初始化
    /// </summary>
    public void MatchInit()
    {

    }
    /// <summary>
    /// 从连接到对局顺位的映射表
    /// </summary>
    public TcpClient[] tcpClientNumberList = new TcpClient[4];
    /// <summary>
    /// 对局主服务器的连接用服务器
    /// </summary>
    public Server SelfServer { get; private set; }
    /// <summary>
    /// 对局控制类
    /// </summary>
    public MainMatchControl MatchControl { get; set; }
    /// <summary>
    /// 玩家可行操作的列表,每当有人打出时更新,响应/跳过后归零
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>>[] AvailableActionDict { get; private set; } = new Dictionary<PlayerAction, List<PlayerActionData>>[4];
    /// <summary>
    /// 玩家若听牌,其听牌的列表.考虑到摸切(即不改变听牌)占切牌的很大一部分,因此单独存储从而减少运算
    /// </summary>
    public Dictionary<Pai, List<Group>>[] AvailableTingPaiDict { get; private set; } = new Dictionary<Pai, List<Group>>[4];
    /// <summary>
    /// 玩家每步都有的等待时间,可以进行操作时重置为5s(默认)
    /// </summary>
    public float[] PlayerBaseTimeList { get; private set; } = new float[4];
    /// <summary>
    /// 玩家在一本场内所通用的总思考时间,仅在开始新的本场时重置为20s(默认),若为零则强制跳过操作
    /// </summary>
    public float[] PlayerThinkingTimeCountList { get; private set; } = new float[4];
    /// <summary>
    /// 玩家行为解决方法,接收进行操作的玩家序号和其操作,并判断是否合理
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool PlayerActionSolve(PlayerActionData playerActionData, int player)
    {
        if (AvailableActionDict != null)
        {
            Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = AvailableActionDict[player];
            // 列表化
            List<PlayerActionData> actionList = playerActionDict[playerActionData.PlayerAction];
            if (actionList.Count == 0)
            {
                // 根本没有对应操作,返回false
                return false;
            }
            else
            {
                return actionList.Contains(playerActionData);
            }
        }
        else
        {
            throw new System.Exception("意料外的操作判断");
        }

    }
    /// <summary>
    /// 当有人打出牌时调用,获取所有可行的响应并发送
    /// </summary>
    /// <returns></returns>
    public bool AvailableActionSend()
    {
        for (int i = 0; i < MatchControl.PlayerList.Count; i++)
        {
            ShouPai shouPai = MatchControl.PlayerList[i].ShouPai;
            // 根据场况依次获取所有玩家的可行操作并存储

            // 鸣牌(吃/碰/杠)的可行操作
            AvailableActionDict[i] = GlobalFunction.SingleClaimingAvailableJudge(shouPai, MatchControl);
            // 荣和的可行操作
            if (AvailableTingPaiDict[i].ContainsKey(MatchControl.CurrentPai))
            {
                // 因为只要能和,就是固定的,因此仅标记可以荣和
                AvailableActionDict[i].Add(PlayerAction.Ron, new());
            }
        }

        // [TODO] Send
        throw new System.Exception("未完成");

        return false;
    }
    /// <summary>
    /// 听牌列表刷新,当有玩家手牌发生改变,即手切/拔北/鸣牌/开杠时调用,更新此玩家的听牌列表
    /// </summary>
    /// <param name="playerNumber">改变自己手牌的玩家序号</param>
    /// <returns></returns>
    public bool TingPaiRefresh(int playerNumber)
    {
        if (GlobalFunction.TingPaiJudge(MatchControl.PlayerList[playerNumber].ShouPai, out Dictionary<Pai, List<Group>> groups))
        {
            // 听牌(不考虑是否有役种)则修改字典
            AvailableTingPaiDict[playerNumber] = groups;
            return true;
        }
        else
        {
            // 判断后发现未听牌,置空
            AvailableTingPaiDict[playerNumber] = null;
            return true;
        }
    }
    /// <summary>
    /// 长考时间的刷新,仅在新本场开始时调用
    /// </summary>
    public void ThinkingTimeReset()
    {
        PlayerThinkingTimeCountList = new float[4] { 20f, 20f, 20f, 20f };
    }
}
