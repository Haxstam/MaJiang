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
        }
        public void SendSignal(GameNetSignal signal)
        {
            int signalId = SignalFactory.GetSignalID(signal);
            if ( signalId>0)
            {
                //上锁避免多线程导致写信息错位问题
                lock (DataWriter)
                {
                    DataWriter.Put(signalId);
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
        public void SignalGet(NetDataReader netr)
        {
            while (!netr.EndOfData)
            {
                ushort signalID = netr.GetUShort();
                GameNetSignal signal = SignalRead(netr,signalID);
                OnSignalGet.Invoke(signal);
            }
        }
        /// <summary>
        /// 解析信号
        /// </summary>
        private GameNetSignal SignalRead(NetDataReader netr , ushort signalID)
        {
            var d = SignalFactory.CreatEmptySignal(signalID);
            d.Reader(netr);
            return d;
        }
    }
}