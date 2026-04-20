using System;
using System.Collections.Generic;

namespace HyCADTool.ReCall
{
    /// <summary>
    /// 道路类 <c>hyRoad*</c> 命令在 <see cref="CommandFacade"/> 中注册的 <c>r*</c> 短别名，供面板等 UI 与正式命令名一并展示。
    /// 与 CommandFacade 道路区 <c>[CommandMethod]</c> 双注册保持同步。
    /// </summary>
    public static class RoadCommandShortAliases
    {
        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hyRoad"] = "rCx",
                ["hyRoadA"] = "rLa",
                ["hyRoadAw"] = "rLaw",
                ["hyRoadAlnByPi"] = "rPi",
                ["hyRoadAlnEditPi"] = "rWk",
                ["hyRoadAlnStation"] = "rSt",
                ["hyRoadAlnTable"] = "rSe",
                ["hyRoadAlnGeomPt"] = "rGp",
                ["hyRoadAlnExportPi"] = "rEp",
                ["hyRoadAlnExportFrame"] = "rEf",
                ["hyRoadAlnDefaults"] = "rDd",
                ["hyRoadAlnInsertPi"] = "rIns",
                ["hyRoadAlnDeletePi"] = "rDp",
                ["hyRoadAlnStaEq"] = "rEq",
                ["hyRoadAlnReverse"] = "rRv",
                ["hyRoadAlnOffset"] = "rOf",
                ["hyRoadAlnUserPickRegister"] = "rLap",
                ["hyRoadAlnCommit"] = "rLac",
                ["hyRoadAlnExportXml"] = "rXo",
                ["hyRoadAlnImportXml"] = "rXi",
                ["hyRoadIntersection"] = "rIs",
                ["hyRoadIntersectionEdit"] = "rIe",
                ["hyRoadIntersectionKerbChain"] = "rIk",
                ["hyRoadIntersectionCrosswalk"] = "rIw",
                ["hyRoadCurbRamp"] = "rCr",
                ["hyRoadTactilePaving"] = "rTp",
                ["hyRoadStopLine"] = "rSl",
                ["hyRoadLaneMarking"] = "rLm",
                ["hyRoadArrow"] = "rAr",
                ["hyRoadP"] = "rPr",
                ["hyRoadProfFG"] = "rFg",
                ["hyRoadProfEG"] = "rEg",
                ["hyRoadProfLabel"] = "rPl",
                ["hyRoadT"] = "rT1",
                ["hyRoadCs"] = "rCs",
                ["hyRoadCsLoad"] = "rCsL",
                ["hyRoadCsQuick"] = "rCsQ",
                ["hyRoadC"] = "rCo",
                ["hyRoadSave"] = "rSv",
                ["hyRoadLoad"] = "rLd",
                ["hyRoad3dExportGltf"] = "rGf",
            };

        /// <summary>若存在短别名则返回 <c>正式名 + 两个空格 + 短名</c>，否则返回正式名。</summary>
        public static string FormatKeyWithShort(string commandKey)
        {
            if (string.IsNullOrEmpty(commandKey)) return commandKey;
            return Map.TryGetValue(commandKey, out var sh) ? commandKey + "  " + sh : commandKey;
        }

        public static bool TryGetShort(string commandKey, out string shortKey)
        {
            if (commandKey != null && Map.TryGetValue(commandKey, out shortKey)) return true;
            shortKey = null;
            return false;
        }
    }
}
