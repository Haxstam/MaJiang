using System;
using UnityEngine;

/// <summary>
/// 256位随机数产生器,Xoshiro256**算法
/// </summary>
public class Xoshiro256StarStar
{
    private ulong[] s = new ulong[4]; // 256位状态
    private readonly byte[] seed;
    /// <summary>
    /// 通过指定的256位(32字节)的种子来创建随机数产生器
    /// </summary>
    /// <param name="seed"></param>
    /// <exception cref="Exception"></exception>
    public Xoshiro256StarStar(byte[] seed)
    {
        if (seed.Length < 32)
        {
            throw new Exception("种子需要32字节");
        }
        this.seed = seed;
        s[0] = BitConverter.ToUInt64(seed, 0);
        s[1] = BitConverter.ToUInt64(seed, 8);
        s[2] = BitConverter.ToUInt64(seed, 16);
        s[3] = BitConverter.ToUInt64(seed, 24);
        // 状态全为0会导致结果异常，简单处理
        if (s[0] == 0 && s[1] == 0 && s[2] == 0 && s[3] == 0)
        {
            s[0] = 1;
        }
    }
    /// <summary>
    /// 随机生成一个256位(32字节)的种子并创建随机数产生器
    /// </summary>
    public Xoshiro256StarStar()
    {
        seed = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(seed);
        }
        s[0] = BitConverter.ToUInt64(seed, 0);
        s[1] = BitConverter.ToUInt64(seed, 8);
        s[2] = BitConverter.ToUInt64(seed, 16);
        s[3] = BitConverter.ToUInt64(seed, 24);
        // 状态全为0会导致结果异常，简单处理
        if (s[0] == 0 && s[1] == 0 && s[2] == 0 && s[3] == 0)
        {
            s[0] = 1;
        }
    }

    private ulong Rol64(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong Next()
    {
        ulong result = Rol64(s[1] * 5, 7) * 9;
        ulong t = s[1] << 17;

        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];

        s[2] ^= t;
        s[3] = Rol64(s[3], 45);


        return result;
    }

    public int NextInt32()
    {
        // 直接取64位随机数的低32位
        return (int)(Next() & 0xFFFFFFFF);
    }
}
