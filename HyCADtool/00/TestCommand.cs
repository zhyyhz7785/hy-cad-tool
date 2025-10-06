using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Commands;
using HyCADTool.HelpClass;
using HyCADTool.HelpClass.CreatBase;
using HyCADTool.HelpClass.TitleBlock;
using HyCADTool.Log;
using HyCADTool.Models;
using HyCADTool.Tools;
using System;
using HyCADTool;
using HyCADTool.Config;
//   ^\s*(?=\r?$)\n   (删除空行正则表达式asdf）
//Cad查询数据命令   (setq ent (entsel)) (setq ent_data (car ent)) (setq ent_data (entget ent_data))
[assembly: CommandClass(typeof(HyCADTool.TestCommand))]
namespace HyCADTool
{
    public class TestCommand
    {
        [CommandMethod("te", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public static void Test()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                SimpleLogger.LogElapsedTime("star1", () =>
                {
                    //var filter = AcTv.Or(AcTv.And(AcTv.Polyline, "柱".GetLayerFilter()), AcTv.DBText).Getfilter();asdfaaa
                    //var dic = EtGpt.SelectToDic(filter, "选择Polyline");
                    //var PillarPiers = dic.Values
                    //.OfType<Polyline>()
                    //.Where(x => x.Layer == "柱" && x.Linetype == "ByLayer")
                    //.ToList();
                    //ed.SetImpliedSelection(PillarPiers.Select(p => p.ObjectId).ToArray());
                    //EtGpt.GetNoUseFiniteElementResult(dic);
                    //BaseRein.CreatePillarPiersExtend();
                    //CreateRegion.AutoFillRegions();
                    // BreakLines.BreakLinesAtIntersections();
                    //var curves=BreakCurves.BreakCurvesAtIntersections();
                    //BreakLines.BreakLinesAtIntersections();
                    //DrawDCEL.CreateDCELPolylinesFromCurves();
                    //SimpleCommand.SelectReinText();
                    //DynamicDraw();
                    //DynamicPolyline();
                    //MyLine();
                    // Reinforcement.ReinAddAnchor1();
                    //Reinforcement.MyPolyline();
                    //Reinforcement.ModifyPolyline();
                    //Reinforcement.ExtentRein();
                    //Reinforcement.GExtend();
                    //SimpleCommand.QuickExtend();
                    //SimpleCommand.MleaderReinTwo();
                    // SimpleCommand.ModifyPolyline();
                    // EtGpt. BreakPoly();
                    // Reinforcement.ReinAddAnchor1();
                    //var a= EtGpt.GetPolylineInfo();
                    //if (a != null)
                    //{
                    //    var pl= a.Value.Polyline;
                    //    var p= a.Value.ClosestPoint;
                    //    var param= a.Value.param;
                    //    var index=(int)Math.Floor(param);
                    //    var pointStart=pl.GetPoint3dAt(index);
                    //    var pointEnd=pl.GetPoint3dAt(index+1);
                    //    ed.WriteMessage("\n多段线详细信息：");
                    //    ed.WriteMessage($"\n  顶点数: {pl.NumberOfVertices}");
                    //    ed.WriteMessage($"\n  是否闭合: {pl.Closed}");
                    //    ed.WriteMessage($"\n  最近点: ({p.X}, {p.Y}, {p.Z})");
                    //    ed.WriteMessage($"\n  最近点所在段索引: {index}");
                    //}
                    //DCEL 测试
                    //BreakCurves.BreakCurvesAtIntersections();
                    // DrawDCEL.CreateDCELPolylinesFromCurves();
                    //var p = db.SelectAEntity<Polyline>();
                    //EtGpt.CreateAdaptiveGrid();
                    //EtGpt.TestPileLayout();
                    //EtGpt.TestPileLayout();
                    //EtGpt.PlacePileInner();
                    //EtGpt.PlacePileWithVoronoi();
                    // EtGpt.PlacePileGridWithImprovedLogic();
                    //EtGpt.PlacePileAndVoronoiWithReplacementRate();
                    //EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //EtGpt.CreateAdaptiveGrid();
                    //EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //EtGpt.GenerateHexagonsInAutoCAD();
                    // EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //EtGpt.GenerateHexagonsInAutoCAD();
                    //EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //EtGpt.AnnotateCircleCenters();
                    // BreakCurves.BreakCurvesAtIntersections();
                    // DrawDCEL.CreateDCELPolylinesFromCurves();
                    //DrawDCEL.CreateDCELPolylinesFromCurves();
                    // GeometryConverter.TestConvertAndMovePolyline();
                    //var p= db.SelectAEntity<Polyline>();
                    // var area=StandardArea.CreateFromPolyline(p);
                    // var ids = db.SelectIds();
                    //var a= ids.ConvertToEntities<Polyline>();
                    // var filePath = @"E:\BaiduSyncdisk\Code\testResult";
                    //LayerConfigManager.ExportLayersToMarkdown();
                    //LayerConfigManager.ImportLayersFromMarkdown();
                    //BaseConfig.InitializeStyle();
                    //var ConfigFilePath = @"E:\BaiduSyncdisk\Code\CSharp\Rebuild1framwork\HyCADToolGpt\HyCADTool\config.json";
                    //var config = new ConfigManager<PileConfigData>(ConfigFilePath);
                    // HyCommand.DrawReinforcement();
                    // HyCommand.ShowPanel();
                    // GeometryConverter.Test1();
                    //HyCommand.DrawElevationWithJig();
                    // HyCommand.UpdateElevationText();
                    // HyCommand.RotateElevation();
                    // HyCommand.TextsCreatElevation();
                    //Section.ProcessSection();
                    // Section.RunExtrudeSections();
                    //Section.RunExtrudeSectionsByElevation();
                    //var dict = ElevationModelHelper.GetPolygonTextDictionary();
                    //ElevationModelHelper.TestPolygonElevationDictionary(dict);
                    // var g=new ElevationModelGenerator(dict);
                    //g.Generate3DModel();
                    //List<Polygon> outerContours;
                    //var dict = ElevationModelHelper.GetPolygonTextDictionary(out outerContours);
                    //var generator = new ElevationModelGenerator();
                    // generator.GenerateCrossSection();
                    //  generator.Generate3DModelWithWallsAndBase();
                    //ElevationModelHelper.TestOverlappingEdges(generator);
                    //ElevationModelHelper.TestIsIsolated(generator);
                    // generator.Generate3DModelWithWallsAndBase(generator.IsInitialized);
                    //ElevationModelGenerator.RunTest();
                    //ElevationModelGenerator.TestBufferStyles();
                    //TestCommands.CreatePolylineBuffer();
                    //TestCommands.CreateLineBuffer();
                    //OffsetCommands.CreateGeometricVariableOffset();
                    //AutoCADUtils.CreateVariableOffsetBuffer();
                    //TestCommands.OffsetEdgesCommand();
                    //TestCommands.OffsetPolylineEdges();
                    //var a = new WallConnectionTest();
                    //a.TestHandleWallConnection();
                    //var a = new HelpClass.CreatBase.ElevationModelGenerator();
                    //a.CreateSectionAndMove();
                    // a.GenerateWalls();
                    //  var a = new Commands();
                    //a.Create2DSectionXY();
                    //var a = new WallConnectionTest();
                    //a.TestHandleWallConnection();
                    //  CreateSection.CreateSectionAndMove();
                    //HyCommand.LineOverkill();
                    // HyCommand.DeleteSelectedObjects();
                    //HyCommand.LineOverkillCommand();
                    //var a = new HelpClass.CreatBase.ElevationModelGenerator();
                    //a.GenerateCrossSection();
                    //HyCommand.AlignTextToLineByDistance();
                    // HyCommand.AttachAnchorBoltToCircles();
                    //HyCommand.ToggleAnchorBoltDisplay();
                    // HyCommand.CreateAnchorBoltTable();
                    //HyCommand.AddAxisFromSelection();
                    // HyCommand.AddAxisNumber();
                    // HyCommand.ConstructBaseDataFilter();
                    // HyCommand.ConstructAxisCommand();
                    //HyCommand.InitializeAxisCommand();
                    // HyCommand.DisplayStructureCommand();
                    // HyCommand.ConstructBaseDataFilter();
                    //HyCommand.HighlightBoltData();
                    // HyCommand.HighlightBoltDataCommand();
                    // HyCommand.CreateAxisTableCommand();
                    //GetEquipment();
                    //var a= db.SelectAEntity<Polyline>();
                    //DimensionForReinforcement.GenerateDimension(a);
                    //HyCommand.SortPolylinePoints();
                    //var a = BaseDimension.Create();
                    // a.BPs.ToSpace();
                    //a.A_APs.ToSpace();
                    //a.B_APs.ToSpace();
                    //var a = new EnvelopeCluster();
                    //a.RunClusterEnvelopeCommand();
                    //var helper = new BaseDimHelper();
                    // var halper = new BaseDimHelper();
                    //var a = new ElevationModelGenerator();
                    //a.GenerateWalls();
                    //a.GenerateRaftAndBase();
                    //ElevationModelGenerator.RunTest();
                    //ps.ToSpace();
                    // psx.ToSpace();
                    //CreateSection.CreateSectionCommand();
                    // HyCommand.DrawRaftThicknessText();
                    // HyCommand.AnchorBoltCreateSection();
                    ///HyCommand.CreatePadFromLine();
                    // HyCommand.CreatePadFromPolylines();
                    //HyCommand.MoveBoltsToMaxYIntersectionUp();
                    //  HyCommand.SplitDimensionAtLine();
                    // EtGpt.ConvertBlockToMLeader();
                    // EtGpt.ConvertBlockToMLeader();
                    // HyCommand.CalculateBoltForces(db,ed);
                    // HyCommand.CreateMinimumBoundingRectangle();
                    // HyCommand.CreateClusteredMBRs();
                    // HyCommand.CreateViewportsFromModelBounds();
                    // HyCommand.DrawTitleBlockCommand();
                    // HyCommand.CreateLayoutViewportsOptimizedNew();
                    //var vp=new ViewportPacker();
                    //vp.PackViewports();
                    //HyCommand.PackViewports();
                    // HyCommand.CreateViewportsFromModelBounds();
                    //  HyCommand.GroupCirclesAndLabelTexts();
                    //HyCommand.GroupCirclesByElevationAndLabel();
                    // HyCommand.GroupCirclesZ_Full();
                    //HyCommand.GroupCirclesZFinal();
                    //  HyCommand.DrawTitleBlockCommand();
                    // HyCommand.MoveBoltsToVerticalIntersection();
                    //EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //EtGpt.PlacePileAndVoronoiWithLloydOptimization();
                    //HyCommand.GroupCirclesZFinal();
                    // HyCommand.GroupCirclesZFinal();
                    //PointClusterHelper.ClusterPointsAndDraw();
                    //var a=new  BaseDimHelper();
                    // BaseDimHelper.Create();
                    //TestLayerInsert();
                    //BaseDimHelper.Create();
                    //HyTest.TestProcessClusterSingle();
                    //HyCommand.ShowPanel();
                    //HyCommand.ReplacePolygonByIntersectionBatch();
                    // HyCommand.CreateDCELPolylinesFromLines();
                    // HyCommand.DrawRaftThicknessText();
                    //HyCommand.CreateViewportsFromModelBounds();
                    // PointClusterHelper.AnnotateClustersTest();
                    //PointClusterHelper.GenerateBoltDimensions();
                    //HyCommand.DrawDimensionInputPoints();
                    //HyCommand.TestAxisRegions();
                    //  HyCommand.AnnotateAxes();
                    // HyCommand.AnnotateAxes11();
                    //HyCommand.TestDrawInCad();
                    // HyCommand.ShowPanel();
                    //  HyCommand.DimPoly();
                    // ConfigManager.ExportConfigToCsv();
                    //LayerConfigManagerCsv.ImportConfigFromCsv("TextStyle","Common");
                    HyCommand.ShowPanel();
                   // HyCommand.ExportAll();
                   //HyTool.RegisterStandardLinetypes();
                    ed.WriteMessage("\n12\n");
                });
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}