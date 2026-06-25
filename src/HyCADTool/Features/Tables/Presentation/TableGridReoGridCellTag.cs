using System;
using HyCAD.Tables.Structure;
using Newtonsoft.Json;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// ReoGrid <see cref="unvell.ReoGrid.Cell.Tag"/> 元数据：FieldKey / Role / anchor 地址。
    /// </summary>
    public sealed class TableGridReoGridCellTag
    {
        public TableGridReoGridCellTag(int row, int col, string fieldKey, CellRole? role)
        {
            Row = row;
            Col = col;
            FieldKey = fieldKey ?? string.Empty;
            Role = role.HasValue ? role.Value.ToString() : null;
        }

        [JsonProperty("r")]
        public int Row { get; }

        [JsonProperty("c")]
        public int Col { get; }

        [JsonProperty("fk", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FieldKey { get; }

        [JsonProperty("role", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Role { get; }

        public static TableGridReoGridCellTag FromAnchor(CellAddr anchor, string fieldKey, CellRole? role) =>
            new TableGridReoGridCellTag(anchor.Row, anchor.Col, fieldKey, role);

        public CellAddr ToAnchor() => new CellAddr(Row, Col);

        public string Serialize() => JsonConvert.SerializeObject(this);

        public static bool TryParse(object tag, out TableGridReoGridCellTag meta)
        {
            meta = null;
            if (tag == null)
                return false;

            if (tag is TableGridReoGridCellTag typed)
            {
                meta = typed;
                return true;
            }

            var text = tag as string ?? tag.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                meta = JsonConvert.DeserializeObject<TableGridReoGridCellTag>(text);
                return meta != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
