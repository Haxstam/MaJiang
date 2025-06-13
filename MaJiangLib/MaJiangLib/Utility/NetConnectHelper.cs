using System;
using System.Collections.Generic;
using System.Reflection;
using LiteNetLib.Utils;
using MaJiangLib.SignalClass;
namespace MaJiangLib.Utility
{
    /// <summary>
    ///     网络连接辅助类
    ///     负责进行一些基础的消息收发和解析等工作
    /// </summary>
    public class NetConnectHelper
    {
        /// <summary>
        ///     缓存避免重复反射
        /// </summary>
        public static Dictionary<Type, ushort> tempSignalIDs = new Dictionary<Type, ushort>();
        /// <summary>
        /// 用来通过id获取对应的消息
        /// </summary>
        public static Dictionary<ushort,Type > tempReSignalIDs = new Dictionary<ushort,Type >();
        /// <summary>
        /// 数据写入工具
        /// </summary>
        public NetDataWriter DataWriter;
        /// <summary>
        /// 数据读取回调
        /// </summary>
        public Action<GameNetSignal> OnSignalGet;
        /// <summary>
        ///     反射获取命令集
        /// </summary>
        public NetConnectHelper()
        {
            DataWriter = new NetDataWriter();
            var temp= ReflectUtility.GetAttributes<GameSignalAttribute>();
            foreach (var com in temp)
            {
                tempSignalIDs.Add(com.Item1,com.Item2.SignalID);
                tempReSignalIDs.Add(com.Item2.SignalID,com.Item1);
            }
        }
        public void SendSignal(GameNetSignal signal)
        {
            Type sinType = signal.GetType();
            if ( tempSignalIDs.TryGetValue(sinType, out ushort SignalID))
            {
                //上锁避免多线程导致写信息错位问题
                lock (DataWriter)
                {
                    DataWriter.Put(SignalID);
                    signal.Writer(DataWriter);
                }
            }
            else
            {
                throw new Exception("没有注册的信号");
            }
        }
        /// <summary>
        /// 得到信号
        /// </summary>
        public void GetSignal(NetDataReader netr)
        {
            while (!netr.EndOfData)
            {
                ushort signalID = netr.GetUShort();
                GameNetSignal signal = ReadSignal(netr,signalID);
                OnSignalGet.Invoke(signal);
            }
        }
        /// <summary>
        /// 解析信号
        /// </summary>
        private GameNetSignal ReadSignal(NetDataReader netr , ushort signalID)
        {
            if (!tempReSignalIDs.TryGetValue(signalID, out Type? d))
            {
                throw new Exception("没有注册的信号");
            }
            GameNetSignal  signal = Activator.CreateInstance(d,new object[] {}) as GameNetSignal;
            signal.Reader(netr);
            return signal;
        }
    }
}