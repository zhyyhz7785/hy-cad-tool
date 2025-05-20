using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        public static List<T> ConvertToEntities<T>(this IEnumerable<ObjectId> objectIds) where T : Entity
        {
            if (objectIds == null)
            {
                return new List<T>();
            }
            List<T> entities = new List<T>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in objectIds)
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity is T typedEntity)
                    {
                        entities.Add(typedEntity);
                    }
                }
                tr.Commit();
            }
            return entities;
        }
        /// <summary>
        /// 将 Entity 集合转换为 ObjectId 集合
        /// </summary>
        public static List<ObjectId> ConvertToObjectIds(this IEnumerable<Entity> entities)
        {
            if (entities == null)
            {
                return new List<ObjectId>();
            }
            List<ObjectId> objectIds = new List<ObjectId>();
            foreach (var entity in entities)
            {
                objectIds.Add(entity.ObjectId);
            }
            return objectIds;
        }
        /// <summary>
        /// 将单个 ObjectId 转换为 Entity
        /// </summary>
        public static Entity ConvertToEntity(this ObjectId objectId)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Entity entity = null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                entity = tr.GetObject(objectId, OpenMode.ForRead) as Entity;
                tr.Commit();
            }
            return entity;
        }
        /// <summary>
        /// 将单个 Entity 转换为 ObjectId
        /// </summary>
        public static ObjectId ConvertToObjectId(this Entity entity)
        {
            return entity.ObjectId;
        }
    }
}
