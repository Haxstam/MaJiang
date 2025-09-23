using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// ������Ա�ת��Ϊ�ֽڴ�,��ʵ�ִ���
/// </summary>
public interface IByteable<T>
{
    // �κ�ʵ���˸ýӿڵ��඼������GlobalFunction.ByteableInstanceDict�н���ע��
    public int ByteSize { get; }
    public byte[] GetBytes();
    public T BytesTo(byte[] bytes, int index);
}
