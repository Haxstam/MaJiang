using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// ������Ա�ת��Ϊ�ֽڴ�,��ʵ�ִ���
/// </summary>
public interface IByteable
{
    public int ByteSize { get; }
    public byte[] GetBytes();
}
