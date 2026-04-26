using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Metadata
{
    public sealed class RulePropertyDescriptor
    {
        public string EntityType { get; set; }

        public string PropertyName { get; set; }

        public string DisplayName { get; set; }

        public string PropertyType { get; set; }

        public string Category { get; set; }

        public RulePropertyPriority Priority { get; set; }

        public Func<Entity, object> ValueAccessor { get; set; }

        public bool IsDefault => Priority <= RulePropertyPriority.Style;

        public object GetValue(Entity entity)
        {
            if (entity == null || ValueAccessor == null)
                return null;

            return ValueAccessor(entity);
        }
    }
}
