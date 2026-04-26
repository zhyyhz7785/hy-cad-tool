using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Road.Shared
{
    /// <summary>
    /// 道路类 <c>hyRoad*</c> 命令的 <c>r*</c> 短别名映射（Refactored 自有版本，与 ReCall.RoadCommandShortAliases 同步）。
    /// 与 ReCall/CommandFacade.cs / Production/ProductionCommandFacade.cs 道路区
    /// <c>[CommandMethod]</c> 双注册保持一致 —— 改这里时，CommandFacade / ProductionCommandFacade 也要同步加 <c>[CommandMethod("r*")]</c>。
    /// </summary>
    public static class RoadCommandShortAliases
    {
        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hyRoad"] = "rCx",
                ["hyRoadA"] = "rLa",
                ["hyRoadAName"] = "rAn",
                ["hyRoadAlnAssign"] = "rAsg",
                ["hyRoadAlnPlan"] = "rPlp",
                ["hyRoadAutoIntersection"] = "rIa",
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
                ["hyRoadSeg3"] = "r3s",
                ["hyRoadTree"] = "rTree",
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
