using System;
using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>表现层的座位输入接口；局部输入和网络输入都提交绑定局/阶段的选择。</summary>
    public interface IMatchDecisionInput
    {
        VisibleMatchState Current { get; }
        IReadOnlyList<MatchCommand> Choices { get; }
        bool Pending { get; }
        bool Submit(MatchCommand command);
    }

    /// <summary>每个实例只控制一个客户端，不持有服务端暗手；确认与状态推进前锁住重复输入。</summary>
    public sealed class NetworkDecisionInput : IMatchDecisionInput
    {
        private readonly LanRoomClient client;
        private Guid pending;
        private VisibleMatchState submittedView;
        private bool acknowledged;
        public VisibleMatchState Current => client.Match.Current;
        public bool Pending
        {
            get
            {
                if (acknowledged && !ReferenceEquals(Current, submittedView)) { pending = Guid.Empty; }
                return pending != Guid.Empty;
            }
        }
        public IReadOnlyList<MatchCommand> Choices => Pending ? Array.Empty<MatchCommand>() : client.AvailableCommands;
        public NetworkDecisionInput(LanRoomClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            client.CommandCompleted += result =>
            {
                if (result.CommandId != pending) { return; }
                if (!result.Accepted) { pending = Guid.Empty; }
                else { acknowledged = true; }
            };
        }
        public bool Submit(MatchCommand command)
        {
            if (Pending || command == null || !Contains(command)) { return false; }
            submittedView = Current; acknowledged = false;
            pending = client.SendCommand(command);
            return pending != Guid.Empty;
        }
        private bool Contains(MatchCommand command)
        {
            foreach (MatchCommand choice in client.AvailableCommands)
            {
                if (ReferenceEquals(choice, command)) { return true; }
            }
            return false;
        }
    }
}
