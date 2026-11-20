using System;
using System.Collections.Generic;
using System.Linq;
using MaJiangLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>使用结算窗口的现有牌位、番种面板和四席点数行展示公开结果。</summary>
    public sealed class MahjongSettlementView : MonoBehaviour
    {
        public MahjongHandStripLayout Hand;
        public MahjongTileWidget WinningTile;
        public RectTransform Dora;
        public RectTransform Ura;
        public TMP_Text Outcome;
        public TMP_Text RoundInfo;
        public GridLayoutGroup YakuList;
        public RectTransform YakuRowPrefab;
        public TMP_Text HanFu;
        public TMP_Text Payment;
        public TMP_Text Limit;
        public RectTransform[] PointRows;
        public TMP_Text Waiting;
        public Button ConfirmButton;

        private static readonly string[] Winds = { "东", "南", "西", "北" };
        private Guid displayedRound;
        private int displayedPage;
        private float yakuCellHeight;

        public void Hide()
        {
            displayedRound = Guid.Empty;
            gameObject.SetActive(false);
        }

        public void Show(MahjongTableView table, VisibleMatchState view, int page, bool confirmationSent)
        {
            RoundSettlementResult result = view.Settlement;
            bool lastPage = page >= result.Winners.Count - 1;
            int selfBit = 1 << view.SelfSeat;
            bool confirmed = confirmationSent || (view.SettlementConfirmations & selfBit) != 0;
            bool othersWaiting = (view.SettlementConfirmations | selfBit) != MatchSession.AllSeatsConfirmed;
            ConfirmButton.gameObject.SetActive(!lastPage || !confirmed);
            Waiting.gameObject.SetActive(lastPage && confirmed && othersWaiting);
            gameObject.SetActive(true);
            if (displayedRound == result.RoundId && displayedPage == page) { return; }
            displayedRound = result.RoundId;
            displayedPage = page;

            WinnerSettlement winner = result.Winners.Count > 0 ? result.Winners[page] : null;
            RevealedHand hand = winner?.WinningTile == null ? null : result.Revealed.Find(item => item.Seat == winner.Seat);
            MainMatchControl state = view.State;
            Outcome.text = OutcomeName(result.Outcome, winner != null);
            RoundInfo.text = $"{Winds[(int)state.Wind - (int)WindType.East]}{state.Round}局{state.Honba}本场";
            if (result.SticksBefore > 0) { RoundInfo.text += $"\n+{result.SticksBefore * 1000}"; }

            Tile[] concealed = hand == null ? Array.Empty<Tile>() : hand.Concealed.OrderBy(tile => tile).ToArray();
            BindCards(table.Art, Hand.Concealed, concealed, false);
            table.DrawMelds(Hand.Melds, hand == null ? Array.Empty<Group>() : hand.Groups, Hand.TileWidth, winner?.Seat ?? 0);
            BindCard(table.Art, WinningTile, winner?.WinningTile, false);
            BindCards(table.Art, Dora, state.DoraPointerList, true);
            bool showUra = hand != null && state.IsRiichi[winner.Seat];
            BindCards(table.Art, Ura, showUra ? result.UraIndicators : Array.Empty<Tile>(), true);

            ScoredHand score = winner?.Score;
            ShowYaku(score?.Yaku);
            HanFu.text = score == null ? "" : score.Yakuman > 0 ? "役满" : score.Fu > 0 ? $"{score.Han}番{score.Fu}符" : $"{score.Han}番";
            bool splitPayment = result.Outcome == RoundOutcomeKind.Tsumo || result.Outcome == RoundOutcomeKind.ExhaustiveDraw;
            Payment.text = score == null ? "" : $"{RiichiScoring.WinningPayment(score.BasePoints, winner.Seat == state.CurrentDealerIndex, splitPayment)}点";
            Limit.text = LimitName(score);
            Limit.gameObject.SetActive(Limit.text.Length > 0);
            ShowPoints(view, table.FixedTableSeat, winner?.Seat ?? -1);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private static void BindCard(MahjongTileArt art, MahjongTileWidget card, Tile tile, bool showBack)
        {
            card.gameObject.SetActive(tile != null || showBack);
            if (!card.gameObject.activeSelf) { return; }
            card.Interactive = false;
            card.HoverSource = false;
            card.Bind(art, tile, tile == null);
        }

        private static void BindCards(MahjongTileArt art, RectTransform list, IReadOnlyList<Tile> tiles, bool showBack)
        {
            for (int index = 0; index < list.childCount; index++)
            {
                var card = list.GetChild(index).GetComponent<MahjongTileWidget>();
                BindCard(art, card, index < tiles.Count ? tiles[index] : null, showBack);
            }
        }

        private void ShowYaku(IReadOnlyList<ScoredYaku> yaku)
        {
            var entries = yaku?.OrderBy(entry => entry.Yaku).ToArray() ?? Array.Empty<ScoredYaku>();
            while (YakuList.transform.childCount < entries.Length) { Instantiate(YakuRowPrefab, YakuList.transform); }
            if (yakuCellHeight == 0) { yakuCellHeight = YakuList.cellSize.y; }
            var listRect = (RectTransform)YakuList.transform;
            int columns = YakuList.constraintCount;
            int rows = Math.Max(1, (entries.Length + columns - 1) / columns);
            float height = listRect.rect.height - YakuList.padding.vertical - (rows - 1) * YakuList.spacing.y;
            YakuList.cellSize = new Vector2(YakuList.cellSize.x, Mathf.Min(yakuCellHeight, height / rows));
            for (int index = 0; index < listRect.childCount; index++)
            {
                Transform row = listRect.GetChild(index);
                row.gameObject.SetActive(index < entries.Length);
                if (!row.gameObject.activeSelf) { continue; }
                ScoredYaku entry = entries[index];
                TMP_Text name = TextAt(row, "番种文字");
                TMP_Text value = TextAt(row, "番数");
                name.text = YakuName(entry.Yaku);
                value.text = entry.Yakuman > 0 ? YakumanName(entry.Yakuman) : $"{entry.Han}番";
                FitYakuText(name);
                FitYakuText(value);
            }
        }

        private void FitYakuText(TMP_Text text)
        {
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, YakuList.cellSize.y - 5);
            text.fontSizeMax = 36;
            text.enableAutoSizing = true;
        }

        private void ShowPoints(VisibleMatchState view, int fixedTableSeat, int winnerSeat)
        {
            MainMatchControl state = view.State;
            RoundSettlementResult result = view.Settlement;
            for (int relative = 0; relative < PointRows.Length; relative++)
            {
                int seat = MahjongSeatOrder.PublicSeat(view.SelfSeat, relative, fixedTableSeat);
                Transform row = PointRows[relative];
                TextAt(row, "风位").text = Winds[MahjongSeatOrder.WindIndex(seat, state.CurrentDealerIndex)];
                TMP_Text name = TextAt(row, "玩家名");
                name.richText = false;
                name.text = state.PlayerList[seat].PlayerProfile.Name;
                name.fontStyle = seat == winnerSeat ? FontStyles.Underline : FontStyles.Normal;
                int delta = result.Deltas[seat];
                string change = delta > 0 ? $"<color=green>+{delta}</color>" : delta < 0 ? $"<color=red>{delta}</color>" : "";
                TMP_Text points = TextAt(row, "点数变化");
                points.richText = true;
                points.text = $"{result.PointsBefore[seat]}{change}";
            }
        }

        private static TMP_Text TextAt(Transform parent, string child) => parent.Find(child).GetComponent<TMP_Text>();
        private static string OutcomeName(RoundOutcomeKind outcome, bool nagashiOrWin) => outcome switch
        {
            RoundOutcomeKind.Ron => "荣和",
            RoundOutcomeKind.Tsumo => "自摸",
            RoundOutcomeKind.ExhaustiveDraw when nagashiOrWin => "流局满贯",
            RoundOutcomeKind.NineTerminals => "九种九牌",
            RoundOutcomeKind.FourWinds => "四风连打",
            RoundOutcomeKind.FourRiichi => "四家立直",
            RoundOutcomeKind.FourKans => "四杠散了",
            _ => "荒牌流局"
        };

        private static string YakumanName(int count) => count > 1 ? $"{count}倍役满" : "役满";
        private static string LimitName(ScoredHand score)
        {
            if (score == null) { return ""; }
            if (score.Yakuman > 0) { return YakumanName(score.Yakuman); }
            return score.BasePoints switch
            {
                8000 => "累计役满", 6000 => "三倍满", 4000 => "倍满", 3000 => "跳满", 2000 => "满贯", _ => ""
            };
        }

        internal static string YakuName(YakuType yaku) => yaku switch
        {
            YakuType.Riichi => "立直", YakuType.DoubleRiichi => "两立直", YakuType.Ippatsu => "一发", YakuType.Tsumo => "门前清自摸和",
            YakuType.Rinshankaiho => "岭上开花", YakuType.HaiteiRaoyui => "海底捞月", YakuType.HoteiRaoyui => "河底捞鱼", YakuType.Chankan => "抢杠",
            YakuType.Jikaze => "自风牌", YakuType.Bakaze => "场风牌", YakuType.Haku => "役牌 白", YakuType.Hatsu => "役牌 发", YakuType.Chun => "役牌 中",
            YakuType.Pinfu => "平和", YakuType.Tanyao => "断幺九", YakuType.Iipeikou => "一杯口", YakuType.Ryanpeiko => "两杯口",
            YakuType.Ittsu => "一气通贯", YakuType.SanshokuDoujun => "三色同顺", YakuType.Chanta => "混全带幺九", YakuType.Junchan => "纯全带幺九",
            YakuType.SanshokuDoukou => "三色同刻", YakuType.Chiitoitsu => "七对子", YakuType.Toitoi => "对对和", YakuType.Honroutou => "混老头",
            YakuType.Sananko => "三暗刻", YakuType.Sankantsu => "三杠子", YakuType.ShoSanGen => "小三元",
            YakuType.Honitsu => "混一色", YakuType.Chinitsu => "清一色", YakuType.Suuankou => "四暗刻", YakuType.SuuankouTanki => "四暗刻单骑",
            YakuType.Tsuuiiso => "字一色", YakuType.DaiSanGen => "大三元", YakuType.ShoSuuShii => "小四喜", YakuType.DaiSuuShii => "大四喜",
            YakuType.Ryuiishoku => "绿一色", YakuType.Chinroto => "清老头", YakuType.Suukantsu => "四杠子", YakuType.Tenho => "天和", YakuType.Chiiho => "地和",
            YakuType.ChurenPoto => "九莲宝灯", YakuType.ChuurenPoutou => "纯正九莲宝灯", YakuType.KokushiMusou => "国士无双",
            YakuType.KokushiMusouThirteenOrphans => "国士无双十三面", YakuType.NagashiMangan => "流局满贯",
            YakuType.Dora => "宝牌", YakuType.AkaDora => "赤宝牌", YakuType.KitaDora => "拔北宝牌", YakuType.UraDora => "里宝牌", _ => ""
        };
    }
}
