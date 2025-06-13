using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace MaJiangLib.Utility
{
    public static class ReflectUtility
    {
        /// <summary>
        /// 获取某个属性和对应的类型
        /// </summary>
        /// <param name="cache">是否进行缓存</param>
        /// <typeparam name="T">要查找的Attribute</typeparam>
        /// <returns></returns>
        public static IEnumerable<(Type,T)> GetAttributes<T>(bool cache=false) where T : Attribute
        {

            Assembly asm = Assembly.GetAssembly(typeof(T));
            Type[] types = asm.GetExportedTypes();
            List<(Type,T)> list = new List<(Type,T)>();
            foreach (var type in types)
            {
                var temp= type.GetCustomAttribute<T>();
                if (temp!=null)
                {
                    list.Add((type,temp));
                }
            }
            return list;
        }
    }
}