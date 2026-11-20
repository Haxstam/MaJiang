using System;
using System.Linq;
using MaJiangLib;
using MaJiang.Presentation;

internal static partial class Program
{
    private static Group DisplayGroup(GroupType type, string hand, string claimed = null, int source = 3)
    {
        var tiles = hand.Split(' ').Select(text => Tile.BytesTo(text)).ToList();
        return claimed == null ? new Group(type, tiles[0].Color, tiles) : new Group(type, tiles[0].Color, tiles, source, Tile.BytesTo(claimed));
    }
    private static MahjongMeldPresentation DisplayCheck(Group group, params string[] slots)
    {
        Tile[] before = group.Tiles.ToArray(); Tile called = group.SingleTile;
        GroupType type = group.GroupType; int source = group.FuluSource;
        MahjongMeldPresentation view = MahjongMeldPresentation.From(group, 0);
        Tile[] actual = new[] { view.First, view.Second, view.Claimed, view.Kan };
        if (actual.Length != slots.Length || actual.Where((tile, i) => !Tile.ExactlyEquals(tile, slots[i] == "-" ? null : Tile.BytesTo(slots[i]))).Any())
        { throw new InvalidOperationException("Meld display slot mismatch."); }
        if (before.Length != group.Tiles.Count || before.Where((tile,i) => !ReferenceEquals(tile, group.Tiles[i])).Any()
            || !ReferenceEquals(called, group.SingleTile) || type != group.GroupType || source != group.FuluSource)
        { throw new InvalidOperationException("Display changed the original meld."); }
        string[] represented = actual.Where(tile => tile != null).Select(tile => tile.ToString()).OrderBy(text => text).ToArray();
        if (!represented.SequenceEqual(group.Tiles.Select(tile => tile.ToString()).OrderBy(text => text)))
        { throw new InvalidOperationException("Display lost or duplicated a physical tile."); }
        return view;
    }
    private static void DisplayChi()
    {
        var view = DisplayCheck(DisplayGroup(GroupType.OpenStraight, "4s 2s", "3s"), "2s", "4s", "3s", "-");
        if (view.ShowKan || view.ConcealOuter) { throw new InvalidOperationException("Chi exposes the fourth card or hides the hand tiles."); }
    }
    private static void DisplayPon()
    {
        DisplayCheck(DisplayGroup(GroupType.OpenTriple, "5p 0p", "5p"), "0p", "5p", "5p", "-");
        var view = DisplayCheck(DisplayGroup(GroupType.OpenTriple, "5p 5p", "0p"), "5p", "5p", "0p", "-");
        if (view.ShowKan) { throw new InvalidOperationException("Pon shows the fourth card."); }
    }
    private static void DisplayOpenKan()
    {
        DisplayCheck(DisplayGroup(GroupType.OpenKan, "5p 5p 5p", "0p"), "5p", "5p", "5p", "0p");
        var view = DisplayCheck(DisplayGroup(GroupType.OpenKan, "5p 0p 5p", "5p"), "0p", "5p", "5p", "5p");
        if (!view.ShowKan || view.ConcealOuter) { throw new InvalidOperationException("Open kan card flags are wrong."); }
    }
    private static void DisplayAddedKan()
    {
        // 与实际加杠存储顺序一致：原碰的三张，再追加第四张。
        Group calledRed = DisplayGroup(GroupType.OpenTriple, "5p 5p", "0p");
        calledRed.GroupType = GroupType.AddedKan; calledRed.Tiles.Add(Tile.BytesTo("5p"));
        DisplayCheck(calledRed, "5p", "5p", "0p", "5p");
        Group addedRed = DisplayGroup(GroupType.OpenTriple, "5p 5p", "5p");
        addedRed.GroupType = GroupType.AddedKan; addedRed.Tiles.Add(Tile.BytesTo("0p"));
        DisplayCheck(addedRed, "0p", "5p", "5p", "5p");
    }
    private static void DisplayClosedKan()
    {
        var view = DisplayCheck(DisplayGroup(GroupType.ClosedKan, "5p 0p 5p 5p"), "5p", "5p", "5p", "0p");
        if (!view.ConcealOuter || !view.ShowKan || view.Direction != ClaimDirection.Top) { throw new InvalidOperationException("Closed kan must show outer backs in the top group."); }
        DisplayCheck(DisplayGroup(GroupType.ClosedKan, "2s 2s 2s 2s"), "2s", "2s", "2s", "2s");
    }
    private static void DisplayDirections()
    {
        for (int owner = 0; owner < 4; owner++)
        {
            foreach (var pair in new[] { (1, ClaimDirection.Right), (2, ClaimDirection.Top), (3, ClaimDirection.Left) })
            {
                Group group = DisplayGroup(GroupType.OpenTriple, "3s 3s", "3s", (owner + pair.Item1) % 4);
                if (MahjongMeldPresentation.From(group, owner).Direction != pair.Item2) { throw new InvalidOperationException("Claim direction changed with absolute seat numbering."); }
            }
        }
    }
}
