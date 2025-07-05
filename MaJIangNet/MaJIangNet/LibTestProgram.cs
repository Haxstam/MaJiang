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
                new(Color.Wans,2),
                new(Color.Wans,3),
                new(Color.Wans,4),
                new(Color.Bamboo,5),
                new(Color.Bamboo,6),
                new(Color.Bamboo,7),
                new(Color.Tungs,4),
                new(Color.Tungs,5),
                new(Color.Tungs,1),
                new(Color.Tungs,2),
                new(Color.Tungs,3),
                new(Color.Wans,9),
                new(Color.Wans,9),
            };
            bool isTingPai = GlobalFunction.TingPaiJudge(shouPai, out Dictionary<Pai, List<Group>> successPais);
            Console.WriteLine(isTingPai);
            foreach (KeyValuePair<Pai, List<Group>> keyValuePair in successPais)
            {
                HePaiData hePaiData = new(1, shouPai, keyValuePair.Key, true, true, keyValuePair.Value);
                RonPoint ronPoint = FanCalculator.RonPointCalculator(hePaiData);
                Console.Write(keyValuePair.Key.ToString() + " ");
                foreach (Group group in keyValuePair.Value)
                {
                    Console.Write(group.ToString() + " ");
                }
                Console.WriteLine($"  Fan:{ronPoint.Fan}, Fu:{ronPoint.Fu}");
                Console.WriteLine();

            }
            return 0;
        }
    }
}
