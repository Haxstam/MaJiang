using System;
using System.Collections.Generic;
using System.Reflection;
using LiteNetLib;
using LiteNetLib.Utils;
using MaJiangLib.SignalClass;
namespace MaJiangLib.Utility
{
    public interface ISignalNetConnectHelper
    {
        public Action<GameNetSignal> OnSignalGet { get; }
        /// <summary>
        /// 连接(绑定上peer)
        /// </summary>
        public void Connect(NetPeer peer);
        /// <summary>
        /// 把信号写入发送列表
        /// </summary>
        /// <param name="signal">信号类</param>
        public void SignalWrite(GameNetSignal signal);
        /// <summary>
        /// 解析读取到的信号
        /// </summary>
        public void SignalGet(NetDataReader netr);
        /// <summary>
        /// 清楚发送列表
        /// </summary>
        public void Clear();
        /// <summary>
        /// 发送信号
        /// </summary>
        public void Send();

    }
    /// <summary>
    ///     网络连接辅助类
    ///     负责进行一些基础的消息收发和解析等工作
    /// </summary>
    public class SignalNetConnectHelper : ISignalNetConnectHelper
    {
        /// <summary>
        /// 数据写入工具
        /// </summary>
        protected NetDataWriter DataWriter;

        public int peerKey => peer.Id;
        public Action<GameNetSignal> OnSignalGet {
            get;
            protected set;
        }

        protected NetPeer peer;
        /// <summary>
        ///     反射获取命令集
        /// </summary>
        public SignalNetConnectHelper()
        {
            DataWriter = new NetDataWriter();
        }
        public void Connect(NetPeer peer)
        {
            this.peer = peer;
        }
        public void SignalWrite(GameNetSignal signal)
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
        public void Send()
        {
            peer.Send(DataWriter, DeliveryMethod.ReliableOrdered);
        }
        public void Clear()
        {
            DataWriter.Reset();
        }
    }
}