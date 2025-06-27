using System;
using System.Collections.Generic;
using MaJiangLib.SignalClass;
namespace MaJiangLib.Utility
{
    public interface ISignalFactory
    {
        GameNetSignal CreateEmptySignal(ushort signalID);
        bool HasSignal(ushort signalID);
        ushort GetSignalID(GameNetSignal signal);
    }

    public class SignalFactory : ISignalFactory
    {
        // 私有静态实例（单例核心）
        private static SignalFactory _instance;
        private static readonly object _lock = new object();

        // 反射数据缓存
        private static readonly Dictionary<Type, ushort> _typeToId = new();
        private static readonly Dictionary<ushort, Type> _idToType = new();

        // 全局访问点（推荐方式）
        public static ISignalFactory Instance {
            get {
                if (_instance == null) {
                    lock (_lock) {
                        _instance ??= new SignalFactory();
                    }
                }
                return _instance;
            }
        }

        // 私有构造函数（防止外部创建）
        private SignalFactory()
        {
            InitializeReflectionCache();
        }

        private void InitializeReflectionCache()
        {
            var attribs = ReflectUtility.GetAttributes<GameSignalAttribute>();
            foreach (var (type, attrib) in attribs) {
                _typeToId[type] = attrib.SignalID;
                _idToType[attrib.SignalID] = type;
            }
        }

        public GameNetSignal CreateEmptySignal(ushort signalID)
            => _idToType.TryGetValue(signalID, out var type) 
                ? (GameNetSignal)Activator.CreateInstance(type) 
                : null;

        public bool HasSignal(ushort signalID) 
            => _idToType.ContainsKey(signalID);

        public ushort GetSignalID(GameNetSignal signal) 
            => _typeToId.GetValueOrDefault(signal.GetType(), (ushort.MinValue));
    }
}