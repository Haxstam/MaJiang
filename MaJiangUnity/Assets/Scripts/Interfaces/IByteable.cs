using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 若类可以被转换为字节串,则实现此类
/// </summary>
public interface IByteable<T>
{
    // 任何实现了该接口的类都必须在GlobalFunction.ByteableInstanceDict中进行注册
    public int ByteSize { get; }
    public byte[] GetBytes();
    public T BytesTo(byte[] bytes, int index);
}
