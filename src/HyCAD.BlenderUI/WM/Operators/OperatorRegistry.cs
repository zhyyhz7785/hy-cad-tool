using System;
using System.Collections.Generic;

namespace HyCAD.BlenderUI.WM.Operators
{
    public static class OperatorRegistry
    {
        private static readonly Dictionary<string, Func<Operator>> _map =
            new Dictionary<string, Func<Operator>>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string id, Func<Operator> factory)
        {
            if (string.IsNullOrEmpty(id) || factory == null) return;
            _map[id] = factory;
        }

        public static bool TryCreate(string id, out Operator op)
        {
            op = null;
            if (!_map.TryGetValue(id, out var f)) return false;
            op = f();
            return op != null;
        }
    }
}
