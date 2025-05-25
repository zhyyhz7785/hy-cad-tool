using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass
{
    public class Rec
    {
        #region 属性
        public static double Scale { get; set; } = Reinforcement.Scale;
        public static RecT.MoveRecRangeStatus Range { get; set; } = RecT.MoveRecRangeStatus.EqualDistance;
        public static RecT.MovePrinciple MovePrinciple { get; set; } = RecT.MovePrinciple.Deviation;
        public static double RangeScale { get; set; } = 2;
        public static double MoveStep { get; set; } = 10;
        public static int RotateStepNumber { get; set; } = 18;
        public static Vector3d[] Directions
        {
            get { return GetDirectionRange(); }
        }
        public static double RangeExtendDistanceX { get; set; } = 100 * Scale;
        public static double RangeExtendDistanceY { get; set; } = 50 * Scale;
        public static Vector3d RangeLength { get; set; } = new Vector3d(80 * Scale, 80 * Scale, 0);
        public static double ExtendX { get; set; } = Scale * 1;
        public static double ExtendY { get; set; } = Scale * 1;
        public static bool IsMoveRecOneDirectionOk { get; set; } = false;
        public static bool IsNeedMove { get; set; } = true;
        public static bool IsMoveRecAllDirectionOk { get; set; } = false;
        public Polyline BasePoly { get; set; }
        public Polyline ExtendBasePoly
        {
            get
            {
                return BasePoly.GetExtendRecBothSideByXY(ExtendX, ExtendY);
            }
        }
        public Polyline[] Polys { get; set; }
        public Vector3d[] MovePolyVecs
        {
            get
            {
                var movePolyVecs = new Vector3d[Polys.Length];
                for (int i = 0; i < Polys.Length; i++)
                {
                    movePolyVecs[i] = GetBastMoveStopVector(Directions, ExtendBasePoly, RangePolys[i], Polys[i]);
                }
                return movePolyVecs;
            }
        }
        private Polyline[] _rangePolys;
        public Polyline[] RangePolys
        {
            get
            {
                return _rangePolys;
            }
            set { _rangePolys = value; }
        }
        #endregion
        /// <summary>
        /// 构造函数均相同，没办法用的重载，泛型没有基类
        /// 修改一个，其他的同样修改
        /// </summary>
        /// <param name="baseT"></param>
        /// <param name="t"></param>
        public Rec(Polyline baseRec, Polyline[] recs)
        {
            BasePoly = baseRec;
            Polys = recs;
            GetRangePoly();
        }
        public void GetRangePoly()
        {
            var polys = new List<Polyline>();
            foreach (var poly in Polys)
            {
                var tempPoly = new Polyline();
                switch (Range)
                {
                    case RecT.MoveRecRangeStatus.Scale:
                        tempPoly = poly.ScaleEntCopy(poly.GetRecCenterPoint(), RangeScale);
                        break;
                    case RecT.MoveRecRangeStatus.EqualDistance:
                        tempPoly = poly.GetExtendRecBothSideByXY(RangeExtendDistanceX, RangeExtendDistanceY);
                        break;
                    case RecT.MoveRecRangeStatus.Square:
                        tempPoly = poly.GetExtendSquare(RangeLength);
                        break;
                }
                polys.Add(tempPoly);
            }
            RangePolys = polys.ToArray();
        }
        public static Tuple<Vector3d, bool> GetMoveStopVector(Polyline baseRec, Polyline rangeRec,
            Polyline moveRec, double step, Vector3d direction)
        {
            var moveRecIn = moveRec.Clone() as Polyline;
            //如果两个Rec中心重叠，调整移动方向向右为默认状态
            if (direction == new Vector3d(0, 0, 0))
            {
                direction = new Vector3d(1, 0, 0);
            }
            var vecOut = new Vector3d();
            var stateBase = baseRec.GetRecBoundState(moveRecIn);
            // baseRec.ToSpace();
            var stateRange = rangeRec.GetRecBoundState(moveRecIn);
            // rangeRec.ToSpace();
            // moveRecIn.ToSpace();
            int i = 1;
            while ((stateBase == RecT.BoundStatus.In || stateBase == RecT.BoundStatus.Intersection)
                && stateRange == RecT.BoundStatus.In)
            {
                moveRecIn = moveRecIn.MoveEnt(direction * step);
                vecOut = i * direction * step;
                i++;
                stateBase = baseRec.GetRecBoundState(moveRecIn);
                stateRange = rangeRec.GetRecBoundState(moveRecIn);
            }
            //如果循环是因为Rec成功移出BaseRec,状态为成功
            if (stateBase == RecT.BoundStatus.Out)
            {
                IsMoveRecOneDirectionOk = true;
            }
            else
            {
                IsMoveRecOneDirectionOk = false;
            }
            var tv = new Tuple<Vector3d, bool>(vecOut, IsMoveRecOneDirectionOk);
            return tv;
        }
        public static Vector3d GetBastMoveStopVector(Vector3d[] directions,
            Polyline baseRec, Polyline rangeRec, Polyline moveRec)
        {
            // 判断是否有重叠，需要移动
            var moveRecIn = moveRec.Clone() as Polyline;
            var stateBase = baseRec.GetRecBoundState(moveRecIn);
            if (stateBase == RecT.BoundStatus.Out)
            {
                IsNeedMove = false;
                var vecNoMove = new Vector3d(0, 0, 0);
                return vecNoMove;
            }
            else
            {
                IsNeedMove = true;
                var dic = new Dictionary<Vector3d, bool>();
                foreach (var direction in directions)
                {
                    var tuple = GetMoveStopVector
                       (baseRec, rangeRec, moveRec, MoveStep, direction);
                    dic.Add(tuple.Item1, tuple.Item2);
                }
                if (dic.Values.Contains(true))
                {
                    IsMoveRecAllDirectionOk = true;
                    //var oksDic = dic.Where(x => x.Value == true) as Dictionary<Vector3d, bool>;
                    var oksDic = dic.Where(x => x.Value == true).ToArray();
                    var okDirection = oksDic.OrderBy(x => x.Key.Length).FirstOrDefault().Key;
                    //okDirection = GetAccurateVec(baseRec, moveRec, okDirection);
                    return okDirection;
                }
                else
                {
                    var a = dic.Keys.FirstOrDefault();
                    var falseDirection = dic.Keys.OrderBy(x => x.Length).FirstOrDefault();
                    return falseDirection;
                }
            }
        }
        public static Vector3d[] GetDirectionRange()
        {
            var vecs = new Vector3d[RotateStepNumber];
            for (int i = 0; i < RotateStepNumber; i++)
            {
                vecs[i] = Vector3d.XAxis.RotateBy(Math.PI * 2 * i / RotateStepNumber, Vector3d.ZAxis);
            }
            return vecs;
        }
      
    }
}
