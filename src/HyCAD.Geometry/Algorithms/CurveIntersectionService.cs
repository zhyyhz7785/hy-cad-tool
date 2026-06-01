using System.Collections.Generic;
using System.Linq;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// 曲线交点计算服务 - Domain 层（平台无关）
    /// 
    /// 职责：
    /// 1. 管理曲线交点参数的收集和组织
    /// 2. 计算分割参数序列
    /// 3. 提供平台无关的交点数据结构
    /// </summary>
    public class CurveIntersectionService
    {
        /// <summary>
        /// 曲线交点数据
        /// </summary>
        public class CurveIntersectionData
        {
            /// <summary>
            /// 曲线索引
            /// </summary>
            public int CurveIndex { get; set; }

            /// <summary>
            /// 交点参数列表
            /// </summary>
            public List<double> Parameters { get; set; } = new List<double>();

            /// <summary>
            /// 曲线起始参数
            /// </summary>
            public double StartParam { get; set; }

            /// <summary>
            /// 曲线结束参数
            /// </summary>
            public double EndParam { get; set; }

            /// <summary>
            /// 是否有交点
            /// </summary>
            public bool HasIntersections => Parameters.Count > 0;
        }

        /// <summary>
        /// 参数段
        /// </summary>
        public class ParameterSegment
        {
            public double StartParam { get; set; }
            public double EndParam { get; set; }
            public double Length => System.Math.Abs(EndParam - StartParam);
        }

        private readonly double _tolerance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tolerance">几何容差</param>
        public CurveIntersectionService(double tolerance = 1e-9)
        {
            _tolerance = tolerance;
        }

        /// <summary>
        /// 创建交点数据
        /// </summary>
        public CurveIntersectionData CreateIntersectionData(int curveIndex, double startParam, double endParam)
        {
            return new CurveIntersectionData
            {
                CurveIndex = curveIndex,
                StartParam = startParam,
                EndParam = endParam
            };
        }

        /// <summary>
        /// 添加交点参数
        /// </summary>
        public void AddIntersectionParameter(CurveIntersectionData data, double parameter)
        {
            if (!double.IsNaN(parameter))
            {
                data.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// 计算分割参数序列
        /// </summary>
        /// <param name="data">交点数据</param>
        /// <returns>排序后的参数序列</returns>
        public List<double> CalculateSplitParameters(CurveIntersectionData data)
        {
            var parameters = new List<double>(data.Parameters);

            // 添加起始和结束参数
            parameters.Add(data.StartParam);
            parameters.Add(data.EndParam);

            // 去重并排序
            return parameters.Distinct().OrderBy(p => p).ToList();
        }

        /// <summary>
        /// 生成参数段列表
        /// </summary>
        /// <param name="sortedParameters">排序后的参数列表</param>
        /// <returns>参数段列表</returns>
        public List<ParameterSegment> GenerateParameterSegments(List<double> sortedParameters)
        {
            var segments = new List<ParameterSegment>();

            for (int i = 0; i < sortedParameters.Count - 1; i++)
            {
                double paramStart = sortedParameters[i];
                double paramEnd = sortedParameters[i + 1];

                // 检查参数差是否大于容差
                if (System.Math.Abs(paramEnd - paramStart) > _tolerance)
                {
                    segments.Add(new ParameterSegment
                    {
                        StartParam = paramStart,
                        EndParam = paramEnd
                    });
                }
            }

            return segments;
        }

        /// <summary>
        /// 检查参数是否在范围内
        /// </summary>
        public bool IsParameterInRange(double parameter, double startParam, double endParam)
        {
            return parameter >= startParam - _tolerance &&
                   parameter <= endParam + _tolerance;
        }
    }
}

