using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Interfaces
{
    /// <summary>
    /// 图层管理器接口
    /// 负责图层的创建、配置和管理
    /// </summary>
    public interface ILayerManager
    {
        /// <summary>
        /// 确保图层存在，如果不存在则创建
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">颜色索引</param>
        void EnsureLayer(Transaction tr, string layerName, short colorIndex);

        /// <summary>
        /// 确保图层存在（可指定锁定状态）。若图层已存在、当前锁定状态与 <paramref name="locked"/>
        /// 不一致，会就地 Upgrade 更新。颜色始终同步到 <paramref name="colorIndex"/>。
        /// </summary>
        void EnsureLayer(Transaction tr, string layerName, short colorIndex, bool locked);

        /// <summary>
        /// 批量创建图层
        /// </summary>
        void EnsureLayers(Transaction tr, params (string name, short colorIndex)[] layers);

        /// <summary>
        /// 临时解锁指定图层执行 <paramref name="body"/>（写入实体），执行完恢复原有锁定状态；
        /// 无论 body 抛异常与否都保证锁态恢复。若图层不存在，会以 <paramref name="locked"/>=true 的
        /// 默认状态创建（颜色 <paramref name="colorIndex"/>）。
        /// </summary>
        /// <param name="tr">共享事务</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="colorIndex">图层不存在时以此颜色创建（存在则忽略）</param>
        /// <param name="body">解锁期间执行的动作</param>
        void WithUnlocked(Transaction tr, string layerName, short colorIndex, Action body);
    }
}

