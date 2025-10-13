using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.ValueObjects.Grid
{
    /// <summary>
    /// 行列值对象（Row Column Value Object）
    /// 表示网格中某个单元格的位置和值
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    public class RowColValue<T>
    {
        /// <summary>
        /// 行索引（Row Index）
        /// </summary>
        public int Row { get; }

        /// <summary>
        /// 列索引（Column Index）
        /// </summary>
        public int Col { get; }

        /// <summary>
        /// 值（Value）
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="row">行索引</param>
        /// <param name="col">列索引</param>
        /// <param name="value">值</param>
        public RowColValue(int row, int col, T value)
        {
            Row = row;
            Col = col;
            Value = value;
        }

        /// <summary>
        /// 判断两个 RowColValue 是否相等
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is RowColValue<T> other)
            {
                return Row == other.Row
                    && Col == other.Col
                    && EqualityComparer<T>.Default.Equals(Value, other.Value);
            }
            return false;
        }

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Col, Value);
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"Row: {Row}, Col: {Col}, Value: {Value}";
        }
    }
}

