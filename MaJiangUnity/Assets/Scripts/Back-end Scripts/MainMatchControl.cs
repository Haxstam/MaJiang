using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;
/// <summary>
/// ���Ծֿ�����,�洢ϵͳ���������ͶԾ������Ϣ
/// </summary>
public class MainMatchControl : IMatchInformation
{
    /*
     *   1.�����齫���Ƶ�������:
     *  ���ƺ����������һ�Ų�����ɽĩȡһ�Ų�������,���ƶ�(10��)�̶�����
     *  Ϊ����ʵ�ַ���,��һ����Ǳ�����һ�Ŵ���������,�����б�ֻ���Ӳ�����
     *  
     *   2.���ڶԾ���Ϣ,Ŀǰ�趨�Ƕ���MainMatchControl,����һ����ȫת��ΪByte[]�ķ���,������ʹ�ô˷���
     *     �Ծ���Ϣ�ĸ����ǰ��յ�����Ϊ�ڱ��ص���������ȡ��,���������û���֮��ĶԾ���Ϣͬ���򾡿��ܱ���
     *     
     */

    /// <summary>
    /// ���Ծֿ������Զ�һ���ֵĹ�����,��Ҫָ���Ƿ��к챦��,�Ƿ�������
    /// </summary>
    /// <param name="matchType"></param>
    /// <param name="haveRedDora"></param>
    /// <param name="haveHonor"></param>
    public MainMatchControl(List<Player> playerList, MatchType matchType, bool haveRedDora, bool haveHonor)
    {
        MatchType = matchType;
        if (MatchType == MatchType.FourMahjongEast || MatchType == MatchType.FourMahjongSouth)
        {
            PlayerList = playerList;
            FourInit();
            // ���б�/��ֵ��ʼ��Ϊ��һ 0�����Ŀ������,��δ��ʼ���ƺ��ƿ�����
            Wind = WindType.East;
            Round = 1;
            Honba = 0;
            PlayerPoint = new() { 25000, 25000, 25000, 25000 };
            CurrentBankerIndex = 0;
            CurrentPlayerIndex = CurrentBankerIndex;
        }
        else
        {

        }
    }
    /// <summary>
    /// ���ɹ�����Ϣ�����ĵ�ǰ������Ϣ,�����û��˴���������Ϣʵ��
    /// </summary>
    /// <param name="playerInformation"></param>
    /// <param name="matchInformation"></param>
    public MainMatchControl(List<IPlayerInformation> playerInformation, IMatchInformation matchInformation)
    {
        PlayerList = new();
        foreach (IPlayerInformation player in playerInformation)
        {
            PlayerList.Add(new(player));
        }
        MatchType = matchInformation.MatchType;
        QiPaiList = matchInformation.QiPaiList;
        DoraList = matchInformation.DoraList;
        UraDoraList = matchInformation.UraDoraList;
        KangCount = matchInformation.KangCount;
        KangMark = matchInformation.KangMark;
        Wind = matchInformation.Wind;
        Round = matchInformation.Round;
        Honba = matchInformation.Honba;
        PlayerPoint = matchInformation.PlayerPoint;
        IsRiichi = matchInformation.IsRiichi;
        IsDoubleRiichi = matchInformation.IsDoubleRiichi;
        RemainPaiCount = matchInformation.RemainPaiCount;
        HaveIppatsu = matchInformation.HaveIppatsu;
        FirstCycleIppatsu = matchInformation.FirstCycleIppatsu;
        IsKang = matchInformation.IsKang;
        CurrentPlayerIndex = matchInformation.CurrentPlayerIndex;
        CurrentStageType = matchInformation.CurrentStageType;
        CurrentBankerIndex = matchInformation.CurrentBankerIndex;
        PlayerFuluList = matchInformation.PlayerFuluList;
    }
    public MainMatchControl()
    {

    }
    /// <summary>
    /// ����б�
    /// </summary>
    public List<Player> PlayerList { get; set; }
    /// <summary>
    /// ��¼��ǰ��������
    /// </summary>
    public MatchType MatchType { get; set; }
    /// <summary>
    /// ���ƶ�,�洢�����˵�����,��0����Ӧ�����Ϊ��һ�����׼�
    /// </summary>
    public List<List<Pai>> QiPaiList { get; set; }
    /// <summary>
    /// ��ǰ��չʾ�ı�����
    /// </summary>
    public int DoraCount { get; set; }
    /// <summary>
    /// ����ָʾ���б�,�洢�����ѱ�չʾ�ı���ָʾ��
    /// </summary>
    public List<Pai> DoraList { get; set; }
    /// <summary>
    /// �ﱦ���б�,��ʱ��������
    /// </summary>
    public List<Pai> UraDoraList { get; set; }
    /// <summary>
    /// Ŀǰ���ܵ�����
    /// </summary>
    public int KangCount { get; set; }
    /// <summary>
    /// ������ҵı��,ͨ���ñ����ж��Ƿ��ж����ҿ���,��ʼΪ6,��һλ��ҿ���ʱ�趨��Ϊ��ұ��,������������ҿ������趨Ϊ5,�����ж��ĸ�����
    /// </summary>
    public int KangMark { get; set; }
    /// <summary>
    /// ��ǰ�糡
    /// </summary>
    public WindType Wind { get; set; }
    /// <summary>
    /// ��ǰΪ�ڼ���
    /// </summary>
    public int Round { get; set; }
    /// <summary>
    /// ��ǰΪ������
    /// </summary>
    public int Honba { get; set; }
    /// <summary>
    /// ��ҵ���
    /// </summary>
    public List<int> PlayerPoint { get; set; }
    /// <summary>
    /// ʣ���Ƶ���Ŀ,�����жϺӵ׺��׵�
    /// </summary>
    public int RemainPaiCount { get; set; }
    /// <summary>
    /// ��ǰ�����лغϵ���ҵ����,��ǰ��ҽ����׶���Ϻ�,ת����һ��ҵ����ƽ׶�
    /// </summary>
    public int CurrentPlayerIndex { get; set; }
    /// <summary>
    /// ��ǰ�����лغϵ���ҵĽ׶�����
    /// </summary>
    public StageType CurrentStageType { get; set; }
    /// <summary>
    /// ��ǰ�ֵ�ׯ�����
    /// </summary>
    public int CurrentBankerIndex { get; set; }
    /// <summary>
    /// ������Ҹ�¶�Ƶ��б�,�����ж���������/�ĸ�ɢ�˵����
    /// </summary>
    public List<List<Group>> PlayerFuluList { get; set; }

    // ��������

    /// <summary>
    /// ��һѲ��ָʾ,�����ж�����ֱ��غ�,�����趨ΪTrue,�������ƻ�ׯ�������ƺ��趨ΪFalse
    /// </summary>
    public bool FirstCycleIppatsu { get; set; }
    /// <summary>
    /// ��¼�Ƿ���ֱ,��ֱ�߶�Ӧ��ŵ�ֵΪtrue
    /// </summary>
    public List<bool> IsRiichi { get; set; }
    /// <summary>
    /// ��¼�Ƿ�Ϊ����ֱ,�˱�����Ӧ���ΪTrueʱ,IsRiichi��Ӧ�����Ҳ����ΪTrue
    /// </summary>
    public List<bool> IsDoubleRiichi { get; set; }
    /// <summary>
    /// һ�����ж�,ĳ����ֱ���趨����Ŷ�Ӧ��ΪTrue,�������ƻ��䱾���ٴ��һ�ź��趨ΪFalse
    /// </summary>
    public List<bool> HaveIppatsu { get; set; }
    /// <summary>
    /// �����������Ƶ�״̬,�α��򿪸ܺ��趨ΪTrue,����ƺ��趨ΪFalse
    /// </summary>
    public List<bool> IsKang { get; set; }
    public Pai CurrentPai { get; set; }

    // �ڲ���Ϣ,����Ϊ�ӿڳ�Ա

    /// <summary>
    /// ��ɽ��һ��������ɽ�е�λ��,������ʱ��ȡ��Ӧ��ɽλ�õ���
    /// </summary>
    public int CurrentPaiIndex { get; set; }
    /// <summary>
    /// ��ɽ
    /// </summary>
    public List<Pai> MainPaiList { get; set; }
    /// <summary>
    /// ����,�㶨14��(����)/18��(����),ǰ10�Ź̶�Ϊ����/�ﱦ�ƶ�,��4��/8��Ϊ������
    /// </summary>
    public List<Pai> PrimePaiList { get; set; }
    /// <summary>
    /// ��һ�������Ƶı��
    /// </summary>
    public int PrimePaiIndex { get; set; }
    /// <summary>
    /// ��ӱ��Ƶķ���,����ݵ�ǰ������������������±���,�������
    /// </summary>
    /// <returns>�Ƿ�ɹ����</returns>
    public bool AddDora()
    {
        if (DoraCount < 5)
        {
            // ����С��5ʱ���
            DoraList.Add(PrimePaiList[2 * DoraCount]);
            UraDoraList.Add(PrimePaiList[2 * DoraCount + 1]);
            DoraCount++;
            return true;
        }
        else
        {
            return false;
        }
    }
    /// <summary>
    /// ����ʱ�����ƵĴ���,��������ж�,��������������
    /// </summary>
    /// <param name="player">���ܵ���ұ��</param>
    /// <returns>����������</returns>
    public Pai KangSolve(int player)
    {
        if (KangCount == 4)
        {
            throw new Exception("����:������4�ܵ�������ֽ��п��ܲ���");
        }
        KangCount++;
        IsKang[player] = true;
        Pai endPai = MainPaiList[-1];
        PrimePaiList.Add(endPai);
        MainPaiList.RemoveAt(-1);
        PrimePaiIndex++;

        bool isAdd = AddDora();

        if (isAdd)
        {
            UnityEngine.Debug.Log("����ӱ���");
        }
        else
        {
            UnityEngine.Debug.Log("��ӱ���ʧ��");
        }
        return PrimePaiList[PrimePaiIndex + 9];
    }
    /// <summary>
    /// �α�����,���˲�����AddDora()���KangSolve()��ͬ
    /// </summary>
    /// <param name="player">�α�����ұ��</param>
    /// <returns>����������</returns>
    public Pai BaBeiSolve(int player)
    {
        IsKang[player] = true;
        Pai endPai = MainPaiList[-1];
        PrimePaiList.Add(endPai);
        MainPaiList.RemoveAt(-1);
        PrimePaiIndex++;
        return PrimePaiList[PrimePaiIndex + 9];
    }
    /// <summary>
    /// ����ɽ���Ƶķ���,����ʣ���ƴ���0ʱ����
    /// </summary>
    /// <returns></returns>
    public Pai TakePai()
    {
        if (RemainPaiCount == 0)
        {
            throw new Exception("����:������ʣ��0����ʱ����ɽ����");
        }
        else
        {
            Pai pai = MainPaiList[CurrentPaiIndex];
            CurrentPaiIndex++;
            return pai;
        }
    }
    /// <summary>
    /// ��������ʼ�׶�ʱ���ƽ���Ϊ
    /// </summary>
    /// <returns></returns>
    public bool StartStageAction()
    {
        Pai singlePai = TakePai();
        Player currentPlayer = PlayerList[CurrentPlayerIndex];
        currentPlayer.ShouPai.SinglePai = singlePai;

        return true;
    }
    /// <summary>
    /// ���ƽ׶��ƽ���Ϊ,��ȡ��ҿɽ��еĲ��������������
    /// </summary>
    /// <returns></returns>
    public bool DiscardStageAction()
    {
        Player currentPlayer = PlayerList[CurrentPlayerIndex];
        // ͬ���趨��Һͱ��ֵ�ǰ����Ϊ���ƻ���
        CurrentStageType = StageType.DiscardStage;
        currentPlayer.CurrentStageType = StageType.DiscardStage;
        Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = SelfAvailableJudge(currentPlayer.ShouPai, currentPlayer.ShouPai.SinglePai, this);

        return false;
    }
    public bool ClaimStageAction()
    {
        return false;
    }


    ///// <summary>
    ///// ��ǰ�����������û��ɽ��еĲ���,��һׯ�����Ϊ0,ÿ�����ƶ����м��㲢���´��б������ж�
    ///// </summary>
    //public List<Dictionary<PlayerAction, List<PlayerActionData>>> CurrentPlayerActionDict { get; private set; } = new();
    ///// <summary>
    ///// ��ǰ�ȴ����Ƶ��û����Խ��еķǸ�¶����,ÿ���û�����ʱ����
    ///// </summary>
    //public Dictionary<PlayerAction, List<PlayerActionData>> CurrentPlayerSelfActionDict { get; private set; } = new();
    ///// <summary>
    ///// �û����Ʋ������·���,�����Ը��²����б�
    ///// </summary>
    ///// <returns></returns>
    //public void PlayerActionFullRefresh(Pai currentPai)
    //{
    //    foreach (Player player in PlayerList)
    //    {
    //        CurrentPlayerActionDict[player.PlayerNumber] = ClaimingAvailableJudge(player.ShouPai, this);
    //    }
    //}
    ///// <summary>
    ///// �û�����������·���,һ��ÿ�غ϶����뱻����
    ///// </summary>
    ///// <param name="currentPai"></param>
    //public void PlayerSelfActionRefresh(Pai currentPai)
    //{
    //    CurrentPlayerSelfActionDict = SelfAvailableJudge(PlayerList[CurrentPlayerIndex].ShouPai, currentPai, this);
    //}
    /// <summary>
    /// ����ͨ�ó�ʼ��:��������ɽ,��������,�������б��,����δ�ƿ����ƺͷַ���ʼ����
    /// </summary>
    public void FourInit()
    {
        // ��ɽ�趨:ͨ�������������ɽ,ȡǰ122����Ϊ����,��14����Ϊ����
        List<Pai> rawPaiList = RandomCardGenerator(out int randomNumber, true, true, true);
        PrimePaiIndex = 0;
        PrimePaiList = rawPaiList.GetRange(122, 14);
        rawPaiList.RemoveRange(122, 14);
        MainPaiList = rawPaiList;
        QiPaiList = new();
        DoraList = new();
        UraDoraList = new();
        IsRiichi = new() { false, false, false, false };
        IsDoubleRiichi = new() { false, false, false, false };
        IsKang = new() { false, false, false, false };
        HaveIppatsu = new() { false, false, false, false };
        PlayerFuluList = new();
        FirstCycleIppatsu = true;
        CurrentStageType = StageType.StartStage;
        CurrentPaiIndex = 0;
        RemainPaiCount = 122;
        KangCount = 0;
        KangMark = 0;
    }
    /// <summary>
    /// ����ׯ����ׯ״̬�µĳ�������
    /// </summary>
    public void FourNewHonba()
    {
        FourInit();
        Honba++;
    }
    /// <summary>
    /// ����ׯ����ׯʱ�ĳ�������
    /// </summary>
    public void FourNewRound()
    {
        FourInit();
        Honba = 0;
        if (Round == 4)
        {
            Wind++;
            Round = 0;
            CurrentBankerIndex = 0;
            CurrentPlayerIndex = CurrentBankerIndex;
        }
        else
        {
            Round++;
            CurrentBankerIndex++;
            CurrentPlayerIndex = CurrentBankerIndex;
        }
    }
    /// <summary>
    /// ��ʼǰ�����Ʒַ����ƿ�����
    /// </summary>
    public void FourBasePaiSend()
    {
        for (int i = 0; i< 4; i++)
        {
            for(int j = 0; j < 13; j++)
            {

            }
        }
    }
    /// <summary>
    /// ���ڱ�����Ϣ�й�����Ϣ�����л�,���ز������û�����512Bytes�ֽڴ�
    /// </summary>
    /// <returns></returns>
    public byte[] GetPublicBytes()
    {
        // �Ծ���Ϣ,ʹ�� 512 bytes �ռ�,��������ɽ,Ҳ���������û��˻��õ�����Ϣ
        // ǰ16λ,Ϊ���ֶ̱���
        byte[] MainBytes = new byte[512];
        MainBytes[0] = (byte)MatchType;
        MainBytes[1] = (byte)Wind;
        MainBytes[2] = (byte)Round;
        MainBytes[3] = (byte)Honba;
        MainBytes[4] = (byte)KangCount;
        MainBytes[5] = (byte)KangMark;
        MainBytes[6] = (byte)CurrentPlayerIndex;
        MainBytes[7] = (byte)CurrentStageType;
        MainBytes[8] = (byte)CurrentBankerIndex;
        MainBytes[9] = (byte)RemainPaiCount;
        MainBytes[10] = BitConverter.GetBytes(FirstCycleIppatsu)[0];
        // ����б� 16~47
        ReplaceBytes(MainBytes, ListToBytes(PlayerPoint), 16);  // +16
        ReplaceBytes(MainBytes, ListToBytes(IsRiichi), 32); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsDoubleRiichi), 36); // +4
        ReplaceBytes(MainBytes, ListToBytes(HaveIppatsu), 40); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsKang), 44); // +4
        // ���ƶ� 48~239
        // ���ƶ�Ŀǰ�趨������ҵ����ƶѴ�СΪ24����,���ƶ��ܴ�С96����,Ҳ�� 192 bytes,������ҵ����ƶ�Ϊ 48 bytes
        // ��������,�����齫ׯ��������ֱ���������12��(�����ӼҸ�4��)��Ҫ24�ι���(�ļ��ܹ�),ʣ��48����ׯ���������12��,Ҳ�����24�Ź���
        // ʵ�����ƴ���20�ž��㹻����
        for (int i = 0; i < QiPaiList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(QiPaiList[i]), 48 + i * 48);
        }
        // ���� 240~259
        // ���ﱦ�ƹ�10�� 20 bytes
        ReplaceBytes(MainBytes, ListToBytes(DoraList), 240);
        ReplaceBytes(MainBytes, ListToBytes(UraDoraList), 250);
        // ��¶�б� 260~484
        // �洢�ļҹ�16��Group,Ҳ�� 224 bytes,������ҵĸ�¶�б�Ϊ 56 bytes
        for (int i = 0; i < PlayerFuluList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(PlayerFuluList[i]), 260 + i * 56);
        }
        // Ŀǰ484~511 ����
        return MainBytes;
    }
    public static MainMatchControl PublicBytesTo(byte[] bytes, int index = 0)
    {
        byte[] shortBytes = new byte[512];
        Array.Copy(bytes, index, shortBytes, 0, 512);
        MainMatchControl mainMatchControl = new();
        // 0-15
        mainMatchControl.MatchType = (MatchType)shortBytes[0];
        mainMatchControl.Wind = (WindType)shortBytes[1];
        mainMatchControl.Round = shortBytes[2];
        mainMatchControl.Honba = shortBytes[3];
        mainMatchControl.KangCount = shortBytes[4];
        mainMatchControl.KangMark = shortBytes[5];
        mainMatchControl.CurrentPlayerIndex = shortBytes[6];
        mainMatchControl.CurrentStageType = (StageType)shortBytes[7];
        mainMatchControl.CurrentBankerIndex = shortBytes[8];
        mainMatchControl.RemainPaiCount = shortBytes[9];
        mainMatchControl.FirstCycleIppatsu = BitConverter.ToBoolean(shortBytes, 10);
        // 16-47
        mainMatchControl.PlayerPoint = BytesToIntList(shortBytes, 16, 4);
        mainMatchControl.IsRiichi = BytesToBoolList(shortBytes, 32, 4);
        mainMatchControl.IsDoubleRiichi = BytesToBoolList(shortBytes, 36, 4);
        mainMatchControl.HaveIppatsu = BytesToBoolList(shortBytes, 40, 4);
        mainMatchControl.IsKang = BytesToBoolList(shortBytes, 44, 4);
        // 48-239,260-484

        mainMatchControl.QiPaiList = new();
        mainMatchControl.PlayerFuluList = new();
        if (mainMatchControl.MatchType == MatchType.FourMahjongEast || mainMatchControl.MatchType == MatchType.FourMahjongSouth)
        {
            for (int i = 0; i < 4; i++)
            {
                mainMatchControl.QiPaiList.Add(BytesToList<Pai>(shortBytes, 48 + i * 48, 24));
                mainMatchControl.PlayerFuluList.Add(BytesToList<Group>(shortBytes, 260 + i * 56, 4));
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                mainMatchControl.QiPaiList.Add(BytesToList<Pai>(shortBytes, 48 + i * 48, 24));
                mainMatchControl.PlayerFuluList.Add(BytesToList<Group>(shortBytes, 260 + i * 56, 4));
            }
        }
        mainMatchControl.DoraList = BytesToList<Pai>(shortBytes, 240, 5);
        mainMatchControl.UraDoraList = BytesToList<Pai>(shortBytes, 250, 5);

        return mainMatchControl;
    }

    //public static implicit operator byte[](MainMatchControl mainMatchControl)
    //{
    //    // �Ծ���Ϣ,ʹ�� 864 bytes �ռ�,��������ɽ,Ҳ���������û��˻��õ�����Ϣ
    //    // ǰ16λ,Ϊ���ֶ̱���
    //    byte[] MainBytes = new byte[864];
    //    MainBytes[0] = (byte)mainMatchControl.MatchType;
    //    MainBytes[1] = (byte)mainMatchControl.Wind;
    //    MainBytes[2] = (byte)mainMatchControl.Round;
    //    MainBytes[3] = (byte)mainMatchControl.Honba;
    //    MainBytes[4] = (byte)mainMatchControl.KangCount;
    //    MainBytes[5] = (byte)mainMatchControl.KangMark;
    //    MainBytes[6] = (byte)mainMatchControl.CurrentPlayerIndex;
    //    MainBytes[7] = (byte)mainMatchControl.CurrentStageType;
    //    MainBytes[8] = (byte)mainMatchControl.CurrentBankerIndex;
    //    MainBytes[9] = (byte)mainMatchControl.RemainPaiCount;
    //    MainBytes[10] = BitConverter.GetBytes(mainMatchControl.FirstCycleIppatsu)[0];
    //    // ����б� 16~47
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.PlayerPoint), 16);  // +16
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsRiichi), 32); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsDoubleRiichi), 36); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.HaveIppatsu), 40); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsKang), 44); // +4
    //    // ���ƶ� 48~431
    //    // ���ƶ�Ŀǰ�趨������ҵ����ƶѴ�СΪ24����,���ƶ��ܴ�С96����,Ҳ�� 384 bytes,������ҵ����ƶ�Ϊ 96 bytes
    //    // ��������,�����齫ׯ��������ֱ���������12��(�����ӼҸ�4��)��Ҫ24�ι���(�ļ��ܹ�),ʣ��48����ׯ���������12��,Ҳ�����24�Ź���
    //    // ʵ�����ƴ���20�ž��㹻����
    //    for (int i = 0; i < mainMatchControl.QiPaiList.Count; i++)
    //    {
    //        ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.QiPaiList[i]), 48 + i * 96);
    //    }
    //    // ���� 432~471
    //    // ���ﱦ�ƹ�10�� 40 bytes
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.DoraList), 432);
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.UraDoraList), 452);
    //    // ��¶�б� 472~855
    //    // �洢�ļҹ�16��Group,Ҳ�� 384 bytes,������ҵĸ�¶�б�Ϊ 96 bytes
    //    for (int i = 0; i < mainMatchControl.PlayerFuluList.Count; i++)
    //    {
    //        ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.PlayerFuluList[i]), 472 + i * 96);
    //    }
    //    // Ŀǰ856~863 ����
    //    return new byte[864];
    //}
}
