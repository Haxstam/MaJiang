using System;
using LiteNetLib.Utils;
namespace MaJiangLib.SignalClass
{
    /// <summary>
    /// 配牌，服务端给所有人发
    /// </summary>
    [Serializable,GameSignal(200)]
    public class PeiPai_C2S : GameNetSignal
    {
        public short UserUID;
        public string Pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(Pai.Length);
            netw.Put(Pai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            Pai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 摸牌的信号，由服务端向客户端发送，对单发送
    /// 包含摸牌的人，摸到的牌的值
    /// </summary>
    [Serializable,GameSignal(100)]
    public class MoPai_S2C : GameNetSignal
    {
        public ushort UserUID;
        public string pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(pai.Length);
            netw.Put(pai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            pai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 摸牌处理信号，由客户端向服务端发送，对单发送
    /// 包含摸牌人的行为
    /// </summary>
    [Serializable,GameSignal(101)]
    public class MoPai_C2S : GameNetSignal
    {
        public ushort UserUID;
        public ushort  MoPaiAction;
        public GameNetSignal NetSignal;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(MoPaiAction);
            NetSignal.Writer(netw);
        }
        public override void Reader(NetDataReader netr)
        {
            UserUID = netr.GetUShort();
            MoPaiAction = netr.GetUShort();
            switch (MoPaiAction)
            {
                case 102:
                    NetSignal = new DaPai_C2S();
                    NetSignal.Reader(netr);
                    break;
                case 105:
                    NetSignal = new Gan_C2S();
                    NetSignal.Reader(netr);
                    break;
                case 113:
                    NetSignal = new He_C2S();
                    NetSignal.Reader(netr);
                    break;
                    
            }
        }
    }
    /// <summary>
    /// 打牌的信号，客户端给服务端发送，
    /// 可以包含在MoPai_reply中,或者其它reply中
    /// </summary>
    [Serializable,GameSignal(102)]
    public class DaPai_C2S :GameNetSignal
    {
        public ushort UserUID;
        public string Pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(Pai.Length);
            netw.Put(Pai);
        }
        public override void Reader(NetDataReader netr)
        {
            UserUID=netr.GetUShort();
            var len = netr.GetInt();
            Pai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 打牌处理信号，由服务端向客户端发送，广播
    /// 包含打牌的人，打牌的牌
    /// </summary>
    [Serializable,GameSignal(103)]
    public class DaPai_S2CC:GameNetSignal
    {
        public ushort UserUID;
        public string  Pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(Pai.Length);
            netw.Put(Pai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            Pai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 打牌后无人吃碰和一类
    /// 等待时间过或者全部取消，或者压根没人可以吃碰和
    /// 进入下一个人的回合 通知一下所有人
    /// </summary>
    [Serializable,GameSignal(104)]
    public class EnterRound_S2CC : GameNetSignal
    {
        public ushort EnterUserUID;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(EnterUserUID);
        }
        public override void Reader(NetDataReader netr)
        {
            EnterUserUID= netr.GetUShort();
        }
    }
    /// <summary>
    /// 杠信号 客户端发给服务端 单播
    /// 杠的人，杠的牌型
    /// </summary>
    [Serializable,GameSignal(105)]
    public class Gan_C2S : GameNetSignal
    {
        public ushort UserUID;
        public string Pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(Pai.Length);
            netw.Put(Pai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int paiLen);
            Pai = netr.GetString(paiLen);
        }
    }
    /// <summary>
    /// 杠之后要开宝牌
    /// </summary>
    [Serializable, GameSignal(106)]
    public class Gan_S2CC : GameNetSignal
    {
        public ushort UserUID;
        /// <summary>
        /// 整个宝牌库
        /// </summary>
        public string  BaoPai;

        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(BaoPai.Length);    
            netw.Put(BaoPai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            netr.Get(out BaoPai,len);
        }
    }

    [Serializable, GameSignal(107)]
    public class GanMo_C2S : GameNetSignal
    {
        public ushort UserUID;
        public string Pai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(Pai.Length);
            netw.Put(Pai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID );
            netr.Get(out int len);
            netr.Get(out Pai,len);
        }
    }
    /// <summary>
    /// FuLu信号 包含副露的类型和信息
    /// </summary>
    [Serializable,  GameSignal(108)]
    public class FuLu_C2S : GameNetSignal
    {
        public ushort UserUID;
        public ushort FuLuAction;
        public GameNetSignal Signal;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(FuLuAction);
            Signal.Writer(netw);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out FuLuAction);
            switch (FuLuAction)
            {
                case 109:
                    Signal = new Chi_C2S();
                    Signal.Reader(netr);
                    break;
                case 110:
                    Signal = new Pen_C2S();
                    Signal.Reader(netr);
                    break;
                case 105:
                    Signal = new Gan_C2S();
                    Signal.Reader(netr);
                    break;
            }
        }
    }
    /// <summary>
    /// 吃牌信号  谁吃的什么牌型
    /// </summary>
    [Serializable, GameSignal(109)]
    public class Chi_C2S : GameNetSignal
    {
        public ushort UserUID;
        public string ChiPai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(ChiPai.Length);
            netw.Put(ChiPai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            netr.Get(out ChiPai,len);
        }
    }
    /// <summary>
    /// 碰牌信号  谁碰了什么牌型
    /// </summary>
    [Serializable, GameSignal(110)]
    public class Pen_C2S : GameNetSignal
    {
        public ushort  UserUID;
        public string PenPai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(PenPai.Length);
            netw.Put(PenPai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            PenPai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 吃牌处理信号  谁吃什么牌型 广播
    /// </summary>
    [Serializable, GameSignal(111)]
    public class Chi_S2CC : GameNetSignal
    {
        public ushort UserUID;
        public string ChiPai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(ChiPai.Length);
            netw.Put(ChiPai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            ChiPai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 碰处理信号  谁碰什么牌型 广播
    /// </summary>
    [Serializable, GameSignal(112)]
    public class Pen_S2CC : GameNetSignal
    {
        public ushort  UserUID;
        public string PenPai;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
            netw.Put(PenPai.Length);
            netw.Put(PenPai);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
            netr.Get(out int len);
            PenPai = netr.GetString(len);
        }
    }
    /// <summary>
    /// 和处理型号 谁和了
    /// </summary>
    [Serializable,GameSignal(113)]
    public class He_C2S : GameNetSignal
    {
        public ushort UserUID;
        public override void Writer(NetDataWriter netw)
        {
            netw.Put(UserUID);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out UserUID);
        }
    }
    /// <summary>
    /// 流局信号
    /// </summary>
    [Serializable,  GameSignal(114)]
    public class LiuJv_S2CC : GameNetSignal
    {
        public ushort LiuType;
        public string LiuJv;

        public override void Writer(NetDataWriter netw)
        {
            netw.Put(LiuType);
            netw.Put(LiuJv.Length);
            netw.Put(LiuJv);
        }
        public override void Reader(NetDataReader netr)
        {
            netr.Get(out LiuType);
            netr.Get(out int len);
            netr.Get(out LiuJv,len);
        }
    }
}