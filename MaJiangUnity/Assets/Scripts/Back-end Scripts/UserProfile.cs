using MaJiangLib;
using System.Text;
using UnityEngine;

/// <summary>
/// 表示用户自己的基本信息
/// </summary>
public class UserProfile : IByteable
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Name { get; set; }

    public int ByteSize { get; set; } = 96;

    public static implicit operator byte[](UserProfile userProfile)
    {
        // 介于用户信息之后长度很可能增加,因此留一个很大的冗余
        byte[] mainBytes = new byte[96];
        byte[] nameBytes = Encoding.UTF8.GetBytes(userProfile.Name);
        if (nameBytes.Length > 48)
        {
            throw new System.Exception("过长用户名,超过48bytes编码限制");
        }
        GlobalFunction.ReplaceBytes(mainBytes, nameBytes, 0);
        return mainBytes;
    }
    public byte[] GetBytes() => this;
}
