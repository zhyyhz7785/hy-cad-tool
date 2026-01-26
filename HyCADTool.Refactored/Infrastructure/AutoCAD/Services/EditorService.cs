using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 编辑器交互服务实现 (Editor Service Implementation)
    /// 封装 AutoCAD Editor 交互操作
    /// </summary>
    public class EditorService : IEditorService
    {
        #region 消息输出 (Message Output)

        public void WriteMessage(string message)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            doc.Editor.WriteMessage($"\n{message}");
        }

        public void WriteWarning(string message)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            doc.Editor.WriteMessage($"\n⚠️  {message}");
        }

        public void WriteError(string message)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            doc.Editor.WriteMessage($"\n✗ 错误 (Error): {message}");
        }

        #endregion

        #region 点输入 (Point Input)

        public Point3d? GetPoint(string prompt)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptPointOptions($"\n{prompt}: ")
            {
                AllowNone = false
            };

            var result = ed.GetPoint(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        public Point3d? GetPoint(string prompt, Point3d basePoint)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptPointOptions($"\n{prompt}: ")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = basePoint
            };

            var result = ed.GetPoint(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        public Point3d? GetAnglePoint(string prompt, Point3d basePoint)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptPointOptions($"\n{prompt}: ")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = basePoint,
                UseDashedLine = true
            };

            var result = ed.GetPoint(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        #endregion

        #region 数值输入 (Numeric Input)

        public double? GetDistance(string prompt)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptDistanceOptions($"\n{prompt}: ")
            {
                AllowNone = false,
                AllowZero = false,
                AllowNegative = false
            };

            var result = ed.GetDistance(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        public double? GetAngle(string prompt)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptAngleOptions($"\n{prompt}: ")
            {
                AllowNone = false
            };

            var result = ed.GetAngle(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        public int? GetInteger(string prompt)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptIntegerOptions($"\n{prompt}: ")
            {
                AllowNone = false
            };

            var result = ed.GetInteger(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        public double? GetDouble(string prompt)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptDoubleOptions($"\n{prompt}: ")
            {
                AllowNone = false
            };

            var result = ed.GetDouble(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.Value;
            }

            return null;
        }

        #endregion

        #region 字符串输入 (String Input)

        public string GetString(string prompt, bool allowSpaces = true)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptStringOptions($"\n{prompt}: ")
            {
                AllowSpaces = allowSpaces
            };

            var result = ed.GetString(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.StringResult;
            }

            return null;
        }

        #endregion

        #region 关键字选择 (Keyword Selection)

        public string GetKeyword(string prompt, params string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                throw new ArgumentException("关键字数组不能为空 (Keyword array cannot be empty)", nameof(keywords));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptKeywordOptions($"\n{prompt}");
            
            foreach (var keyword in keywords)
            {
                options.Keywords.Add(keyword);
            }
            
            options.Keywords.Default = keywords[0];

            var result = ed.GetKeywords(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.StringResult;
            }

            return null;
        }

        public string GetKeyword(string prompt, string defaultKeyword, params string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                throw new ArgumentException("关键字数组不能为空 (Keyword array cannot be empty)", nameof(keywords));

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptKeywordOptions($"\n{prompt}");
            
            foreach (var keyword in keywords)
            {
                options.Keywords.Add(keyword);
            }
            
            if (!string.IsNullOrEmpty(defaultKeyword))
            {
                options.Keywords.Default = defaultKeyword;
            }

            var result = ed.GetKeywords(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.StringResult;
            }
            
            // 如果用户按回车使用默认值
            if (result.Status == PromptStatus.None && !string.IsNullOrEmpty(defaultKeyword))
            {
                return defaultKeyword;
            }

            return null;
        }

        #endregion

        #region 确认输入 (Confirmation Input)

        public bool GetYesNo(string prompt, bool defaultValue = true)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动的 AutoCAD 文档 (No active AutoCAD document)");

            var ed = doc.Editor;

            var options = new PromptKeywordOptions($"\n{prompt}")
            {
                AllowNone = true
            };
            
            options.Keywords.Add("是(Yes)");
            options.Keywords.Add("否(No)");
            options.Keywords.Default = defaultValue ? "是(Yes)" : "否(No)";

            var result = ed.GetKeywords(options);

            if (result.Status == PromptStatus.OK)
            {
                return result.StringResult.StartsWith("是") || result.StringResult.ToUpper().StartsWith("Y");
            }

            // 用户按回车，返回默认值
            if (result.Status == PromptStatus.None)
            {
                return defaultValue;
            }

            // 用户取消，返回 false
            return false;
        }

        #endregion
    }
}

