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
    /// �հ���������,��Ҫ�����ߵ����ƺ�IP��ַ
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
    /// �������ֽڴ��������ķ���
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
            Debug.Log("�Ӱ���ʶ�޶�Ӧ����");
            throw;
        }
    }
    /// <summary>
    /// ��������ֽڴ�
    /// </summary>
    public byte[] Bytes { get; set; }
    public PackType PackType { get; set; }
    /// <summary>
    /// �ڷ����������ҵ������Ӱ�,���ǵ�UTF-8��������,�޶�����仰Ϊ64�ַ�,Ҳ����󳤶�Ϊ256bytes
    /// </summary>
    /// <param name="pack">���ӹ���</param>
    /// <param name="word">��Ҫ���������,�޶��Ϊ64�ַ�</param>
    /// <param name="roomNumber">Ŀ�귿���,����Ǳ�ڵĳ�ͻ</param>
    /// <returns></returns>
    public bool RoomWordPack(string word, int roomNumber)
    {
        // ͷ��ʶ 4 bytes + �����(int) 4 bytes + ����
        PackType = PackType.RoomWord;

        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.RoomWord;
        ReplaceBytes(spanBytes, BitConverter.GetBytes(roomNumber), 108);
        ReplaceBytes(spanBytes, Encoding.UTF8.GetBytes(word), 116);
        return true;
    }
    /// <summary>
    /// ��ҽ��в���ʱ�����͵Ĳ����Ӱ�
    /// </summary>
    /// <param name="pack">���ӹ���</param>
    /// <param name="playerActionData">��������еĲ���</param>
    /// <returns></returns>
    public bool PlayerActionPack(PlayerActionData playerActionData)
    {
        // ͷ��ʶ 4 bytes + ��Ҳ��� 16 bytes
        PackType = PackType.PlayerAction;
        Span<byte> spanBytes = Bytes;
        spanBytes[104] = (byte)PackType.PlayerAction;
        ReplaceBytes(spanBytes, playerActionData.GetBytes(), 108);
        return true;
    }
    /// <summary>
    /// "_MD_" �Ծ���Ϣ��,��������������еĹ�����������ͬ��
    /// </summary>
    /// <param name="pack">��ʼ����</param>
    /// <param name="mainMatchControl">�Ծ���Ϣ</param>
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
    /// ����13���Ƶĳ�ʼ��
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
        // ���ﵥ��ʵ���ֵ�->byte[]��ת��
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
                // �ֵ����л����������,ʵ���ϴ洢Ϊ�б�
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
    /// �հ�,���Ӱ����ݲ��Ϲ�
    /// </summary>
    Empty = 1,
    /// <summary>
    /// ϵͳ��Ϣ
    /// </summary>
    SystemInformation,
    /// <summary>
    /// ȷ����Ϣ
    /// </summary>
    Acknowledge,
    /// <summary>
    /// �Ծֳ�ʼ��
    /// </summary>
    Init,
    /// <summary>
    /// �Ծ���Ϣ
    /// </summary>
    MatchData,
    /// <summary>
    /// �����Ϊ
    /// </summary>
    PlayerAction,
    /// <summary>
    /// ��ҿɽ��е���Ϊ
    /// </summary>
    AvailableAction,
    /// <summary>
    /// �����ڶԻ�
    /// </summary>
    RoomWord,
    /// <summary>
    /// �罻��Ϊ
    /// </summary>
    SocialAction,
}
