using System;
using System.Collections.Generic;
using MaJiangLib.SignalClass;
namespace MaJiangLib.Utility
{
    public interface ISignalFactory
    {
        public static ISignalFactory Instance;
    }
    public class SignalFactory : ISignalFactory
    {
        /// <summary>
        ///     缓存避免重复反射
        /// </summary>
        protected static Dictionary<Type, ushort> tempSignalIDs = new Dictionary<Type, ushort>();
        /// <summary>
        /// 用来通过id获取对应的消息
        /// </summary>
        protected static Dictionary<ushort,Type > tempReSignalIDs = new Dictionary<ushort,Type >();
        public SignalFactory()
        {
            if(ISignalFactory.Instance==null)
                ISignalFactory.Instance = this;
            var temp= ReflectUtility.GetAttributes<GameSignalAttribute>();
            foreach (var com in temp)
            {
                tempSignalIDs.Add(com.Item1,com.Item2.SignalID);
                tempReSignalIDs.Add(com.Item2.SignalID,com.Item1);
            }
        }
        public static GameNetSignal CreatEmptySignal(ushort signalID)
        {
            if (tempReSignalIDs.ContainsKey(signalID))
            {
                var temp = tempReSignalIDs[signalID];
                return (GameNetSignal)Activator.CreateInstance(temp);
            }
            return null;
        }
        public static bool HasSignal(ushort signalID)
        {
            return tempReSignalIDs.ContainsKey(signalID);
        }
        /// <summary>
        /// 获得信号类对应的id
        /// </summary>
        /// <returns>不存在会返回-1</returns>
        public static int GetSignalID(GameNetSignal signal)
        {
            if (tempSignalIDs.ContainsKey(signal.GetType()))
            {
                return tempSignalIDs[signal.GetType()];
            }
            return 0;
        }
    }
}