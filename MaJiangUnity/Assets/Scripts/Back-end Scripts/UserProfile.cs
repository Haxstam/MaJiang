using MaJiangLib;
using System.Text;
using UnityEngine;

/// <summary>
/// ��ʾ�û��Լ��Ļ�����Ϣ
/// </summary>
public class UserProfile : IByteable<UserProfile>
{
    /// <summary>
    /// �û���
    /// </summary>
    public string Name { get; set; }

    public int ByteSize { get; set; } = 96;

    public static implicit operator byte[](UserProfile userProfile)
    {
        // �����û���Ϣ֮�󳤶Ⱥܿ�������,�����һ���ܴ������
        byte[] mainBytes = new byte[96];
        byte[] nameBytes = Encoding.UTF8.GetBytes(userProfile.Name);
        if (nameBytes.Length > 48)
        {
            throw new System.Exception("�����û���,����48bytes��������");
        }
        GlobalFunction.ReplaceBytes(mainBytes, nameBytes, 0);
        return mainBytes;
    }
    public static UserProfile StaticBytesTo(byte[] bytes, int index = 0)
    {
        string name = Encoding.UTF8.GetString(bytes, index, 48);
        UserProfile userProfile = new UserProfile();
        userProfile.Name = name;
        return userProfile;
    }
    public UserProfile BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);
    public byte[] GetBytes() => this;
}
