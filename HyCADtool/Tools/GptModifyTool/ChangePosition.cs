using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Interop.Common;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        #region 图形位置变化
        public static ObjectId MirrorEntCom<T>(this Entity ent, Point3d a, Point3d b) where T : Entity
        {
            if (ent.Id == ObjectId.Null)
            {
                var entId = ent.ToSpace();
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                dynamic aent = ent.AcadObject;
                var mirror = aent.Mirror(a.ToArray(), b.ToArray());
                //var type = Microsoft.VisualBasic.Information.TypeName(a);
                var mirrorCom = (IAcadObject)mirror;
                var id = mirrorCom.ObjectID;
                var idN = new ObjectId((IntPtr)id);
                ent.ChangeEntityPropertyInDb(x => x.Erase());
                return idN;
            }
            else
            {
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                dynamic aent = ent.AcadObject;
                var mirror = aent.Mirror(a.ToArray(), b.ToArray());
                //var type = Microsoft.VisualBasic.Information.TypeName(a);
                var mirrorCom = (IAcadObject)mirror;
                var id = mirrorCom.ObjectID;
                var idN = new ObjectId((IntPtr)id);
                return idN;
            }
        }
        public static ObjectId[] MirrorEntSCom<T>(this IEnumerable<T> ents, Point3d a, Point3d b) where T : Entity
        {
            var listId = new List<ObjectId>();
            foreach (var ent in ents)
            {
                var id = MirrorEntCom<T>(ent, a, b);
                listId.Add(id);
            }
            return listId.ToArray();
        }
        public static T MirrorEnt<T>(this T tEnt, Point3d a, Point3d b) where T : Entity
        {
            if (tEnt.ObjectId != ObjectId.Null)
            {
                tEnt.ChangeEntityPropertyInDb(x =>
                {
                    var matrix3dm = Matrix3d.Mirroring(new Line3d(a, b));
                    tEnt.TransformBy(matrix3dm);
                });
                return tEnt;
            }
            else
            {
                var matrix3dm = Matrix3d.Mirroring(new Line3d(a, b));
                tEnt.TransformBy(matrix3dm);
                return tEnt;
            }
        }
        public static MText MirrorMText(this MText mtext, Point3d a, Point3d b)
        {
            //1 获取镜像后的边界框，及边界框中心线
            var poly = mtext.GetRecByText();
            poly = poly.MirrorEnt(a, b);
            var segM = poly.GetCenterLineFromRec()[0];
            var segN = poly.GetCenterLineFromRec()[1];
            //2 镜像文字
            mtext.MirrorEnt(a, b);
            //3 根据镜像线 判断文字如何镜像回去
            var axis = new LineSegment3d(a, b);
            {
                if (Math.Abs(axis.Direction.Y) < Math.Abs(axis.Direction.X))
                {
                    mtext.MirrorEnt(segM.StartPoint, segM.EndPoint);
                }
                else
                {
                    mtext.MirrorEnt(segN.StartPoint, segN.EndPoint);
                }
            }
            return mtext;
        }
        public static T MoveEnt<T>(this T tEnt, Vector3d vec3d) where T : Entity
        {
            if (tEnt.ObjectId != ObjectId.Null)
            {
                tEnt.ChangeEntityPropertyInDb(x =>
                {
                    var matrix3dd = Matrix3d.Displacement(vec3d);
                    tEnt.TransformBy(matrix3dd);
                });
                return tEnt;
            }
            else
            {
                var matrix3dd = Matrix3d.Displacement(vec3d);
                tEnt.TransformBy(matrix3dd);
                return tEnt;
            }
        }
        public static T[] MoveEnts<T>(this T[] tEnts, Vector3d[] vec3d) where T : Entity
        {
            for (int i = 0; i < tEnts.Length; i++)
            {
                tEnts[i] = tEnts[i].MoveEnt(vec3d[i]);
            }
            return tEnts;
        }
        public static T RotateEnt<T>(this T tEnt, Point3d basePoint, double angle) where T : Entity
        {
            if (tEnt.ObjectId != ObjectId.Null)
            {
                tEnt.ChangeEntityPropertyInDb(x =>
                {
                    var matrix3dr = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
                    tEnt.TransformBy(matrix3dr);
                });
                return tEnt;
            }
            else
            {
                var matrix3dr = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
                tEnt.TransformBy(matrix3dr);
                return tEnt;
            }
        }
        public static T ScaleEnt<T>(this T tEnt, Point3d basePoint, double scale) where T : Entity
        {
            if (tEnt.ObjectId != ObjectId.Null)
            {
                tEnt.ChangeEntityPropertyInDb(x =>
                {
                    Matrix3d matrix3ds = Matrix3d.Scaling(scale, basePoint); ;
                    tEnt.TransformBy(matrix3ds);
                });
                return tEnt;
            }
            else
            {
                Matrix3d matrix3ds = Matrix3d.Scaling(scale, basePoint); ;
                tEnt.TransformBy(matrix3ds);
                return tEnt;
            }
        }
        public static T MirrorEntCopy<T>(this T tEnt, Point3d a, Point3d b) where T : Entity
        {
            var matrix3dm = Matrix3d.Mirroring(new Line3d(a, b));
            tEnt = tEnt.GetTransformedCopy(matrix3dm) as T;
            return tEnt;
        }
        public static T MoveEntCopy<T>(this T tEnt, Vector3d vec3d) where T : Entity
        {
            var matrix3dd = Matrix3d.Displacement(vec3d);
            tEnt = tEnt.GetTransformedCopy(matrix3dd) as T;
            return tEnt;
        }
        public static T RotateEntCopy<T>(this T tEnt, Point3d basePoint, double angle) where T : Entity
        {
            var matrix3dr = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
            tEnt = tEnt.GetTransformedCopy(matrix3dr) as T;
            return tEnt;
        }
        public static T ScaleEntCopy<T>(this T tEnt, Point3d basePoint, double scale) where T : Entity
        {
            Matrix3d matrix3ds = Matrix3d.Scaling(scale, basePoint); ;
            tEnt = tEnt.GetTransformedCopy(matrix3ds) as T;
            return tEnt;
        }
        public static T[] MoveEntCopy<T>(this T tEnt, Vector3d vec3d, int times) where T : Entity
        {
            var list = new List<T>();
            for (int i = 0; i < times; i++)
            {
                var matrix3dd = Matrix3d.Displacement(vec3d);
                tEnt = tEnt.GetTransformedCopy(matrix3dd) as T;
                list.Add(tEnt);
            }
            return list.ToArray();
        }
        public static T[] RotateEntCopy<T>(this T tEnt, Point3d basePoint, double angle, int times) where T : Entity
        {
            var list = new List<T>();
            for (int i = 0; i < times; i++)
            {
                var matrix3dr = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
                tEnt = tEnt.GetTransformedCopy(matrix3dr) as T;
                list.Add(tEnt);
            }
            return list.ToArray();
        }
        public static T[] ScaleEntCopy<T>(this T tEnt, Point3d basePoint, double scale, int times) where T : Entity
        {
            var list = new List<T>();
            for (int i = 0; i < times; i++)
            {
                Matrix3d matrix3ds = Matrix3d.Scaling(scale, basePoint); ;
                tEnt = tEnt.GetTransformedCopy(matrix3ds) as T;
                list.Add(tEnt);
            }
            return list.ToArray();
        }
        #endregion
    }
}
