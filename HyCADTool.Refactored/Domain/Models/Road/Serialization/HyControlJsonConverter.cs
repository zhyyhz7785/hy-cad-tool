using System;
using HyCADTool.Refactored.Domain.Models.Road.ControlElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Refactored.Domain.Models.Road.Serialization
{
    /// <summary>
    /// <see cref="IHyControl"/> 的多态 JsonConverter（M6）。
    ///
    /// <para><b>协议</b></para>
    /// 每个控制体 JSON 对象必须含顶层字符串字段 <c>"Kind"</c>，取值与各子类的
    /// <c>KindConstant</c> 对齐：
    /// <list type="bullet">
    ///   <item><c>"Control.ReferencePoint"</c> → <see cref="ReferencePoint"/></item>
    ///   <item><c>"Control.ReferenceLine"</c> → <see cref="ReferenceLine"/></item>
    ///   <item><c>"Control.ReferencePlane"</c> → <see cref="ReferencePlane"/></item>
    ///   <item><c>"Control.SelectionSet"</c> → <see cref="SelectionSet"/></item>
    /// </list>
    ///
    /// <para><b>写</b></para>
    /// 直接委托回标准序列化（子类已带 <c>Kind</c> 字符串属性）。
    ///
    /// <para><b>读</b></para>
    /// 先读成 <see cref="JObject"/>，按 <c>Kind</c> 字段分派到具体类型；未知 <c>Kind</c> 时抛出
    /// <see cref="JsonSerializationException"/>（避免静默丢数据）。
    /// </summary>
    public sealed class HyControlJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(IHyControl).IsAssignableFrom(objectType);

        public override bool CanWrite => false; // 写直接走默认路径（各子类带 Kind 属性）

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var jo = JObject.Load(reader);
            string kind = jo["Kind"]?.ToString() ?? jo["kind"]?.ToString();

            if (string.IsNullOrEmpty(kind))
            {
                throw new JsonSerializationException(
                    "IHyControl JSON 对象缺少必填字段 'Kind'，无法多态反序列化。");
            }

            Type targetType = ResolveType(kind);
            if (targetType == null)
            {
                throw new JsonSerializationException(
                    $"未知 IHyControl Kind = '{kind}'，无法多态反序列化。");
            }

            object instance = Activator.CreateInstance(targetType);
            serializer.Populate(jo.CreateReader(), instance);
            return instance;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // CanWrite=false 时 Newtonsoft 不会调用本方法；保留默认委托。
            serializer.Serialize(writer, value);
        }

        private static Type ResolveType(string kind)
        {
            if (kind == ReferencePoint.KindConstant) return typeof(ReferencePoint);
            if (kind == ReferenceLine.KindConstant) return typeof(ReferenceLine);
            if (kind == ReferencePlane.KindConstant) return typeof(ReferencePlane);
            if (kind == SelectionSet.KindConstant) return typeof(SelectionSet);
            return null;
        }
    }
}
