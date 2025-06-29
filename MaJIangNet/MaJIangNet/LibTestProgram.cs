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
            shouPai.ShouPaiList = new()
            {
                new(Color.Wans,1),
                new(Color.Wans,1),
                new(Color.Wans,3),
                new(Color.Wans,3),
                new(Color.Wans,2),
                new(Color.Wans,2),
                new(Color.Wans,7),
                new(Color.Wans,7),
                new(Color.Wans,9),
                new(Color.Wans,9),
                new(Color.Tungs,9),
                new(Color.Tungs,9),
                new(Color.Tungs,3),
            };
            bool isTingPai = GlobalFunction.TingPaiJudge(shouPai, out Dictionary<Pai, List<Group>> successPais);
            Console.WriteLine(isTingPai);
            foreach (KeyValuePair<Pai, List<Group>> keyValuePair in successPais)
            {
                Console.Write(keyValuePair.Key.ToString() + " ");
                foreach (Group group in keyValuePair.Value)
                {
                    Console.Write(group.ToString() + " ");
                }
                Console.WriteLine();
            }
            return 0;
        }
    }
}
