using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 若类可以被转换为字节串,则实现此类
/// </summary>
public interface IByteable
{
    public int ByteSize { get; }
    public byte[] GetBytes();
}
