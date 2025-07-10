using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaJIangNet
{
    internal class LibTestProgram
    {
        public static int Main()
        {
            ShouPai shouPai = new ShouPai();
            FanCalculator.MatchInformation = new MatchInformation();
            shouPai.ShouPaiList = new()
            {
                new(Color.Honor,1),
                new(Color.Honor,1), 
                new(Color.Honor,1),
                new(Color.Honor,2),
                new(Color.Honor,2),
                new(Color.Honor,2),
                new(Color.Honor,3),
                new(Color.Honor,3),
                new(Color.Honor,3),
                new(Color.Honor,4),
                new(Color.Honor,4),
                new(Color.Honor,4),
                new(Color.Honor,5),
            };
            bool isTingPai = GlobalFunction.TingPaiJudge(shouPai, out Dictionary<Pai, List<Group>> successPais);
            Console.WriteLine(isTingPai);
            foreach (KeyValuePair<Pai, List<Group>> keyValuePair in successPais)
            {
                // 测试用例:玩家为1,
                HePaiData hePaiData = new(shouPai, keyValuePair.Key, true, true, keyValuePair.Value);
                RonPoint ronPoint = FanCalculator.RonPointCalculator(hePaiData);
                Console.Write(keyValuePair.Key.ToString() + " ");
                foreach (Group group in keyValuePair.Value)
                {
                    Console.Write(group.ToString() + " ");
                }
                Console.WriteLine();
                foreach (var fanData in ronPoint.FanDatas)
                {
                    Console.WriteLine($"{fanData.Yaku}:{fanData.Fan}");
                }
                Console.WriteLine($"Sum: Fan:{ronPoint.Fan}, Fu:{ronPoint.Fu}");
                Console.WriteLine();

            }
            return 0;
        }
    }
}
