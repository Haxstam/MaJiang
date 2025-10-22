using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
using static MaJiangLib.PackCoder;

public class Pack
{
    /// <summary>
    /// 空包创建方法,需要发送者的名称和IP地址
    /// </summary>
    /// <param name="senderName"></param>
    /// <param name="iPAddress"></param>
    public Pack(string senderName, IPAddress iPAddress)
    {
        byte[] Bytes = new byte[PackSize];
        Span<byte> spanBytes = Bytes;

        PackHeadBytes.CopyTo(spanBytes);
        ReplaceBytes(spanBytes, PackHeadBytes, 0);
        ReplaceBytes(spanBytes, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), 8);
        ReplaceBytes(spanBytes, iPAddress.GetAddressBytes(), 16);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(senderName), 20);
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
            PackType = (PackType)spanBytes[104];
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
    public PackType PackType { get; set; }
    /// <summary>
    /// 在房间内聊天室的语言子包,考虑到UTF-8编码限制,限定最长单句话为64字符,也即最大长度为256bytes
    /// </summary>
    /// <param name="pack">待加工包</param>
    /// <param name="word">所要传输的内容,限定最长为64字符</param>
    /// <param name="roomNumber">目标房间号,避免潜在的冲突</param>
    /// <returns></returns>
    public bool RoomWordPack(string word, int roomNumber)
    {
        // 头标识 4 bytes + 房间号(int) 4 bytes + 内容
        PackType = PackType.RoomWord;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.RoomWord;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(roomNumber), 108);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(word), 116);
        return true;
    }
    /// <summary>
    /// 玩家进行操作时所发送的操作子包
    /// </summary>
    /// <param name="pack">待加工包</param>
    /// <param name="playerActionData">玩家所进行的操作</param>
    /// <returns></returns>
    public bool PlayerActionPack(PlayerActionData playerActionData)
    {
        // 头标识 4 bytes + 玩家操作 16 bytes
        PackType = PackType.PlayerAction;
        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.PlayerAction;
        ReplaceBytes(spanBytes, playerActionData.GetBytes(), 108);
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
        spanBytes[104] = (byte)PackType.MatchData;
        ReplaceBytes(spanBytes, mainMatchControl.GetPublicBytes(), 108);
        return true;
    }
    /// <summary>
    /// 起手13张牌的初始化
    /// </summary>
    /// <param name="pais"></param>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool InitPack(List<Pai> pais, int player)
    {
        PackType = PackType.Init;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.Init;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(player), 108);
        ReplaceBytes(spanBytes, ListToBytes(pais), 112);
        return true;
    }
    public bool AvailableActionPack(Dictionary<PlayerAction, List<PlayerActionData>> avaliableActions, int player)
    {
        PackType = PackType.AvailableAction;
        // 这里单独实现字典->byte[]的转换
        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.AvailableAction;
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
        return true;
    }
    public bool SignalPack(SignalType signalType, int player)
    {
        PackType = PackType.Acknowledge;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.Acknowledge;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(player), 108);
        spanBytes[112] = (byte)signalType;
        return true;
    }

}
public enum PackType : byte
{
    /// <summary>
    /// 空包,或子包内容不合规
    /// </summary>
    Empty = 1,
    /// <summary>
    /// 系统信息
    /// </summary>
    SystemInformation,
    /// <summary>
    /// 确认信息
    /// </summary>
    Acknowledge,
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
}
