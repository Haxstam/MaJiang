using System;
using System.Net;
using System.Net.Sockets;
using Unity;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
[Serializable]
public class ClientStatu : IByteable<ClientStatu>
{
    public ClientStatu()
    {

    }
    public ClientStatu(int roomNumber, NetworkStatu currentNetworkStatu, IPAddress selfIPAddress, UserProfile selfProfile)
    {
        RoomNumber = roomNumber;
        CurrentNetworkStatu = currentNetworkStatu;
        SelfIPAddress = selfIPAddress;
        SelfProfile = selfProfile;
    }

    /// <summary>
    /// 自身所在房间的房间号
    /// </summary>
    public int RoomNumber { get => roomNumber; set => roomNumber = value; }
    /// <summary>
    /// 当前用户端状态
    /// </summary>
    public NetworkStatu CurrentNetworkStatu { get => currentNetworkStatu; set => currentNetworkStatu = value; }
    /// <summary>
    /// 自身IP地址
    /// </summary>
    public IPAddress SelfIPAddress { get => selfIPAddress; set => selfIPAddress = value; }
    /// <summary>
    /// 自身用户档案
    /// </summary>
    public UserProfile SelfProfile { get => selfProfile; set => selfProfile = value; }
    /// <summary>
    /// 玩家在对局中的序号(仅在开始对局后有效)
    /// </summary>
    public int PlayerNumber { get => playerNumber; set => playerNumber = value; }

    public const int byteSize = 128;
    [SerializeField]
    private NetworkStatu currentNetworkStatu;
    [SerializeField]
    private int roomNumber;
    [SerializeField]
    private IPAddress selfIPAddress;
    [SerializeField]
    private UserProfile selfProfile;
    [SerializeField]
    private int playerNumber;

    public int ByteSize
    {
        get { return byteSize; }
    }


    public override bool Equals(object obj)
    {
        ClientStatu other = obj as ClientStatu;
        return other != null && other == this;
    }
    public static bool operator ==(ClientStatu left, ClientStatu right)
    {
        return left.RoomNumber == right.RoomNumber && left.SelfIPAddress == right.SelfIPAddress && left.SelfProfile == right.SelfProfile && left.CurrentNetworkStatu == right.CurrentNetworkStatu;
    }
    public static bool operator !=(ClientStatu left, ClientStatu right) => !(left == right);
    public override int GetHashCode()
    {
        return HashCode.Combine(RoomNumber, SelfIPAddress, SelfProfile.GetHashCode(), CurrentNetworkStatu.GetHashCode());
    }

    public byte[] GetBytes()
    {

        byte[] bytes = new byte[byteSize];
        ReplaceBytes(bytes, BitConverter.GetBytes(RoomNumber), 0);
        ReplaceBytes(bytes, BitConverter.GetBytes((int)CurrentNetworkStatu), 4);
        if (SelfIPAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            // 是IPv4就标记为true
            ReplaceBytes(bytes, BitConverter.GetBytes(true), 8);
        }
        else if (SelfIPAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // IPv6标记为false,别的值暂时不管
            ReplaceBytes(bytes, BitConverter.GetBytes(false), 8);
        }
        ReplaceBytes(bytes, SelfIPAddress.GetAddressBytes(), 12);
        ReplaceBytes(bytes, SelfProfile.GetBytes(), 28);
        ReplaceBytes(bytes, BitConverter.GetBytes(PlayerNumber), 124);
        return bytes;
    }
    public ClientStatu BytesTo(byte[] bytes, int index) => StaticBytesTo(bytes, index);
    public static ClientStatu StaticBytesTo(byte[] bytes, int index = 0)
    {
        ClientStatu clientStatu = new ClientStatu();
        clientStatu.RoomNumber = BitConverter.ToInt32(bytes, 0);
        clientStatu.CurrentNetworkStatu = (NetworkStatu)BitConverter.ToInt32(bytes, 4);
        bool addressType = BitConverter.ToBoolean(bytes, 8);
        byte[] addressBytes;
        if (addressType)
        {
            addressBytes = new byte[4];
            Buffer.BlockCopy(bytes, 12, addressBytes, 0, 4);
        }
        else
        {
            addressBytes = new byte[16];
            Buffer.BlockCopy(bytes, 12, addressBytes, 0, 16);
        }
        clientStatu.SelfIPAddress = new(addressBytes);
        clientStatu.SelfProfile = UserProfile.StaticBytesTo(bytes, 28);
        clientStatu.PlayerNumber = BitConverter.ToInt32(bytes, 124);
        return clientStatu;
    }
}