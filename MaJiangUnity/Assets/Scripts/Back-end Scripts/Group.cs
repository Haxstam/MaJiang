using System;
using System.Collections.Generic;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// 面子分组,定义了最基础的面子/雀头的属性
    /// </summary>
    public class Group : IByteable<Group>
    {
        /// <summary>
        /// 非副露下的构造器,需要面子类型,花色,面子的牌列表
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">面子花色</param>
        /// <param name="pais">面子的牌</param>
        public Group(GroupType groupType, Color color, List<Pai> pais)
        {
            GroupType = groupType;
            Color = color;
            Pais = pais;
        }
        /// <summary>
        /// 副露的构造器,需要面子类型,花色,鸣牌来源及组成面子的手里的牌和别家的牌,在创建时Pais不包含所鸣之牌,创建后将副露牌添加进Pais的末尾
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">花色</param>
        /// <param name="pais">自己手中的牌的列表,输入要求不包含所鸣牌</param>
        /// <param name="player">所鸣牌的来源</param>
        /// <param name="singlePai">所鸣的牌</param>
        public Group(GroupType groupType, Color color, List<Pai> pais, int player, Pai singlePai)
        {
            GroupType = groupType;
            Color = color;
            Pais = pais;
            Pais.Add(singlePai);
            SinglePai = singlePai;
            FuluSource = player;
        }
        /// <summary>
        /// 面子类型
        /// </summary>
        public GroupType GroupType { get; set; }
        /// <summary>
        /// 组成面子的牌的列表
        /// </summary>
        public List<Pai> Pais { get; set; }
        /// <summary>
        /// 面子花色
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// 该面子的来源,门清的面子来源均为自己,如果是副露,则为其所鸣牌的来源玩家
        /// </summary>
        public int FuluSource { get; set; }
        /// <summary>
        /// 所鸣的单牌,仅用于副露下的面子
        /// </summary>
        public Pai SinglePai { get; set; }
        /// <summary>
        /// 在役种判断中对刻子的判断较繁琐,通过该属性简化,当为刻子或杠子时,返回True
        /// </summary>
        /// <returns></returns>
        public bool IsTriple
        {
            get
            {
                return GroupType == GroupType.Triple || GroupType == GroupType.MingTriple || GroupType == GroupType.AnKang || GroupType == GroupType.MingKang || GroupType == GroupType.JiaKang;
            }
        }
        public const int byteSize = 14;
        public int ByteSize { get => byteSize; }

        

        public override string ToString()
        {
            string str = "";
            if (GroupType == GroupType.Triple)
            {
                str = str + Pais[0].ToString() + Pais[0].ToString() + Pais[0].ToString();
            }
            else if (GroupType == GroupType.Pair)
            {
                str = str + Pais[0].ToString() + Pais[0].ToString();
            }
            else
            {
                str = str + Pais[0].ToString() + Pais[1].ToString() + Pais[2].ToString();
            }
            return str;
        }
        /// <summary>
        /// "=="重载,仅当两组面子完全相同,即花色,牌型和序号都相同时成立,对于吃牌,已知鸣牌两张中较小的那张和所鸣牌即可确定鸣牌
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Group a, Group b)
        {
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Pais[0] == b.Pais[0] && a.SinglePai == b.SinglePai)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// "!="重载
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(Group a, Group b)
        {
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Pais[0] == b.Pais[0])
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        /// <summary>
        /// 面子的序列化隐式转换
        /// </summary>
        /// <param name="group"></param>
        public static implicit operator byte[](Group group)
        {
            // 1 byte(GroupType) + 1 byte(Color) + 1 byte(FuluSource) + 1 byte 留白 + 2 bytes(SinglePai) + 8 bytes(Pais) = 14 bytes
            Span<byte> mainBytes = new byte[14];
            mainBytes[0] = (byte)group.GroupType;
            mainBytes[1] = (byte)group.Color;
            mainBytes[2] = (byte)group.FuluSource;
            ReplaceBytes(mainBytes, group.SinglePai, 4);
            ReplaceBytes(mainBytes, ListToBytes(group.Pais), 6);
            return mainBytes.ToArray();
        }
        public byte[] GetBytes() => this;
        public static Group StaticBytesTo(byte[] bytes, int index = 0)
        {
            byte[] shortBytes = new byte[14];
            Array.Copy(bytes, index, shortBytes, 0, 14);
            GroupType groupType = (GroupType)shortBytes[0];
            Color color = (Color)shortBytes[1];
            int fuluSource = shortBytes[2];
            
            Pai singlePai = Pai.StaticBytesTo(shortBytes, 4);
            List<Pai> pais = BytesToList<Pai>(shortBytes, 6, 4);
            return new(groupType, color, pais, fuluSource, singlePai);
        }
        public Group BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);
        public override bool Equals(object obj)
        {
            Group group = obj as Group;
            if (group != null)
            {
                return group == this;
            }
            else
            {
                return false;
            }
        }
        public override int GetHashCode()
        {
            HashCode listHash = new();
            foreach (Pai listPai in Pais)
            {
                listHash.Add(listPai.GetHashCode());
            }
            return HashCode.Combine(GroupType, listHash.ToHashCode(), Color, FuluSource, SinglePai);
        }
    }

}

