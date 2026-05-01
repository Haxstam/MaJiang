using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static MaJiangLib.GlobalFunction;

public class Pack
{
    /*
     * 包结构
     * (1) 8   bytes [0x02, 0x48, 0x61, 0x78, 0x4D, 0x61, 0x6A, 0x50] "\u0002HaxMajP" 数据包头特定标识,UTF-8编码,首位为STX(0x02)从而避免包头误判
     * (2) 8   bytes Unix毫秒时间戳,long类型
     * (3) 4   bytes 包来源IP地址,为IPv4地址类型
     * (4) 48  bytes 包来源用户名,为string类型,UTF-8编码,限定最长为12字符,如果不足则补0
     * (5) 4   bytes 版本号([TODO]暂空,未实现)
     * (6) 16  bytes MD5校验(72~87)
     * (7) 4   bytes 包类型
     * (8) 4   bytes TaskID(92~95)
     * (9) 8   bytes 留空
     * (10)912 bytes 包主要数据部分(起始于第104处)
     * (11)8   bytes [0x03, 0x45, 0x6E, 0x64, 0x50, 0x61, 0x63, 0x6B]"\u0003EndPack" 数据包结束特定标识,UTF-8编码,首位为ETX(0x03)从而避免包尾误判
     * 
     * 1.因为包变量
     */

    public static string PackHeadStr { get; } = "\u0002HaxMajP";
    public static byte[] PackHeadBytes { get; } = new byte[8] { 0x02, 0x48, 0x61, 0x78, 0x4D, 0x61, 0x6A, 0x50 };
    public static string PackEndStr { get; } = "\u0003EndPack";
    public static byte[] PackEndBytes { get; } = new byte[8] { 0x03, 0x45, 0x6E, 0x64, 0x50, 0x61, 0x63, 0x6B };
    public static int PackSize { get; } = 1024;
    public static int PackEndIndex { get; } = 1016;


    /// <summary>
    /// 用于判断包是否合法(包头尾正确,大小正确),如果合法则返回True,反之则返回False
    /// </summary>
    /// <param name="pack"></param>
    /// <returns>如果合法则返回True,反之则返回False</returns>
    public static bool IsLegalPack(byte[] pack)
    {
        if (pack.Length == PackSize)
        {
            for (int i = 0; i < 8; i++)
            {
                if (pack[i] == PackHeadBytes[i] && pack[i + PackEndIndex] == PackEndBytes[i])
                {

                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }
    public static bool IsLegalPack(Span<byte> pack)
    {
        if (pack.Length == PackSize)
        {
            for (int i = 0; i < 8; i++)
            {
                if (pack[i] == PackHeadBytes[i] && pack[i + PackEndIndex] == PackEndBytes[i])
                {

                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// 根据给定NetworkInformation创建并自动分配taskID,且将其返回
    /// </summary>
    /// <param name="networkInformation"></param>
    public Pack(INetworkInformation networkInformation, out int taskID)
    {
        Bytes = new byte[PackSize];
        Span<byte> spanBytes = Bytes;
        taskID = networkInformation.GetTaskID();
        PackHeadBytes.CopyTo(spanBytes);
        ReplaceBytes(spanBytes, PackHeadBytes, 0);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), 8);
        ReplaceBytes(spanBytes, networkInformation.SelfIPAddress.GetAddressBytes(), 16);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(networkInformation.SelfProfile.Name), 20);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(taskID), 92);
        ReplaceBytes(spanBytes, PackEndBytes, PackEndIndex);
        PackType = PackType.Empty;
    }
    /// <summary>
    /// 根据给定信息创建,但指定返回包ID
    /// </summary>
    /// <param name="networkInformation"></param>
    /// <param name="returnTaskID"></param>
    public Pack(INetworkInformation networkInformation, int returnTaskID)
    {
        Bytes = new byte[PackSize];
        Span<byte> spanBytes = Bytes;
        PackHeadBytes.CopyTo(spanBytes);
        ReplaceBytes(spanBytes, PackHeadBytes, 0);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), 8);
        ReplaceBytes(spanBytes, networkInformation.SelfIPAddress.GetAddressBytes(), 16);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(networkInformation.SelfProfile.Name), 20);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(returnTaskID), 92);
        ReplaceBytes(spanBytes, PackEndBytes, PackEndIndex);
        PackType = PackType.Empty;
    }
    /// <summary>
    /// 空包创建方法,需要发送者的名称和IP地址
    /// </summary>
    /// <param name="senderName"></param>
    /// <param name="iPAddress"></param>
    public Pack(string senderName, IPAddress iPAddress, int returnTaskID)
    {
        Bytes = new byte[PackSize];
        Span<byte> spanBytes = Bytes;

        PackHeadBytes.CopyTo(spanBytes);
        ReplaceBytes(spanBytes, PackHeadBytes, 0);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), 8);
        ReplaceBytes(spanBytes, iPAddress.GetAddressBytes(), 16);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(senderName), 20);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(returnTaskID), 92);
        ReplaceBytes(spanBytes, PackEndBytes, PackEndIndex);
        PackType = PackType.Empty;
    }
    /// <summary>
    /// 从现有字节串创建包的方法
    /// </summary>
    /// <param name="bytes"></param>
    /// <exception cref="Exception"></exception>
    public Pack(byte[] bytes)
    {
        Bytes = bytes;
        Span<byte> spanBytes = Bytes;
        try
        {
            PackType = (PackType)spanBytes[PackTypeIndex];
        }
        catch (Exception)
        {
            Debug.Log("子包标识无对应类型");
            throw;
        }
    }
    /// <summary>
    /// 包本身的字节串
    /// </summary>
    public byte[] Bytes { get; set; }

    public const int PackTypeIndex = 88;

    // 感觉可以全部写成get;set;的形式,把ReplaceBytes的一部分调用放在里面

    /// <summary>
    /// 包的MD5校验码
    /// </summary>
    public byte[] MD5Code { get { return new Span<byte>(Bytes, 72, 16).ToArray(); } }
    /// <summary>
    /// 包的类型
    /// </summary>
    public PackType PackType
    {
        get
        { return (PackType)Bytes[PackTypeIndex]; }
        set
        { Bytes[PackTypeIndex] = (byte)value; }
    }
    /// <summary>
    /// 包所对应的ID
    /// </summary>
    public int TaskID { get { return BitConverter.ToInt32(Bytes, 92); } }
    /// <summary>
    /// 在房间内聊天室的语言子包,考虑到UTF-8编码限制,限定最长单句话为64字符,也即最大长度为256bytes
    /// </summary>
    /// <param name="pack">待加工包</param>
    /// <param name="word">所要传输的内容,限定最长为64字符</param>
    /// <param name="roomNumber">目标房间号,避免潜在的冲突</param>
    /// <returns></returns>
    public bool RoomWordPack(int roomNumber, string word)
    {
        // 头标识 4 bytes + 房间号(int) 4 bytes + 内容
        PackType = PackType.RoomWord;

        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(roomNumber), 104);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(word), 112);
        AddMD5();
        return true;
    }
    /// <summary>
    /// 玩家进行操作时所发送的操作子包
    /// </summary>
    /// <param name="pack">待加工包</param>
    /// <param name="playerActionData">玩家所进行的操作</param>
    /// <returns></returns>
    public bool PlayerActionPack(List<PlayerActionData> playerActionData, int startIndex, int length)
    {
        // 头标识 4 bytes + 玩家操作列表 16*i bytes
        PackType = PackType.PlayerAction;
        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(startIndex), 104);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(length), 108);
        ReplaceBytes(spanBytes, ListToBytes(playerActionData), 112);
        AddMD5();
        return true;
    }
    /// <summary>
    /// "_MD_" 对局信息包,输出本场比赛所有的公共数据用于同步
    /// </summary>
    /// <param name="pack">初始化包</param>
    /// <param name="mainMatchControl">对局信息</param>
    /// <returns></returns>
    public bool MatchDataPack(MainMatchControl mainMatchControl)
    {
        PackType = PackType.MatchData;

        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, mainMatchControl.GetPublicBytes(), 104);
        AddMD5();
        return true;
    }
    /// <summary>
    /// 对局设定包
    /// </summary>
    /// <param name="matchSettingData"></param>
    /// <returns></returns>
    public bool MatchSettingPack(MatchSettingData matchSettingData)
    {
        PackType = PackType.MatchSettingData;
        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, matchSettingData.GetBytes(), 104);
        AddMD5();
        return true;
    }
    /// <summary>
    /// 可行操作包,存储可行操作,所进行操作的单牌,玩家序号
    /// </summary>
    /// <param name="avaliableActions"></param>
    /// <param name="player"></param>
    /// <param name="singleTile"></param>
    /// <returns></returns>
    public bool AvailableActionPack(Dictionary<PlayerAction, List<PlayerActionData>> avaliableActions, int player, Tile singleTile)
    {
        PackType = PackType.AvailableAction;
        // 这里单独实现字典->byte[]的转换
        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, singleTile.GetBytes(), 104);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(player), 108);
        int index = 112;
        foreach (var pair in avaliableActions)
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }
            else
            {
                // 字典序列化不标记类型,实际上存储为列表
                byte[] listBytes = ListToBytes(pair.Value);
                ReplaceBytes(spanBytes, listBytes, index);
                index += listBytes.Length;
            }
        }
        AddMD5();
        return true;
    }
    public bool SignalPack(SignalType signalType)
    {
        PackType = PackType.Signal;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)signalType;
        AddMD5();
        return true;
    }
    public bool AcknowledgePack(SignalType signalType)
    {
        PackType = PackType.Acknowledge;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)signalType;
        AddMD5();
        return true;
    }
    /// <summary>
    /// 连接房间时所用包,发送房间号和自身信息
    /// </summary>
    /// <param name="roomNumber"></param>
    /// <param name="playerInformation"></param>
    /// <returns></returns>
    public bool ConnectRoomPack(int roomNumber, UserProfile playerInformation)
    {
        PackType = PackType.UserProfile;

        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(roomNumber), 104);
        ReplaceBytes(spanBytes, playerInformation.GetBytes(), 108);
        AddMD5();
        return true;
    }

    public bool HonbaInitPack(List<Tile> tileList, HonbaEndMark honbaEndMark)
    {
        PackType = PackType.Init;

        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, BitConverter.GetBytes((int)honbaEndMark), 104);
        ReplaceBytes(spanBytes, ListToBytes(tileList), 108);
        AddMD5();
        return false;
    }
    public bool ClientStatuPack(ClientStatu clientStatu)
    {
        PackType = PackType.ClientStatuPack;
        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, clientStatu.GetBytes(), 104);
        AddMD5();
        return true;
    }
    /// <summary>
    /// 为包添加MD5标识,仅在包设定完全后生成
    /// </summary>
    public void AddMD5()
    {
        byte[] MD5Code = MD5.Create().ComputeHash(Bytes);
        Span<byte> spanBytes = Bytes;
        ReplaceBytes(spanBytes, MD5Code, 72);
    }

    #region *解码部分*

    /// <summary>
    /// 信号包解码
    /// </summary>
    /// <returns></returns>
    public SignalType SignalPackDecode()
    {
        return (SignalType)Bytes[104];
    }
    public UserProfile ConnectRoomPackDecode(out int roomNumber)
    {
        Span<byte> spanBytes = Bytes;
        UserProfile userProfile = UserProfile.StaticBytesTo(spanBytes.Slice(108, UserProfile.byteSize).ToArray());
        roomNumber = BitConverter.ToInt32(Bytes, 104);
        return userProfile;
    }
    public List<PlayerActionData> PlayerActionPackDecode(out int startIndex, out int length)
    {
        startIndex = BitConverter.ToInt32(Bytes, 104);
        length = BitConverter.ToInt32(Bytes, 108);
        List<PlayerActionData> actionData = BytesToList<PlayerActionData>(Bytes, 112, length);
        return actionData;
    }
    public List<Tile> HonbaInitPackDecode(out HonbaEndMark honbaEndMark)
    {
        honbaEndMark = (HonbaEndMark)BitConverter.ToInt32(Bytes, 104);
        List<Tile> tileList = BytesToList<Tile>(Bytes, 108, 13);
        return tileList;
    }
    public ClientStatu ClientStatuPackDecode()
    {
        Span<byte> spanBytes = Bytes;
        return ClientStatu.StaticBytesTo(spanBytes.Slice(104, ClientStatu.byteSize).ToArray());
    }
    #endregion
}
public enum PackType : byte
{
    /// <summary>
    /// 空包,或子包内容不合规
    /// </summary>
    Empty = 1,
    /// <summary>
    /// 信号
    /// </summary>
    Signal,
    /// <summary>
    /// 系统信息
    /// </summary>
    SystemInformation,
    UserProfile,
    /// <summary>
    /// 确认信息
    /// </summary>
    Acknowledge,
    /// <summary>
    /// 对局设定
    /// </summary>
    MatchSettingData,
    /// <summary>
    /// 对局初始化
    /// </summary>
    Init,
    /// <summary>
    /// 对局信息
    /// </summary>
    MatchData,
    /// <summary>
    /// 玩家行为
    /// </summary>
    PlayerAction,
    /// <summary>
    /// 玩家可进行的行为
    /// </summary>
    AvailableAction,
    /// <summary>
    /// 房间内对话
    /// </summary>
    RoomWord,
    /// <summary>
    /// 社交行为
    /// </summary>
    SocialAction,
    HeartBeatPack,
    ClientStatuPack,
}
