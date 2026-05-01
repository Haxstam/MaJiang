using MaJiangLib;
using System;
using System.Text;
using UnityEngine;
[Serializable]
/// <summary>
/// 表示用户自己的基本信息
/// </summary>
public class UserProfile : IByteable<UserProfile>
{
    public UserProfile()
    {

    }
    public UserProfile(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Name { get => name; set => name = value; }
    public const int byteSize = 96;
    [SerializeField]
    private string name;

    public int ByteSize { get => byteSize; }

    public static implicit operator byte[](UserProfile userProfile)
    {
        // 介于用户信息之后长度很可能增加,因此留一个很大的冗余
        Span<byte> mainBytes = new byte[96];
        byte[] nameBytes = Encoding.UTF8.GetBytes(userProfile.Name);
        if (nameBytes.Length > 48)
        {
            throw new System.Exception("过长用户名,超过48bytes编码限制");
        }
        GlobalFunction.ReplaceBytes(mainBytes, nameBytes, 0);
        return mainBytes.ToArray();
    }
    public static UserProfile StaticBytesTo(byte[] bytes, int index = 0)
    {
        string name = Encoding.UTF8.GetString(bytes, index, 48);
        UserProfile userProfile = new()
        {
            Name = name
        };
        return userProfile;
    }
    public UserProfile BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);
    public byte[] GetBytes() => this;
    public static bool operator ==(UserProfile a, UserProfile b)
    {
        if (a.Name == b.Name)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public static bool operator !=(UserProfile a, UserProfile b) => !(a == b);
    public override int GetHashCode()
    {
        return HashCode.Combine(Name);
    }
    public override bool Equals(object obj)
    {
        UserProfile userProfile = obj as UserProfile;
        return userProfile != null && userProfile == this;
    }
}
