using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 承载力验算计算引擎（纯数学，平台无关）
    /// 复合地基：JGJ 79-2012 §7.1.5~7.1.6
    /// 桩基：JGJ 94-2008 §5.2~5.4
    /// </summary>
    public static class BearingCapacityService
    {
        #region 复合地基承载力 (JGJ 79 §7.1.5)

        /// <summary>
        /// 计算复合地基承载力验算
        /// </summary>
        public static BearingCapacityResult CalculateComposite(SettlementInput settlement, BearingCapacityInput input)
        {
            var result = new BearingCapacityResult();

            double d = input.CompPileDiameter;
            double s = input.CompPileSpacing;

            double ap = Math.PI / 4.0 * d * d;
            double up = Math.PI * d;

            // 面积置换率 m = d²/de²
            double de;
            switch (input.ArrangementType)
            {
                case 0: de = 1.05 * s; break;  // 等边三角形
                case 2: de = 1.13 * s; break;  // 矩形（简化取单间距）
                default: de = 1.13 * s; break;  // 正方形
            }
            double m = (d * d) / (de * de);

            // 单桩承载力 Ra（式 7.1.5-3）
            double sumQsiLi = 0;
            var rows = new List<SideResistanceRow>();
            foreach (var layer in input.CompSideResistance)
            {
                double product = layer.Qsi * layer.Thickness;
                sumQsiLi += product;
                rows.Add(new SideResistanceRow
                {
                    LayerName = layer.LayerName,
                    SoilType = layer.SoilType,
                    Thickness = layer.Thickness,
                    Qsi = layer.Qsi
                });
            }

            double qsk = up * sumQsiLi;
            double qpk = input.AlphaP * input.CompQp * ap;
            double ra = qsk + qpk;

            // 复合地基承载力 fspk（式 7.1.5-2）
            double fspk = input.Lambda * m * ra / ap + input.Beta * (1 - m) * input.Fsk;

            // 桩体强度验算（式 7.1.6-1）
            double requiredFcu = 4.0 * input.Lambda * ra / ap;
            bool fcuCheck = input.Fcu >= requiredFcu;

            result.Ra = Math.Round(ra, 1);
            result.Qsk = Math.Round(qsk, 1);
            result.Qpk = Math.Round(qpk, 1);
            result.Quk = Math.Round(ra, 1);
            result.Fspk = Math.Round(fspk, 1);
            result.RequiredFcu = Math.Round(requiredFcu, 1);
            result.ActualFcu = input.Fcu;
            result.FcuCheck = fcuCheck;
            result.Lambda = input.Lambda;
            result.Beta = input.Beta;
            result.AreaReplacementRatio = Math.Round(m, 4);
            result.Fsk = input.Fsk;
            result.AlphaP = input.AlphaP;
            result.QpValue = input.CompQp;
            result.SideResistanceRows = rows;
            result.BearingCheck = true;

            return result;
        }

        #endregion

        #region 桩基承载力 (JGJ 94 §5.2~5.4)

        /// <summary>
        /// 计算桩基承载力验算
        /// </summary>
        public static BearingCapacityResult CalculatePile(SettlementInput settlement, BearingCapacityInput input)
        {
            var result = new BearingCapacityResult();

            double d = settlement.PileDiameter;
            double ap = Math.PI / 4.0 * d * d;
            double u = Math.PI * d;

            // 单桩竖向极限承载力（式 5.3.5）
            double sumQsiLi = 0;
            var rows = new List<SideResistanceRow>();
            foreach (var layer in input.PileSideResistance)
            {
                double product = layer.Qsi * layer.Thickness;
                sumQsiLi += product;
                rows.Add(new SideResistanceRow
                {
                    LayerName = layer.LayerName,
                    SoilType = layer.SoilType,
                    Thickness = layer.Thickness,
                    Qsi = layer.Qsi
                });
            }

            double qsk = u * sumQsiLi;
            double qpk = input.PileQp * ap;
            double quk = qsk + qpk;

            // 承载力特征值（式 5.2.2）
            double ra = quk / input.SafetyFactor;

            // 群桩验算
            double nk = input.Nk;
            bool bearingCheck = nk <= 0 || nk <= ra;

            result.Ra = Math.Round(ra, 1);
            result.Qsk = Math.Round(qsk, 1);
            result.Qpk = Math.Round(qpk, 1);
            result.Quk = Math.Round(quk, 1);
            result.Nk = nk;
            result.BearingCheck = bearingCheck;
            result.QpValue = input.PileQp;
            result.SideResistanceRows = rows;

            return result;
        }

        #endregion
    }
}
