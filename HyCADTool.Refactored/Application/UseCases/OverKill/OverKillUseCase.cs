using HyCADTool.Refactored.Application.DTOs.OverKill;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Application.UseCases.OverKill
{
    /// <summary>
    /// OverKill 功能的应用用例
    /// 编排业务流程：合并重叠线段 → 查找独立端点 → 延长到交点 → 标记警告
    /// </summary>
    public class OverKillUseCase
    {
        private readonly LineOverKillService _lineService;
        
        public OverKillUseCase(LineOverKillService lineService)
        {
            _lineService = lineService;
        }
        
        /// <summary>
        /// 执行 OverKill 处理
        /// </summary>
        public OverKillResult Execute(OverKillRequest request)
        {
            var result = new OverKillResult
            {
                WarningMarkers = new List<WarningMarker>()
            };
            
            try
            {
                // 步骤 1: 合并重叠线段
                var mergedLines = _lineService.MergeOverlappingLines(
                    request.Lines, 
                    request.Tolerance);
                
                int mergedCount = request.Lines.Count - mergedLines.Count;
                
                // 步骤 2: 查找独立端点
                var independentEndpoints = _lineService.FindIndependentEndpoints(
                    mergedLines,
                    request.Tolerance);
                
                // 步骤 3: 处理独立端点
                var finalLines = new List<Line2D>(mergedLines);
                int extendedCount = 0;
                
                foreach (var (line, isStart) in independentEndpoints)
                {
                    var (extended, found, intersection) = _lineService.ExtendToIntersection(
                        line,
                        isStart,
                        finalLines.Where(l => !l.Equals(line)),
                        request.ExtensionDistance,
                        request.Tolerance);
                    
                    if (found)
                    {
                        // 替换原线段 - 使用 FindIndex 查找匹配的线段
                        int index = finalLines.FindIndex(l => l.Equals(line));
                        if (index >= 0)
                        {
                            finalLines[index] = extended;
                            extendedCount++;
                        }
                    }
                    else
                    {
                        // 创建警告标记
                        var freeEnd = isStart ? line.StartPoint : line.EndPoint;
                        var direction = line.Direction.Normalize();
                        if (isStart) direction = direction * -1;
                        
                        result.WarningMarkers.Add(new WarningMarker
                        {
                            Location = freeEnd,
                            Direction = direction,
                            Size = request.ExtensionDistance * 20
                        });
                    }
                }
                
                result.ProcessedLines = finalLines;
                result.MergedCount = mergedCount;
                result.ExtendedCount = extendedCount;
                result.Success = true;
                result.Message = $"处理完成：合并 {mergedCount} 条线段，延长 {extendedCount} 个端点";
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"处理失败: {ex.Message}";
                return result;
            }
        }
    }
}

