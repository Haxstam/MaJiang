using System;
using LiteNetLib.Utils;
namespace MaJiangLib.SignalClass
{
    [GameSignal(1)]
    public class TestSig : GameNetSignal
    {
        private int randomValue;
        public TestSig()
        {
            randomValue = new Random().Next();
            Console.WriteLine("TestSig:{0}",randomValue);
        }
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(randomValue);
        }
        public override void Reader(NetDataReader netr)
        {
            randomValue=netr.GetInt();           
        }
    }
}