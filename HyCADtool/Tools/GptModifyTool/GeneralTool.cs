using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        #region enum
        public enum DimensionFor
        {
            ForLeft,
            ForRight,
            ForUp,
            ForDown,
        }
        public enum Angle
        {
            First,
            Second,
            Third,
            Fourth
        }
        //得到随机的enum
        public static T GetRandomEnum<T>() where T : Enum, new()
        {
            T[] ts = Enum.GetValues(typeof(T)) as T[];
            Random random = new Random();
            T t = ts[random.Next(0, ts.Length)];
            return t;
        }
        #endregion
        #region 文件相关操作
        public static List<Point2d> GetFilePoints(string path)
        {
            List<Point2d> points = new List<Point2d>();
            //string path = @"C:\Users\40578\Desktop\bb.csv";
            string[] contents = File.ReadAllLines(path);
            int i = -1;
            foreach (var item in contents)
            {
                i++;
                string[] str = item.Split(',');
                bool a = double.TryParse(str[0], out double x);
                bool b = double.TryParse(str[1], out double y);
                if (a == false || b == false)
                {
                    Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage("转换失败，请检查数据格式");
                    return null;
                }
                else
                {
                    Point2d point = new Point2d(x, y);
                    points.Add(point);
                }
            }
            return points;
        }
        #endregion
        #region Linq
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
        //使用  var query = people.DistinctBy(p => new { p.Id, p.Name });
        //使用  var query = people.DistinctBy(p => p.Id);
        /// <summary>
        /// 嵌套数组拍平
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tSourse"></param>
        /// <returns></returns>
        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> tSourse)
        {
            var list = new List<T>();
            foreach (var ts in tSourse)
            {
                list = list.Concat(ts).ToList();
            }
            return list;
        }
        /// <summary>
        /// 找到groupby 结果中数量的排序，最少的是First，最多的是Last
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="groups"></param>
        /// <returns></returns>
        public static Dictionary<TKey, int> GetGroupCount<TKey, TSource>(this IEnumerable<IGrouping<TKey, TSource>> groups)
        {
            Dictionary<TKey, int> dic = new Dictionary<TKey, int>();
            foreach (IGrouping<TKey, TSource> Group in groups)
            {
                var count = Group.Count();
                dic.Add(Group.Key, count);
            }
            dic = dic.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            return dic;
        }
        #endregion
        #region 数组
        /// <summary>
        /// 把短长度数组变成想要长度数组，中间是空值，两头相同
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourseShort"></param>
        /// <param name="countMax"></param>
        /// <returns></returns>
        public static T[] Fillarray<T>(this T[] sourseShort, int countMax)
        {
            var count = sourseShort.Length;
            var values = new T[countMax];
            if (count % 2 == 0)
            {
                for (int i = 0; i < count / 2; i++)
                {
                    values[i] = sourseShort[i];
                    values[countMax - 1 - i] = sourseShort[count - 1 - i];
                }
            }
            else
            {
                int i = 0;
                for (i = 0; i < count / 2; i++)
                {
                    values[i] = sourseShort[i];
                    values[countMax - 1 - i] = sourseShort[count - 1 - i];
                }
                values[i] = sourseShort[i];
            }
            return values;
        }
        /// <summary>
        /// 把嵌套数组，转换为2维数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourse"></param>
        /// <returns></returns>
        public static T[,] TransformArrayTo2d<T>(this T[][] sourse)
        {
            var values = new T[sourse.Length, sourse[0].Length];
            for (int i = 0; i < sourse.Length; i++)
            {
                for (int j = 0; j < sourse[0].Length; j++)
                {
                    values[i, j] = sourse[i][j];
                }
            }
            return values;
        }
        /// <summary>
        /// 拍平嵌套数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourse"></param>
        /// <returns></returns>
        public static T[] Array2ToArray<T>(this T[][] sourse)
        {
            var list = new List<T>();
            foreach (var items in sourse)
            {
                foreach (var item in items)
                {
                    list.Add(item);
                }
            }
            return list.ToArray();
        }
        public static T[] GetArrayByIndexs<T>(this T[] ts, int[] ints)
        {
            var nts = new T[ints.Length];
            for (int i = 0; i < ints.Length; i++)
            {
                nts[i] = ts[ints[i]];
            }
            return nts;
        }
        #region 别人的代码
        static void Main(string[] args)
        {
            double[] a = { 1, 2, 3, 4, 5, 6 };
            double[,] b = Row2VecD(a);
            double[,] c = Row2ArrT(a, 2);
            //double[,] c = RowToArrD(a, 2);
            double[] d = Arr2RowD(c);
            Console.WriteLine("\n行向量→列向量");
            for (int i = 0; i < b.GetLength(0); i++)
            {
                for (int j = 0; j < b.GetLength(1); j++)
                {
                    Console.WriteLine("b[{0},{1}]：{2}", i, j, b[i, j]);
                }
            }
            Console.WriteLine("\n\n行向量→数组");
            for (int i = 0; i < c.GetLength(0); i++)
            {
                for (int j = 0; j < c.GetLength(1); j++)
                {
                    Console.WriteLine("c[{0},{1}]：{2}", i, j, c[i, j]);
                }
            }
            Console.WriteLine("\n\n数组→行向量");
            for (int i = 0; i < d.Length; i++)
            {
                Console.WriteLine("d[{0}]：{1}", i, d[i]);
            }
            Console.ReadKey();
        }
        //行向量→数组（内存复制版）
        static double[,] RowToArrD(double[] src, int row)
        {
            if (src.Length % row != 0) return null;
            int col = src.Length / row;
            double[,] dst = new double[row, col];
            for (int i = 0; i < row; i++)
            {
                //说明：“二维数组”【顺序储存】<=>“一维数组”
                Buffer.BlockCopy(src, i * col * sizeof(double),
                    dst, i * col * sizeof(double), col * sizeof(double));
            }
            return dst;
        }
        //行向量→列向量（内存复制版）
        static double[,] Row2VecD(double[] src)
        {
            double[,] dst = new double[src.Length, 1];
            Buffer.BlockCopy(src, 0, dst, 0, sizeof(double) * src.Length);
            return dst;
        }
        //行向量→数组（循环版；泛型版）
        static T[,] Row2ArrT<T>(T[] vec, int row)
        {
            if (vec.Length % row != 0) return null;
            int col = vec.Length / row;
            T[,] ret = new T[row, col];
            for (int i = 0; i < vec.Length; i++)
            {
                ret[i / col, i % col] = vec[i];
            }
            return ret;
        }
        //数组→行向量（内存复制版）
        static double[] Arr2RowD(double[,] src)
        {
            int elem = src.GetLength(0) * src.GetLength(1);
            double[] dst = new double[elem];
            Buffer.BlockCopy(src, 0, dst, 0, elem * sizeof(double));
            return dst;
        }
        #endregion
        #endregion
    }
}
