using MaJiangLib;
using System;
using System.Linq;

internal static class Program
{
    private static void Main()
    {
        MahjongEngine engine = new(new(MatchType.FourMahjongSouth),
            new[] { new UserProfile("东"), new UserProfile("南"), new UserProfile("西"), new UserProfile("北") },
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        // 这里的循环是宿主主动选择操作；移除循环后，引擎会一直保持当前状态。
        int count = 0;
        while (engine.GetState().OperationStage != MatchOperationStage.RoundEnded && count++ < 1000)
        {
            int seat = engine.GetState().PendingSeats[0];
            var choices = engine.GetAvailableCommands(seat);
            MatchCommand selected = choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                ?? choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.TakeCut)
                ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.HandCut);
            OperationFeedback feedback = engine.Submit(seat, selected);
            foreach (MatchRecord record in feedback.Records)
            {
                Console.WriteLine($"#{record.Sequence} {record.Kind}: {record.Action?.PlayerAction} {record.Action?.TargetTile}");
            }
        }
        MatchState state = engine.GetState();
        Console.WriteLine($"本局结束：{state.Outcome}；点数：{string.Join(", ", state.State.PlayerPoint)}");
        Console.WriteLine($"本局共 {engine.GetRecords().Count} 条已执行记录。等待宿主显式调用 StartNextRound()。");
    }
}
