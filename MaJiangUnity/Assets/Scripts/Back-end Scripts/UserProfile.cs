using MaJiangLib;
using System.Text;
using UnityEngine;

/// <summary>
/// ��ʾ�û��Լ��Ļ�����Ϣ
/// </summary>
public class UserProfile : IByteable
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
    public byte[] GetBytes() => this;
}
