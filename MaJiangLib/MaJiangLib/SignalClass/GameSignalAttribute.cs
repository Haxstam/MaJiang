using System;
using LiteNetLib.Utils;
namespace MaJiangLib.SignalClass
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GameSignalAttribute : Attribute
    {
        public ushort SignalID;
        public GameSignalAttribute(ushort signalID)
        {
            SignalID = signalID;
        }
    }

    public abstract class GameNetSignal
    {
        public abstract void Writer(NetDataWriter netw);
        public abstract void Reader(NetDataReader netr);
    }
}