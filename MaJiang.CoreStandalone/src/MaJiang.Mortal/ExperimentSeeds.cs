using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MaJiang.Mortal
{
    /// <summary>将批量实验编号稳定映射为32字节种子，避免逐字节截断造成每256场重复。</summary>
    public static class ExperimentSeeds
    {
        public const string EncodingVersion = "sha256-v1";

        public static byte[] FromNumber(long number) => SHA256.HashData(Encoding.UTF8.GetBytes(
            "MaJiang.Experiment/" + EncodingVersion + "/" + number.ToString(CultureInfo.InvariantCulture)));
    }
}
