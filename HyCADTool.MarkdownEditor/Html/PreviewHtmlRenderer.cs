using Markdig;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.TextLayout;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// Markdown → 交互式分栏 HTML 预览（用于 WebView2 预览控件）
    /// </summary>
    public static class PreviewHtmlRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string ToInteractiveHtml(
            string markdown,
            int columnCount,
            double previewScale = 1.0,
            EditorConfig config = null,
            LayoutResult layoutResult = null)
        {
            string body = string.IsNullOrEmpty(markdown)
                ? "<p class=\"empty\">(无内容)</p>"
                : Markdown.ToHtml(markdown, Pipeline);

            int cols = Math.Max(1, Math.Min(10, columnCount));
            double scale = Math.Max(0.1, Math.Min(5.0, previewScale));
            EditorConfig cfg = config ?? new EditorConfig();
            string footerHtml = BuildFooterHtml(cols);
            string dynamicCss = BuildDynamicCss(scale, cfg);
            string dynamicJs = BuildDynamicJs(scale, cfg, cols, layoutResult);

            const string katexCss = "https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css";
            const string katexJs = "https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.js";
            const string katexAutoRenderJs = "https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/contrib/auto-render.min.js";

            return "<!DOCTYPE html>\n<html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />"
                + "<link rel=\"stylesheet\" href=\"" + katexCss + "\" />"
                + "<style>" + dynamicCss + "</style>"
                + "</head><body>"
                + "<div id=\"source\" style=\"display:none;\">" + body + "</div>"
                + "<div class=\"viewport\" id=\"viewport\"><div class=\"pages\" id=\"pages\"></div></div>"
                + footerHtml
                + "<script src=\"" + katexJs + "\"></script>"
                + "<script src=\"" + katexAutoRenderJs + "\"></script>"
                + "<script>" + dynamicJs + "</script>"
                + "</body></html>";
        }

        private static string BuildDynamicCss(double previewScale, EditorConfig cfg)
        {
            double textSizeMm = Math.Max(0.1, cfg.TextSize);
            double drawScale = Math.Max(0.1, cfg.DrawScale);
            double textXScale = Math.Max(0.1, cfg.TextXScale);
            double basePx = textSizeMm * previewScale;
            double lineHeightFactor = Math.Max(1.0, cfg.LineSpacingFactor);
            double pageWidthPx = cfg.PageWidthMm * previewScale;
            double pageHeightPx = cfg.PageHeightMm * previewScale;
            double leftPx = cfg.MarginLeftMm * previewScale;
            double rightPx = cfg.MarginRightMm * previewScale;
            double topPx = cfg.MarginTopMm * previewScale;
            double bottomPx = cfg.MarginBottomMm * previewScale;
            double gutterPx = Math.Max(0, cfg.ColumnGutter) * previewScale;
            // 分栏间隔至少保留可见/可拖拽宽度，避免 0 时无法操作
            double gutterVisualPx = Math.Max(6, gutterPx);

            string h1Margin = $"margin:{cfg.H1SpaceBefore:F1}em 0 {cfg.H1SpaceAfter:F1}em";
            string h2Margin = $"margin:{cfg.H2SpaceBefore:F1}em 0 {cfg.H2SpaceAfter:F1}em";
            string h3Margin = $"margin:{cfg.H3SpaceBefore:F1}em 0 {cfg.H3SpaceAfter:F1}em";
            string pMargin = $"margin:0 0 {cfg.PSpaceAfter:F1}em";
            string liMargin = $"margin:0 0 {cfg.LiSpaceAfter:F1}em";
            string bqMargin = $"margin:{cfg.QuoteSpaceBefore:F1}em 0 {cfg.QuoteSpaceAfter:F1}em";
            string fontFamily = ResolvePreviewFontFamily(cfg);
            double listIndentPx = Math.Max(8, cfg.ListIndent * drawScale * previewScale);
            double quoteIndentPx = Math.Max(8, cfg.QuoteIndent * drawScale * previewScale);

            var tokens = new (string token, string value)[]
            {
                ("/*BASE_FONT_SIZE*/", $"{basePx.ToString("0.###", CultureInfo.InvariantCulture)}px"),
                ("/*BASE_LINE_HEIGHT*/", lineHeightFactor.ToString("0.###", CultureInfo.InvariantCulture)),
                ("/*BASE_FONT_FAMILY*/", fontFamily),
                ("/*TEXT_X_SCALE_CSS*/", textXScale.ToString("0.###", CultureInfo.InvariantCulture)),
                ("/*PAPER_WIDTH*/", $"{Round(pageWidthPx):F0}px"),
                ("/*PAPER_HEIGHT*/", $"{Round(pageHeightPx):F0}px"),
                ("/*PAD_LEFT*/", $"{Round(leftPx):F0}px"),
                ("/*PAD_RIGHT*/", $"{Round(rightPx):F0}px"),
                ("/*PAD_TOP*/", $"{Round(topPx):F0}px"),
                ("/*PAD_BOTTOM*/", $"{Round(bottomPx):F0}px"),
                ("/*GUTTER*/", $"{Round(gutterVisualPx):F0}px"),
                ("/*LIST_INDENT*/", $"{Round(listIndentPx):F0}px"),
                ("/*QUOTE_INDENT*/", $"{Round(quoteIndentPx):F0}px"),
                ("/*H1_MARGIN*/", h1Margin),
                ("/*H2_MARGIN*/", h2Margin),
                ("/*H3_MARGIN*/", h3Margin),
                ("/*P_MARGIN*/", pMargin),
                ("/*LI_MARGIN*/", liMargin),
                ("/*BQ_MARGIN*/", bqMargin),
            };

            var sb = new StringBuilder(CSS_TEMPLATE);
            foreach (var token in tokens)
                sb.Replace(token.token, token.value);
            return sb.ToString();
        }

        private static string BuildDynamicJs(double previewScale, EditorConfig cfg, int columnCount, LayoutResult layoutResult)
        {
            double safePreviewScale = Math.Max(0.01, previewScale);
            double textSizeMm = Math.Max(0.1, cfg.TextSize);
            double textXScale = Math.Max(0.1, cfg.TextXScale);
            double pageWidthMm = Math.Max(1.0, cfg.PageWidthMm);
            double lineHeightFactor = Math.Max(1.0, cfg.LineSpacingFactor);
            double fontWidthFactor = ResolveFontWidthFactor(cfg);
            const double contentPaddingX = 20.0; // .col-content 左右 padding 合计
            string layoutJson = layoutResult == null ? "null" : JsonConvert.SerializeObject(layoutResult);
            string customColumnWidths = BuildCustomColumnWidthsJson(layoutResult, safePreviewScale);

            return JS_TEMPLATE
                .Replace("/*PREVIEW_SCALE*/", safePreviewScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_SIZE_MM*/", textSizeMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_X_SCALE*/", textXScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*LINE_HEIGHT_FACTOR*/", lineHeightFactor.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FONT_WIDTH_FACTOR*/", fontWidthFactor.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*PAGE_WIDTH_MM*/", pageWidthMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*COLUMN_COUNT*/", columnCount.ToString(CultureInfo.InvariantCulture))
                .Replace("/*CONTENT_PADDING_X*/", contentPaddingX.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*LAYOUT_RESULT*/", layoutJson)
                .Replace("/*CUSTOM_COLUMN_WIDTHS*/", customColumnWidths);
        }

        private static double ResolveFontWidthFactor(EditorConfig cfg)
        {
            string name = (cfg?.FontFileName ?? cfg?.PreviewFontFamily ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name))
                return 1.0;

            // 经验系数：用于弥补 Web 字体渲染与 CAD 字体宽度口径差。
            if (name.Contains("yahei") || name.Contains("微软雅黑") || name.Contains("msyh"))
                return 1.0;
            if (name.Contains("simsun") || name.Contains("宋体") || name.Contains("song"))
                return 1.03;
            if (name.EndsWith(".shx", StringComparison.Ordinal))
                return 0.95;

            return 1.0;
        }

        private static string BuildCustomColumnWidthsJson(LayoutResult layoutResult, double previewScale)
        {
            if (layoutResult?.ColumnWidthsMm == null || layoutResult.ColumnWidthsMm.Length == 0)
                return "[]";

            var values = layoutResult.ColumnWidthsMm
                .Where(w => w > 0)
                .Select(w => (w * previewScale).ToString("0.###", CultureInfo.InvariantCulture))
                .ToArray();

            if (values.Length == 0)
                return "[]";

            return "[" + string.Join(",", values) + "]";
        }

        private static string ResolvePreviewFontFamily(EditorConfig cfg)
        {
            string previewFamily = cfg?.PreviewFontFamily;
            if (string.IsNullOrWhiteSpace(previewFamily))
                previewFamily = "Microsoft YaHei";

            string escaped = previewFamily.Replace("\\", "\\\\").Replace("'", "\\'");
            return $"'{escaped}','微软雅黑','Segoe UI',sans-serif";
        }

        private static double Round(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static string BuildFooterHtml(int count)
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"paper-footer\" id=\"paper-footer\">");
            for (int i = 0; i < count; i++)
            {
                sb.Append("<span class=\"col-footer-item\">第 ").Append(i + 1)
                  .Append(" 栏 <span class=\"col-chars\" id=\"chars-").Append(i).Append("\"></span>")
                  .Append(" · <span id=\"info-").Append(i).Append("\"></span></span>");
                if (i < count - 1)
                    sb.Append("<span class=\"col-footer-sep\"></span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        #region CSS

        private const string CSS_TEMPLATE = @"
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{
  overflow:hidden;background:#0d1117;color:#d4d4d4;
  font-family:/*BASE_FONT_FAMILY*/;
  font-size:/*BASE_FONT_SIZE*/;line-height:/*BASE_LINE_HEIGHT*/
}

.viewport{
  width:100%;height:calc(100% - 24px);
  overflow-x:auto;overflow-y:auto;
  padding:8px 6px 12px 0;
}
.pages{
  display:flex;flex-direction:column;gap:16px;
  align-items:flex-start;
}
.page-wrap{position:relative}
.paper{
  width:/*PAPER_WIDTH*/;
  height:/*PAPER_HEIGHT*/;
  background:#ffffff;
  color:#111111;
  position:relative;
  overflow:hidden;
}
.paper-inner{
  width:100%;
  height:100%;
  padding:/*PAD_TOP*/ /*PAD_RIGHT*/ /*PAD_BOTTOM*/ /*PAD_LEFT*/;
  background:#2b3138;
  display:-ms-flexbox;
  display:flex;
  -ms-flex-direction:row;
  flex-direction:row;
}

.col-wrap{-ms-flex:1;flex:1;display:-ms-flexbox;display:flex;-ms-flex-direction:column;flex-direction:column;min-width:0;overflow:hidden}
.col-content{-ms-flex:1;flex:1;overflow:hidden;padding:8px 10px;background:#ffffff;color:#111111;caret-color:#111}
.col-content.last{overflow:hidden}
.col-content[contenteditable='true']{outline:none}
.col-content[contenteditable='true']:focus{box-shadow:inset 0 0 0 1px #58a6ff}
.col-vhandle{height:6px;background:#2b3138;cursor:ns-resize;-ms-flex-negative:0;flex-shrink:0}
.col-vhandle:hover{background:#6e7681}
.col-tables{flex-shrink:0;padding:4px 10px;background:#f8f9fb;border-top:1px dashed #adb5bd;overflow-x:auto;overflow-y:hidden}
.col-tables:empty{display:none}
.col-gap{
  width:/*GUTTER*/;min-width:6px;background:transparent;
  -ms-flex-negative:0;flex-shrink:0;cursor:ew-resize;position:relative
}
.col-gap:before{
  content:'';position:absolute;left:50%;top:0;bottom:0;width:1px;
  margin-left:-0.5px;background:#6e7681
}
.col-gap:hover{background:#2f3942}
.col-gap:hover:before{width:2px;margin-left:-1px;background:#58a6ff}

.paper-footer{
  display:-ms-flexbox;display:flex;-ms-flex-direction:row;flex-direction:row;
  height:24px;
  padding:4px 0;color:#8b949e;font-size:11px;
  border-top:1px solid #24292e;
  background:#111418;
  flex-shrink:0;
}
.col-footer-item{-ms-flex:1;flex:1;text-align:center}
.col-footer-sep{
  width:/*GUTTER*/;min-width:6px;background:transparent;
  -ms-flex-negative:0;flex-shrink:0;position:relative
}
.col-footer-sep:before{
  content:'';position:absolute;left:50%;top:0;bottom:0;width:1px;
  margin-left:-0.5px;background:#4b5560
}
.col-chars{color:#58a6ff}

.paper-resize-right{
  position:absolute;top:0;right:0;width:6px;height:100%;
  cursor:ew-resize;background:transparent;
}
.paper-resize-bottom{
  position:absolute;left:0;bottom:0;width:100%;height:6px;
  cursor:ns-resize;background:transparent;
}
.paper-resize-corner{
  position:absolute;right:0;bottom:0;width:12px;height:12px;
  cursor:nwse-resize;background:#30363d;border-top:1px solid #6e7681;border-left:1px solid #6e7681;
}

.empty{color:#555;font-style:italic;text-align:center;padding:30px}

h1{font-size:1.6em;color:#333333;/*H1_MARGIN*/;border-bottom:1px solid #d0d7de;padding-bottom:3px}
h2{font-size:1.3em;color:#333333;/*H2_MARGIN*/}
h3{font-size:1.1em;color:#333333;/*H3_MARGIN*/}
h4,h5,h6{font-size:1em;color:#333333;margin:5px 0 2px}
p{/*P_MARGIN*/}
strong{color:#000}
em{color:#444}
ul,ol{padding-left:/*LIST_INDENT*/;margin:4px 0}
li{/*LI_MARGIN*/}
code{background:#f0f3f6;color:#24292f;padding:1px 4px;border-radius:2px;font-family:Consolas,monospace;font-size:0.9em}
pre{background:#f6f8fa;border:1px solid #d0d7de;border-radius:3px;padding:6px;margin:4px 0;overflow-x:auto}
pre code{background:none;padding:0}
blockquote{border-left:3px solid #6e7781;padding:3px /*QUOTE_INDENT*/;/*BQ_MARGIN*/;color:#333;background:#f6f8fa}
hr{border:none;border-top:1px solid #d0d7de;margin:8px 0}
table{border-collapse:collapse;width:auto;max-width:100%;margin:6px 0}
th,td{border:1px solid #d0d7de;padding:3px 6px;font-size:0.9em;text-align:left;word-break:break-word;overflow-wrap:break-word}
th{background:#f6f8fa;color:#111;font-weight:bold}
tr:nth-child(even){background:#f8fafc}

.col-content h1,.col-content h2,.col-content h3,.col-content h4,.col-content h5,.col-content h6,
.col-content p,.col-content ul,.col-content ol,.col-content blockquote,.col-content pre{
  transform-origin:left top;
  transform:scaleX(/*TEXT_X_SCALE_CSS*/);
  width:calc(100% / /*TEXT_X_SCALE_CSS*/);
}
";

        #endregion

        #region JavaScript

        private const string JS_TEMPLATE = @"
window.colChars=[];
window.colParas=[];
window.previewStats={schemaVersion:3,currentPage:1,pageCount:1,charsPerColumn:[],columnParagraphIndicesText:'',columnParagraphIndices:[],columns:[],pages:[],blockTypeCounts:{}};
var COLUMN_COUNT=/*COLUMN_COUNT*/;
var PREVIEW_SCALE=/*PREVIEW_SCALE*/;
var TEXT_SIZE_MM=/*TEXT_SIZE_MM*/;
var TEXT_X_SCALE=/*TEXT_X_SCALE*/;
var LINE_HEIGHT_FACTOR=/*LINE_HEIGHT_FACTOR*/;
var FONT_WIDTH_FACTOR=/*FONT_WIDTH_FACTOR*/;
var PAGE_WIDTH_MM=/*PAGE_WIDTH_MM*/;
var CONTENT_PADDING_X=/*CONTENT_PADDING_X*/;
var LAYOUT_RESULT=/*LAYOUT_RESULT*/;
var CHAR_WIDTH_MM=Math.max(0.01,TEXT_SIZE_MM*TEXT_X_SCALE*FONT_WIDTH_FACTOR);
var hPage=-1,hDiv=-1,sX=0,sW=[];
var vPage=-1,vCol=-1,sY=0,sH=0;
var pMode='',pStartX=0,pStartY=0,pStartW=0,pStartH=0,pTargetPage=0;
var currentPageIndex=0;
var customPaperWidth=0,customPaperHeight=0;
var customColumnWidths=/*CUSTOM_COLUMN_WIDTHS*/;
var customColumnHeights={};
var scrollTicking=false;
var contentChangedTimer=0;
var isDistributing=false;
var middlePanActive=false;
var middlePanStartX=0,middlePanStartY=0,middlePanStartLeft=0,middlePanStartTop=0;
var middlePanViewport=null;

function getPreviewStats(){ return window.previewStats || null; }
function getColChars(){return (window.previewStats&&window.previewStats.charsPerColumn?window.previewStats.charsPerColumn:[]).join(',');}
function getColParas(){return window.previewStats&&window.previewStats.columnParagraphIndicesText?window.previewStats.columnParagraphIndicesText:'';}
function getPaperSize(){
  var p=document.getElementById('paper-0');
  if(!p) return '';
  return p.offsetWidth+','+p.offsetHeight;
}

function postPaperScaleToHost(scale){
  if(!window.chrome||!window.chrome.webview||!window.chrome.webview.postMessage) return;
  if(!isFinite(scale) || scale<=0) return;
  window.chrome.webview.postMessage({ type:'paperScale', value:scale });
}

function postWheelZoomToHost(deltaY){
  if(!window.chrome||!window.chrome.webview||!window.chrome.webview.postMessage) return;
  if(!isFinite(deltaY)) return;
  window.chrome.webview.postMessage({ type:'wheelZoom', deltaY:deltaY });
}

function postPageStateToHost(current,total){
  if(!window.chrome||!window.chrome.webview||!window.chrome.webview.postMessage) return;
  window.chrome.webview.postMessage({ type:'pageState', currentPage:current, pageCount:total });
}

function toColParasText(colParas){
  var parts=[];
  if(!colParas) return '';
  for(var i=0;i<colParas.length;i++){parts.push((colParas[i]||[]).join(','));}
  return parts.join('|');
}

function normalizeBlockType(tagName){
  var t=(tagName||'').toLowerCase();
  if(t==='h1'||t==='h2'||t==='h3'||t==='h4'||t==='h5'||t==='h6') return 'heading';
  if(t==='p') return 'paragraph';
  if(t==='ul'||t==='ol') return 'list';
  if(t==='blockquote') return 'blockquote';
  if(t==='pre') return 'codeblock';
  if(t==='table') return 'table';
  if(t==='hr') return 'hr';
  if(!t) return 'unknown';
  return t;
}

function charDisplayUnits(ch){
  if(!ch) return 0;
  var code=ch.charCodeAt(0);
  if(code<=0x007F) return 1;
  if(code<=0x024F) return 1;
  if((code>=0x3400&&code<=0x4DBF) || (code>=0x4E00&&code<=0x9FFF) || (code>=0xF900&&code<=0xFAFF)) return 2;
  if((code>=0x3000&&code<=0x303F) || (code>=0xFF01&&code<=0xFF60) || (code>=0xFFE0&&code<=0xFFE6)) return 2;
  return 2;
}

function calcTextMetrics(text){
  var units=0, chars=0, i=0, ch='';
  text=text||'';
  for(i=0;i<text.length;i++){
    ch=text.charAt(i);
    if(/\s/.test(ch)) continue;
    chars++;
    units+=charDisplayUnits(ch);
  }
  return {chars:chars, units:units};
}

function buildBlockTypeCountForColumn(sourceElements, paraIndices, outputMap){
  outputMap=outputMap||{};
  var map={}, i=0, idx=0, el=null, key='';
  for(i=0;i<paraIndices.length;i++){
    idx=paraIndices[i];
    el=(sourceElements && idx>=0 && idx<sourceElements.length) ? sourceElements[idx] : null;
    key=normalizeBlockType(el ? el.tagName : '');
    map[key]=(map[key]||0)+1;
    outputMap[key]=(outputMap[key]||0)+1;
  }
  return map;
}

function createEmptyColumnStats(columnIndex){
  return {
    index:columnIndex,
    charsPerLine:0,
    paragraphCount:0,
    totalChars:0,
    totalDisplayUnits:0,
    avgDisplayUnitsPerChar:0,
    blockTypes:{}
  };
}

function countRenderedBlocks(pages){
  var count=0;
  if(!pages) return 0;
  for(var p=0;p<pages.length;p++){
    var page=pages[p];
    if(!page || !page.columns) continue;
    for(var c=0;c<page.columns.length;c++){
      var col=page.columns[c];
      count+=col && col.children ? col.children.length : 0;
    }
  }
  return count;
}

function appendBlockByFlow(pages, sourceElements, blockIndex, globalBlockTypes){
  if(!sourceElements || blockIndex<0 || blockIndex>=sourceElements.length) return;
  if(!pages || pages.length<=0){
    pages=[createPage(0)];
  }

  var pi=Math.max(0,pages.length-1);
  var ci=0;
  var page=pages[pi];
  for(ci=0;ci<COLUMN_COUNT;ci++){
    var probe=page.columns[ci];
    if(probe && !isColumnOverflow(probe)){
      break;
    }
  }
  if(ci>=COLUMN_COUNT){
    ci=COLUMN_COUNT;
  }

  var guard=0;
  while(guard<4000){
    guard++;
    if(ci>=COLUMN_COUNT){
      pi++;
      ci=0;
    }
    if(pi>=pages.length){
      pages.push(createPage(pi));
    }
    page=pages[pi];
    var col=page.columns[ci];
    if(!col){
      ci++;
      continue;
    }

    var clone=sourceElements[blockIndex].cloneNode(true);
    col.appendChild(clone);
    // 列非空时溢出则顺延到下一列/页；单块过高则强制保留，避免死循环丢块。
    if(isColumnOverflow(col) && col.children.length>1){
      col.removeChild(clone);
      ci++;
      continue;
    }

    page.colParas[ci].push(blockIndex);
    var key=normalizeBlockType(sourceElements[blockIndex]?sourceElements[blockIndex].tagName:'');
    globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
    return;
  }
}

function fillMissingBlocksByFlow(pages, sourceElements, assignedFlags, globalBlockTypes){
  if(!sourceElements || sourceElements.length===0) return 0;
  var missing=[];
  for(var i=0;i<sourceElements.length;i++){
    if(!assignedFlags[i]){
      missing.push(i);
    }
  }
  for(var m=0;m<missing.length;m++){
    appendBlockByFlow(pages, sourceElements, missing[m], globalBlockTypes);
    assignedFlags[missing[m]]=true;
  }
  return missing.length;
}

function getColumn(pageIndex,colIndex){
  return document.getElementById('col-'+pageIndex+'-'+colIndex);
}

function getNextPosition(pageIndex,colIndex){
  var nextCol=colIndex+1;
  var nextPage=pageIndex;
  if(nextCol>=COLUMN_COUNT){
    nextCol=0;
    nextPage++;
  }
  return { pageIndex:nextPage, colIndex:nextCol };
}

function ensurePage(pageIndex){
  var page=document.querySelector('[data-page-index=""'+pageIndex+'""]');
  if(page) return page;
  createPage(pageIndex);
  return document.querySelector('[data-page-index=""'+pageIndex+'""]');
}

function ensureColumn(pageIndex,colIndex){
  if(colIndex<0 || colIndex>=COLUMN_COUNT) return null;
  ensurePage(pageIndex);
  return getColumn(pageIndex,colIndex);
}

function isColumnOverflow(col){
  if(!col) return false;
  return col.scrollHeight>col.clientHeight+2;
}

function getEditingBlockInColumn(col){
  if(!col || document.activeElement!==col) return null;
  var sel=window.getSelection ? window.getSelection() : null;
  if(!sel || sel.rangeCount<=0) return null;
  var node=sel.anchorNode;
  if(!node) return null;
  if(node.nodeType===3) node=node.parentNode;
  while(node && node.parentNode!==col){
    node=node.parentNode;
  }
  return (node && node.parentNode===col) ? node : null;
}

function canFitBlock(col,block){
  if(!col || !block) return false;
  col.appendChild(block);
  var fits=!isColumnOverflow(col);
  col.removeChild(block);
  return fits;
}

function trimTrailingEmptyPages(){
  var wraps=document.querySelectorAll('.page-wrap');
  for(var i=wraps.length-1;i>=1;i--){
    var cols=wraps[i].querySelectorAll('.col-content');
    var hasContent=false;
    for(var c=0;c<cols.length;c++){
      if(cols[c].children.length>0){
        hasContent=true;
        break;
      }
    }
    if(hasContent) break;
    wraps[i].parentNode.removeChild(wraps[i]);
  }
}

function moveOverflowForward(pageIndex,colIndex,lockedBlock){
  var col=ensureColumn(pageIndex,colIndex);
  if(!col) return;
  var safe=0;
  while(isColumnOverflow(col) && safe<200){
    safe++;
    var moveNode=col.lastElementChild;
    if(!moveNode) break;
    if(lockedBlock && moveNode===lockedBlock){
      moveNode=moveNode.previousElementSibling;
    }
    if(!moveNode) break;
    var nextPos=getNextPosition(pageIndex,colIndex);
    var nextCol=ensureColumn(nextPos.pageIndex,nextPos.colIndex);
    if(!nextCol) break;
    nextCol.insertBefore(moveNode,nextCol.firstChild);
  }
}

function pullFromNext(pageIndex,colIndex,lockedBlock){
  var col=ensureColumn(pageIndex,colIndex);
  if(!col) return;
  var nextPos=getNextPosition(pageIndex,colIndex);
  var nextCol=getColumn(nextPos.pageIndex,nextPos.colIndex);
  if(!nextCol) return;
  var safe=0;
  while(nextCol.firstElementChild && safe<200){
    safe++;
    var candidate=nextCol.firstElementChild;
    if(!candidate || (lockedBlock && candidate===lockedBlock)) break;
    if(!canFitBlock(col,candidate)) break;
    col.appendChild(candidate);
  }
}

function computeCharsPerLine(col,metrics){
  var contentW=Math.max(1, col.clientWidth-CONTENT_PADDING_X);
  var contentMm=contentW/Math.max(0.01, PREVIEW_SCALE);
  var unitPerLine=Math.floor(contentMm/CHAR_WIDTH_MM);
  var avgUnitsPerChar=metrics.chars>0 ? (metrics.units/metrics.chars) : 1;
  var cpl=Math.floor(unitPerLine/Math.max(0.5, avgUnitsPerChar));
  if(cpl<1) cpl=1;
  return cpl;
}

function rebuildPreviewStatsFromDom(){
  var pages=document.querySelectorAll('.page-wrap');
  var pageStats=[];
  var globalBlockTypes={};
  var blockCursor=0;
  var p=0, c=0, b=0;

  for(p=0;p<pages.length;p++){
    var cols=pages[p].querySelectorAll('.col-content');
    if(cols.length>0){
      cols[cols.length-1].className='col-content last';
    }
    var pageBlockTypes={};
    var colStats=[];
    var colChars=[];
    var colParas=createEmptyColParas();
    for(c=0;c<COLUMN_COUNT;c++){
      var col=(c<cols.length) ? cols[c] : null;
      if(!col){
        colChars.push(0);
        colStats.push(createEmptyColumnStats(c));
        continue;
      }
      var paraIndices=[];
      for(b=0;b<col.children.length;b++){
        var node=col.children[b];
        paraIndices.push(blockCursor++);
        var key=normalizeBlockType(node ? node.tagName : '');
        pageBlockTypes[key]=(pageBlockTypes[key]||0)+1;
        globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
      }
      colParas[c]=paraIndices;
      var rawText=(col.innerText||col.textContent||'');
      var metrics=calcTextMetrics(rawText);
      var cpl=computeCharsPerLine(col,metrics);
      colChars.push(cpl);
      var bt={};
      for(b=0;b<col.children.length;b++){
        var t=normalizeBlockType(col.children[b]?col.children[b].tagName:'');
        bt[t]=(bt[t]||0)+1;
      }
      colStats.push({
        index:c,
        charsPerLine:cpl,
        paragraphCount:paraIndices.length,
        totalChars:metrics.chars,
        totalDisplayUnits:metrics.units,
        avgDisplayUnitsPerChar:metrics.chars>0 ? (metrics.units/metrics.chars) : 0,
        blockTypes:bt
      });
    }
    pageStats.push({
      pageIndex:p,
      charsPerColumn:colChars,
      columnParagraphIndicesText:toColParasText(colParas),
      columnParagraphIndices:colParas,
      columns:colStats,
      blockTypeCounts:pageBlockTypes
    });
  }

  if(pageStats.length<=0){
    var emptyColumns=[], k=0;
    for(k=0;k<COLUMN_COUNT;k++) emptyColumns.push(createEmptyColumnStats(k));
    var emptyParas=createEmptyColParas();
    pageStats.push({
      pageIndex:0,
      charsPerColumn:emptyColumns.map(function(){ return 0; }),
      columnParagraphIndicesText:toColParasText(emptyParas),
      columnParagraphIndices:emptyParas,
      columns:emptyColumns,
      blockTypeCounts:{}
    });
  }

  var firstPage=pageStats[0];
  window.colChars=firstPage.charsPerColumn||[];
  window.colParas=firstPage.columnParagraphIndices||createEmptyColParas();
  window.previewStats={
    schemaVersion:3,
    currentPage:Math.min(currentPageIndex+1, pageStats.length),
    pageCount:Math.max(1,pageStats.length),
    charsPerColumn:firstPage.charsPerColumn||[],
    columnParagraphIndicesText:firstPage.columnParagraphIndicesText||'',
    columnParagraphIndices:firstPage.columnParagraphIndices||createEmptyColParas(),
    columns:firstPage.columns||[],
    pages:pageStats,
    blockTypeCounts:globalBlockTypes
  };
}

function cascadeReflow(startPage,startCol,lockedBlock){
  isDistributing=true;
  var pageIndex=Math.max(0,startPage||0);
  var colIndex=Math.max(0,startCol||0);
  var safe=0;
  while(safe<2000){
    safe++;
    var col=ensureColumn(pageIndex,colIndex);
    if(!col) break;
    moveOverflowForward(pageIndex,colIndex,lockedBlock);
    pullFromNext(pageIndex,colIndex,lockedBlock);
    var next=getNextPosition(pageIndex,colIndex);
    var nextCol=getColumn(next.pageIndex,next.colIndex);
    if(!nextCol){
      break;
    }
    pageIndex=next.pageIndex;
    colIndex=next.colIndex;
  }
  trimTrailingEmptyPages();
  separateTablesFromColumns();
  renderMathInColumns();
  normalizeTableLayout();
  rebuildPreviewStatsFromDom();
  var wraps=document.querySelectorAll('.page-wrap');
  var targetPage=Math.min(currentPageIndex, Math.max(0, wraps.length-1));
  setCurrentPage(targetPage, true);
  isDistributing=false;
}

function appendTextEscaped(text){
  return (text||'').replace(/\r\n/g,'\n');
}

function extractKatexTex(node){
  if(!node || node.nodeType!==1) return '';
  var host=node;
  if(node.classList && node.classList.contains('katex-display')){
    host=node.querySelector('.katex');
  }
  if(!host) return '';
  if(host.classList && !host.classList.contains('katex')){
    host=host.querySelector('.katex');
  }
  if(!host) return '';
  var ann=host.querySelector('annotation[encoding=""application/x-tex""]');
  return ann ? (ann.textContent||'') : '';
}

function inlineToMarkdown(node){
  if(!node) return '';
  if(node.nodeType===3){
    return appendTextEscaped(node.nodeValue||'');
  }
  if(node.nodeType!==1) return '';
  var tag=(node.tagName||'').toLowerCase();
  if(node.classList && node.classList.contains('katex-display')){
    var texDisplay=extractKatexTex(node);
    if(texDisplay) return '\n$$'+texDisplay+'$$\n';
  }
  if(node.classList && node.classList.contains('katex')){
    var texInline=extractKatexTex(node);
    if(texInline) return '$'+texInline+'$';
  }
  if(tag==='br') return '\n';
  var inner='';
  for(var i=0;i<node.childNodes.length;i++){
    inner+=inlineToMarkdown(node.childNodes[i]);
  }
  if(tag==='strong' || tag==='b') return '**'+inner+'**';
  if(tag==='em' || tag==='i') return '*'+inner+'*';
  if(tag==='code') return '`'+inner.replace(/\n/g,' ')+'`';
  if(tag==='a'){
    var href=node.getAttribute('href')||'';
    if(!href) return inner;
    return '['+inner+']('+href+')';
  }
  return inner;
}

function blockToMarkdown(el){
  if(!el || el.nodeType!==1) return '';
  if(el.classList && el.classList.contains('empty')) return '';
  var tag=(el.tagName||'').toLowerCase();
  if(/^h[1-6]$/.test(tag)){
    var lv=parseInt(tag.charAt(1),10);
    if(!isFinite(lv)||lv<1) lv=1;
    return Array(lv+1).join('#')+' '+inlineToMarkdown(el).trim();
  }
  if(tag==='p') return inlineToMarkdown(el).trim();
  if(tag==='blockquote'){
    return '> '+inlineToMarkdown(el).replace(/\n/g,'\n> ').trim();
  }
  if(tag==='hr') return '---';
  if(tag==='pre'){
    var code=(el.textContent||'').replace(/\n+$/,'');
    return '```\n'+code+'\n```';
  }
  if(tag==='ul' || tag==='ol'){
    var lines=[];
    var li=el.querySelectorAll(':scope > li');
    for(var i=0;i<li.length;i++){
      var prefix=(tag==='ol') ? ((i+1)+'. ') : '- ';
      lines.push(prefix+inlineToMarkdown(li[i]).trim());
    }
    return lines.join('\n');
  }
  if(tag==='table'){
    return tableToMarkdown(el);
  }
  return inlineToMarkdown(el).trim();
}

function tableToMarkdown(tableEl){
  if(!tableEl) return '';
  var rows=tableEl.querySelectorAll('tr');
  if(!rows || rows.length<=0) return '';
  var cells=[], r=0, c=0, maxCols=0;
  for(r=0;r<rows.length;r++){
    var row=rows[r];
    var rowCells=row ? row.querySelectorAll('th,td') : [];
    var line=[];
    for(c=0;c<rowCells.length;c++){
      line.push(inlineToMarkdown(rowCells[c]).replace(/\n+/g,' ').trim());
    }
    if(line.length>maxCols) maxCols=line.length;
    cells.push(line);
  }
  if(maxCols<=0) return '';

  for(r=0;r<cells.length;r++){
    while(cells[r].length<maxCols) cells[r].push('');
  }

  var header=cells[0];
  var lines=['| '+header.join(' | ')+' |'];
  var sep=[];
  for(c=0;c<maxCols;c++) sep.push('---');
  lines.push('| '+sep.join(' | ')+' |');
  for(r=1;r<cells.length;r++){
    lines.push('| '+cells[r].join(' | ')+' |');
  }
  return lines.join('\n');
}

function extractAllMarkdown(){
  var wraps=document.querySelectorAll('.page-wrap');
  var lines=[];
  for(var p=0;p<wraps.length;p++){
    var cols=wraps[p].querySelectorAll('.col-content');
    for(var c=0;c<cols.length;c++){
      var children=cols[c].children;
      for(var i=0;i<children.length;i++){
        var md=blockToMarkdown(children[i]);
        if(md && md.trim()){
          lines.push(md.trim());
        }
      }
      var tablesArea=cols[c].parentNode?cols[c].parentNode.querySelector('.col-tables'):null;
      if(tablesArea){
        for(var t=0;t<tablesArea.children.length;t++){
          var tmd=blockToMarkdown(tablesArea.children[t]);
          if(tmd && tmd.trim()) lines.push(tmd.trim());
        }
      }
    }
  }
  return lines.join('\n\n');
}

function separateTablesFromColumns(){
  var colContents=document.querySelectorAll('.col-content');
  for(var i=0;i<colContents.length;i++){
    var col=colContents[i];
    var wrap=col.parentNode;
    if(!wrap) continue;
    var tablesArea=wrap.querySelector('.col-tables');
    if(!tablesArea) continue;
    tablesArea.innerHTML='';
    for(var j=col.children.length-1;j>=0;j--){
      if((col.children[j].tagName||'').toLowerCase()==='table'){
        tablesArea.insertBefore(col.children[j],tablesArea.firstChild);
      }
    }
  }
}

function normalizeTableLayout(){
  var areas=document.querySelectorAll('.col-tables');
  if(!areas || areas.length===0) return;
  var baseLinePx=Math.max(8, TEXT_SIZE_MM*PREVIEW_SCALE*Math.max(1,LINE_HEIGHT_FACTOR));
  for(var i=0;i<areas.length;i++){
    var col=areas[i];
    var tables=col.querySelectorAll('table');
    for(var t=0;t<tables.length;t++){
      var table=tables[t];
      var rows=table.querySelectorAll('tr');
      if(!rows || rows.length<=0) continue;

      var maxCols=0, r=0, c=0;
      for(r=0;r<rows.length;r++){
        var rowCells=rows[r].querySelectorAll('th,td');
        if(rowCells.length>maxCols) maxCols=rowCells.length;
      }
      if(maxCols<=0) continue;

      var colUnits=[];
      for(c=0;c<maxCols;c++) colUnits.push(2);
      for(r=0;r<rows.length;r++){
        var cells=rows[r].querySelectorAll('th,td');
        for(c=0;c<cells.length;c++){
          var metrics=calcTextMetrics(cells[c].innerText||cells[c].textContent||'');
          if(metrics.units>colUnits[c]) colUnits[c]=metrics.units;
        }
      }

      var totalUnits=0;
      for(c=0;c<colUnits.length;c++) totalUnits+=Math.max(1,colUnits[c]);
      if(totalUnits<=0) totalUnits=maxCols;

      table.style.tableLayout='fixed';
      table.style.width='100%';

      var tableW=Math.max(1, table.clientWidth || (col.clientWidth-CONTENT_PADDING_X));
      for(r=0;r<rows.length;r++){
        var row=rows[r];
        var rowCells=row.querySelectorAll('th,td');
        var rowMaxLines=1;
        for(c=0;c<rowCells.length;c++){
          var pct=(Math.max(1,colUnits[c])/totalUnits)*100;
          rowCells[c].style.width=pct.toFixed(3)+'%';
          rowCells[c].style.verticalAlign='top';

          var cellWpx=Math.max(1, tableW*(pct/100));
          var cellWmm=cellWpx/Math.max(0.01,PREVIEW_SCALE);
          var unitsPerLine=Math.max(1, Math.floor(cellWmm/CHAR_WIDTH_MM));
          var cellMetrics=calcTextMetrics(rowCells[c].innerText||rowCells[c].textContent||'');
          var cellLines=Math.max(1, Math.ceil(cellMetrics.units/unitsPerLine));
          if(cellLines>rowMaxLines) rowMaxLines=cellLines;
        }
        row.style.height=Math.max(baseLinePx*1.3, baseLinePx*rowMaxLines).toFixed(2)+'px';
      }
    }
  }
}

function renderMathInColumns(){
  if(typeof renderMathInElement!=='function') return;
  var cols=document.querySelectorAll('.col-content');
  for(var i=0;i<cols.length;i++){
    try{
      renderMathInElement(cols[i], {
        delimiters:[
          {left:'$$', right:'$$', display:true},
          {left:'\\[', right:'\\]', display:true},
          {left:'$', right:'$', display:false},
          {left:'\\(', right:'\\)', display:false}
        ],
        throwOnError:false,
        strict:'ignore',
        ignoredClasses:['katex']
      });
      var displays=cols[i].querySelectorAll('.katex-display');
      for(var d=0;d<displays.length;d++){
        displays[d].setAttribute('contenteditable','false');
      }
      var inlines=cols[i].querySelectorAll('.katex');
      for(var k=0;k<inlines.length;k++){
        inlines[k].setAttribute('contenteditable','false');
      }
    }catch(_){}
  }
}

function notifyContentChanged(){
  if(contentChangedTimer){
    clearTimeout(contentChangedTimer);
  }
  contentChangedTimer=setTimeout(function(){
    if(!window.chrome||!window.chrome.webview||!window.chrome.webview.postMessage) return;
    var markdown=extractAllMarkdown();
    window.chrome.webview.postMessage({ type:'contentChanged', markdown:markdown });
  }, 220);
}

function bindEditableColumnEvents(col,pageIndex,colIndex){
  if(!col) return;
  col.addEventListener('paste', function(ev){
    if(!ev) return;
    if(ev.preventDefault) ev.preventDefault();
    var text='';
    if(ev.clipboardData && ev.clipboardData.getData){
      text=ev.clipboardData.getData('text/plain')||'';
    }else if(window.clipboardData && window.clipboardData.getData){
      text=window.clipboardData.getData('Text')||'';
    }
    if(document.execCommand){
      document.execCommand('insertText', false, text);
    }
  });
  col.addEventListener('input', function(){
    if(isDistributing) return;
    var p=parseInt(col.getAttribute('data-page-index')||pageIndex,10);
    var c=parseInt(col.getAttribute('data-col-index')||colIndex,10);
    if(!isFinite(p)) p=0;
    if(!isFinite(c)) c=0;
    var locked=getEditingBlockInColumn(col);
    cascadeReflow(p,c,locked);
    notifyContentChanged();
  });
}

function createPage(pageIndex){
  var pagesRoot=document.getElementById('pages');
  var wrap=document.createElement('div');
  wrap.className='page-wrap';
  wrap.setAttribute('data-page-index', pageIndex);

  var paper=document.createElement('div');
  paper.className='paper';
  paper.id='paper-'+pageIndex;
  if(customPaperWidth>0) paper.style.width=customPaperWidth+'px';
  if(customPaperHeight>0) paper.style.height=customPaperHeight+'px';

  var inner=document.createElement('div');
  inner.className='paper-inner';

  var columns=[];
  for(var i=0;i<COLUMN_COUNT;i++){
    var colWrap=document.createElement('div');
    colWrap.className='col-wrap';
    if(customColumnWidths[i]>0){
      colWrap.style.webkitFlex='none';
      colWrap.style.msFlex='none';
      colWrap.style.flex='none';
      colWrap.style.width=customColumnWidths[i]+'px';
    }

    var col=document.createElement('div');
    col.className='col-content';
    col.id='col-'+pageIndex+'-'+i;
    col.setAttribute('contenteditable','true');
    col.setAttribute('data-page-index', pageIndex);
    col.setAttribute('data-col-index', i);
    bindEditableColumnEvents(col,pageIndex,i);

    var key=pageIndex+'_'+i;
    if(customColumnHeights[key]>0){
      col.style.webkitFlex='none';
      col.style.msFlex='none';
      col.style.flex='none';
      col.style.height=customColumnHeights[key]+'px';
    }

    var vHandle=document.createElement('div');
    vHandle.className='col-vhandle';
    vHandle.onmousedown=(function(p,c){ return function(ev){ startV(ev,p,c); }; })(pageIndex,i);

    var colTablesEl=document.createElement('div');
    colTablesEl.className='col-tables';

    colWrap.appendChild(col);
    colWrap.appendChild(colTablesEl);
    colWrap.appendChild(vHandle);
    inner.appendChild(colWrap);
    columns.push(col);

    if(i<COLUMN_COUNT-1){
      var gap=document.createElement('div');
      gap.className='col-gap';
      gap.onmousedown=(function(p,c){ return function(ev){ startH(ev,p,c); }; })(pageIndex,i);
      inner.appendChild(gap);
    }
  }

  paper.appendChild(inner);

  var rightHandle=document.createElement('div');
  rightHandle.className='paper-resize-right';
  rightHandle.onmousedown=(function(p){ return function(ev){ startPaperResize(ev,'right',p); }; })(pageIndex);
  paper.appendChild(rightHandle);

  var bottomHandle=document.createElement('div');
  bottomHandle.className='paper-resize-bottom';
  bottomHandle.onmousedown=(function(p){ return function(ev){ startPaperResize(ev,'bottom',p); }; })(pageIndex);
  paper.appendChild(bottomHandle);

  var cornerHandle=document.createElement('div');
  cornerHandle.className='paper-resize-corner';
  cornerHandle.onmousedown=(function(p){ return function(ev){ startPaperResize(ev,'corner',p); }; })(pageIndex);
  paper.appendChild(cornerHandle);

  wrap.appendChild(paper);
  pagesRoot.appendChild(wrap);

  return {
    pageIndex:pageIndex,
    wrap:wrap,
    paper:paper,
    columns:columns,
    colParas:createEmptyColParas()
  };
}

function createEmptyColParas(){
  var colParas=[], i=0;
  for(i=0;i<COLUMN_COUNT;i++) colParas.push([]);
  return colParas;
}

function clearPages(){
  var pagesRoot=document.getElementById('pages');
  if(pagesRoot) pagesRoot.innerHTML='';
}

function syncFooterWidths(page){
  if(!page) return;
  var wraps=page.wrap.querySelectorAll('.col-wrap');
  var items=document.querySelectorAll('.col-footer-item');
  var seps=document.querySelectorAll('.col-footer-sep');
  var gaps=page.wrap.querySelectorAll('.col-gap');
  var i;

  for(i=0;i<items.length;i++){
    var w=(i<wraps.length)?wraps[i].offsetWidth:0;
    items[i].style.webkitFlex='none';
    items[i].style.msFlex='none';
    items[i].style.flex='none';
    items[i].style.width=w+'px';
  }

  for(i=0;i<seps.length;i++){
    var gw=(i<gaps.length)?gaps[i].offsetWidth:0;
    seps[i].style.width=gw+'px';
  }
}

function renderFooterFromPage(pageStat, page){
  var i, ch, info;
  for(i=0;i<COLUMN_COUNT;i++){
    ch=document.getElementById('chars-'+i);
    info=document.getElementById('info-'+i);
    if(ch){
      var cpl=pageStat&&pageStat.charsPerColumn&&i<pageStat.charsPerColumn.length ? pageStat.charsPerColumn[i] : 0;
      ch.innerHTML=cpl+' 字/行';
    }
    if(info){
      var col=pageStat&&pageStat.columns&&i<pageStat.columns.length ? pageStat.columns[i] : null;
      var paraCount=col?col.paragraphCount:0;
      var totalChars=col?col.totalChars:0;
      info.innerHTML=paraCount+' 段 · '+totalChars+' 字';
    }
  }
  syncFooterWidths(page);
}

function setCurrentPage(index, notifyHost){
  var total=window.previewStats&&window.previewStats.pageCount ? window.previewStats.pageCount : 1;
  if(total<1) total=1;
  var next=index;
  if(next<0) next=0;
  if(next>=total) next=total-1;
  currentPageIndex=next;

  if(window.previewStats){
    window.previewStats.currentPage=currentPageIndex+1;
  }

  var pageStat=window.previewStats&&window.previewStats.pages&&window.previewStats.pages.length>currentPageIndex
    ? window.previewStats.pages[currentPageIndex]
    : null;
  var pages=document.querySelectorAll('.page-wrap');
  var page=pages.length>currentPageIndex ? {
    wrap:pages[currentPageIndex]
  } : null;
  renderFooterFromPage(pageStat, page);

  if(notifyHost){
    postPageStateToHost(currentPageIndex+1, total);
  }
}

function detectCurrentPageIndexByScroll(){
  var viewport=document.getElementById('viewport');
  var wraps=document.querySelectorAll('.page-wrap');
  if(!viewport || wraps.length===0) return 0;
  var target=viewport.scrollTop + viewport.clientHeight/2;
  var nearest=0, minDist=Number.MAX_VALUE;
  for(var i=0;i<wraps.length;i++){
    var mid=wraps[i].offsetTop + wraps[i].offsetHeight/2;
    var d=Math.abs(mid-target);
    if(d<minDist){
      minDist=d;
      nearest=i;
    }
  }
  return nearest;
}

function handleViewportScroll(){
  if(scrollTicking) return;
  scrollTicking=true;
  window.requestAnimationFrame(function(){
    scrollTicking=false;
    var idx=detectCurrentPageIndexByScroll();
    if(idx!==currentPageIndex){
      setCurrentPage(idx, true);
    }else{
      var pages=document.querySelectorAll('.page-wrap');
      if(pages.length>0 && currentPageIndex<pages.length){
        syncFooterWidths({wrap:pages[currentPageIndex]});
      }
    }
  });
}

function jumpToPage(pageNo){
  var wraps=document.querySelectorAll('.page-wrap');
  if(!wraps || wraps.length===0) return;
  var idx=(pageNo||1)-1;
  if(idx<0) idx=0;
  if(idx>=wraps.length) idx=wraps.length-1;
  var viewport=document.getElementById('viewport');
  if(viewport){
    viewport.scrollTop=Math.max(0, wraps[idx].offsetTop-2);
  }
  setCurrentPage(idx, true);
}

function getLayoutPages(layout){
  if(!layout) return [];
  if(layout.pages && layout.pages.length) return layout.pages;
  if(layout.Pages && layout.Pages.length) return layout.Pages;
  return [];
}

function getLayoutColIndices(pageObj,col){
  if(!pageObj) return [];
  var cols=pageObj.columnBlockIndices||pageObj.ColumnBlockIndices||[];
  return (cols && col<cols.length && cols[col]) ? cols[col] : [];
}

function distributeByLayout(src, els){
  var pagesDef=getLayoutPages(LAYOUT_RESULT);
  if(!pagesDef || pagesDef.length===0) return false;

  var pages=[], i=0, c=0, e=0;
  var globalBlockTypes={};
  var assignedFlags=[];
  for(i=0;i<els.length;i++) assignedFlags.push(false);
  clearPages();

  for(i=0;i<pagesDef.length;i++){
    var page=createPage(i);
    pages.push(page);
    if(customPaperWidth<=0 || customPaperHeight<=0){
      customPaperWidth=page.paper.offsetWidth;
      customPaperHeight=page.paper.offsetHeight;
    }

    for(c=0;c<COLUMN_COUNT;c++){
      var colEl=page.columns[c];
      var indices=getLayoutColIndices(pagesDef[i],c);
      for(e=0;e<indices.length;e++){
        var idx=indices[e];
        if(idx<0 || idx>=els.length) continue;
        var clone=els[idx].cloneNode(true);
        colEl.appendChild(clone);
        assignedFlags[idx]=true;
        page.colParas[c].push(idx);
        var key=normalizeBlockType(els[idx]?els[idx].tagName:'');
        globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
      }
    }
  }

  var missingCount=fillMissingBlocksByFlow(pages, els, assignedFlags, globalBlockTypes);
  var renderedCount=countRenderedBlocks(pages);
  if(renderedCount<els.length){
    // 二次兜底：异常场景下按顺序补齐，保证不丢块。
    for(i=0;i<els.length;i++){
      if(!assignedFlags[i]){
        appendBlockByFlow(pages, els, i, globalBlockTypes);
        assignedFlags[i]=true;
      }
    }
    renderedCount=countRenderedBlocks(pages);
  }
  if(renderedCount!==els.length){
    // 保守回退，避免输出部分内容导致误判。
    return false;
  }

  var pageStats=[];
  for(i=0;i<pages.length;i++){
    var pageRef=pages[i];
    if(pageRef.columns.length>0){
      pageRef.columns[pageRef.columns.length-1].className='col-content last';
    }

    var colChars=[], colStats=[], pageBlockTypes={};
    var pageDef=pagesDef[i];
    var layoutChars=pageDef ? (pageDef.charsPerColumn||pageDef.CharsPerColumn||[]) : [];

    for(c=0;c<COLUMN_COUNT;c++){
      var col=pageRef.columns[c];
      var rawText=(col.innerText||col.textContent||'');
      var metrics=calcTextMetrics(rawText);
      var paraIndices=pageRef.colParas[c]||[];

      var cpl=(layoutChars && c<layoutChars.length) ? (layoutChars[c]||0) : 0;
      if(!cpl || cpl<1){
        cpl=computeCharsPerLine(col,metrics);
      }

      colChars.push(cpl);
      colStats.push({
        index:c,
        charsPerLine:cpl,
        paragraphCount:paraIndices.length,
        totalChars:metrics.chars,
        totalDisplayUnits:metrics.units,
        avgDisplayUnitsPerChar:metrics.chars>0 ? (metrics.units/metrics.chars) : 0,
        blockTypes:buildBlockTypeCountForColumn(els, paraIndices, pageBlockTypes)
      });
    }

    pageStats.push({
      pageIndex:i,
      charsPerColumn:colChars,
      columnParagraphIndicesText:toColParasText(pageRef.colParas),
      columnParagraphIndices:pageRef.colParas,
      columns:colStats,
      blockTypeCounts:pageBlockTypes
    });
  }

  var firstPage=pageStats.length>0 ? pageStats[0] : null;
  window.colChars=firstPage ? firstPage.charsPerColumn : [];
  window.colParas=firstPage ? firstPage.columnParagraphIndices : createEmptyColParas();
  window.previewStats={
    schemaVersion:3,
    currentPage:1,
    pageCount:Math.max(1,pageStats.length),
    charsPerColumn:firstPage ? firstPage.charsPerColumn : [],
    columnParagraphIndicesText:firstPage ? firstPage.columnParagraphIndicesText : '',
    columnParagraphIndices:firstPage ? firstPage.columnParagraphIndices : createEmptyColParas(),
    columns:firstPage ? firstPage.columns : [],
    pages:pageStats,
    blockTypeCounts:globalBlockTypes
  };

  separateTablesFromColumns();
  renderMathInColumns();
  normalizeTableLayout();
  setCurrentPage(0, true);
  return true;
}

function distribute(){
  isDistributing=true;
  var src=document.getElementById('source');
  var i,e,clone,ci=0,pi=0;
  var globalBlockTypes={};
  var pages=[];

  clearPages();
  pages.push(createPage(0));
  if(customPaperWidth<=0 || customPaperHeight<=0){
    var firstPaper=pages[0].paper;
    customPaperWidth=firstPaper.offsetWidth;
    customPaperHeight=firstPaper.offsetHeight;
  }

  var els=src.children;
  if(distributeByLayout(src, els)){
    isDistributing=false;
    return;
  }

  if(!els||els.length===0){
    if(pages[0] && pages[0].columns && pages[0].columns[0]){
      pages[0].columns[0].innerHTML='<p class=""empty"">(无内容)</p>';
    }
    var emptyColumns=[], k=0;
    for(k=0;k<COLUMN_COUNT;k++) emptyColumns.push(createEmptyColumnStats(k));
    window.colChars=emptyColumns.map(function(){ return 0; });
    window.colParas=createEmptyColParas();
    window.previewStats={
      schemaVersion:3,
      currentPage:1,
      pageCount:1,
      charsPerColumn:window.colChars,
      columnParagraphIndicesText:toColParasText(window.colParas),
      columnParagraphIndices:window.colParas,
      columns:emptyColumns,
      pages:[{
        pageIndex:0,
        charsPerColumn:window.colChars,
        columnParagraphIndicesText:toColParasText(window.colParas),
        columnParagraphIndices:window.colParas,
        columns:emptyColumns,
        blockTypeCounts:{}
      }],
      blockTypeCounts:{}
    };
    setCurrentPage(0, true);
    isDistributing=false;
    return;
  }

  for(e=0;e<els.length;e++){
    var key=normalizeBlockType(els[e]?els[e].tagName:'');
    globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;

    var page=pages[pi];
    if(!page){
      page=createPage(pi);
      pages.push(page);
    }
    if(ci>=COLUMN_COUNT) ci=COLUMN_COUNT-1;

    var col=page.columns[ci];
    clone=els[e].cloneNode(true);
    col.appendChild(clone);
    if(col.scrollHeight>col.clientHeight+2){
      col.removeChild(clone);
      ci++;
      if(ci>=COLUMN_COUNT){
        pi++;
        ci=0;
      }

      page=pages[pi];
      if(!page){
        page=createPage(pi);
        pages.push(page);
      }
      page.columns[ci].appendChild(clone);
      page.colParas[ci].push(e);
    } else {
      page.colParas[ci].push(e);
    }
  }

  var pageStats=[];
  for(i=0;i<pages.length;i++){
    var pageRef=pages[i];
    if(pageRef.columns.length>0){
      pageRef.columns[pageRef.columns.length-1].className='col-content last';
    }

    var colChars=[], colStats=[], pageBlockTypes={};
    for(var c=0;c<COLUMN_COUNT;c++){
      var colEl=pageRef.columns[c];
      var rawText=(colEl.innerText||colEl.textContent||'');
      var metrics=calcTextMetrics(rawText);
      var cpl=computeCharsPerLine(colEl,metrics);
      var avgUnitsPerChar=metrics.chars>0 ? (metrics.units/metrics.chars) : 1;
      var paraIndices=pageRef.colParas[c]||[];
      colChars.push(cpl);
      colStats.push({
        index:c,
        charsPerLine:cpl,
        paragraphCount:paraIndices.length,
        totalChars:metrics.chars,
        totalDisplayUnits:metrics.units,
        avgDisplayUnitsPerChar:avgUnitsPerChar,
        blockTypes:buildBlockTypeCountForColumn(els, paraIndices, pageBlockTypes)
      });
    }

    pageStats.push({
      pageIndex:i,
      charsPerColumn:colChars,
      columnParagraphIndicesText:toColParasText(pageRef.colParas),
      columnParagraphIndices:pageRef.colParas,
      columns:colStats,
      blockTypeCounts:pageBlockTypes
    });
  }

  var firstPage=pageStats.length>0 ? pageStats[0] : {
    charsPerColumn:[],
    columnParagraphIndicesText:'',
    columnParagraphIndices:createEmptyColParas(),
    columns:[]
  };
  window.colChars=firstPage.charsPerColumn||[];
  window.colParas=firstPage.columnParagraphIndices||createEmptyColParas();
  window.previewStats={
    schemaVersion:3,
    currentPage:1,
    pageCount:Math.max(1,pageStats.length),
    charsPerColumn:firstPage.charsPerColumn||[],
    columnParagraphIndicesText:firstPage.columnParagraphIndicesText||'',
    columnParagraphIndices:firstPage.columnParagraphIndices||createEmptyColParas(),
    columns:firstPage.columns||[],
    pages:pageStats,
    blockTypeCounts:globalBlockTypes
  };

  separateTablesFromColumns();
  renderMathInColumns();
  normalizeTableLayout();
  var targetPage=Math.min(currentPageIndex, Math.max(0, pageStats.length-1));
  setCurrentPage(targetPage, true);
  isDistributing=false;
}

function startH(ev,pageIndex,idx){
  hPage=pageIndex;
  hDiv=idx;
  sX=ev.clientX;
  var page=document.querySelector('[data-page-index=""'+pageIndex+'""]');
  if(!page) return;
  var w=page.querySelectorAll('.col-wrap');
  sW=[];
  for(var i=0;i<w.length;i++) sW.push(w[i].offsetWidth);
  if(ev.preventDefault) ev.preventDefault();
}

function startV(ev,pageIndex,idx){
  vPage=pageIndex;
  vCol=idx;
  sY=ev.clientY;
  var page=document.querySelector('[data-page-index=""'+pageIndex+'""]');
  if(!page) return;
  var cols=page.querySelectorAll('.col-content');
  if(idx>=0 && idx<cols.length){
    sH=cols[idx].offsetHeight;
  }
  if(ev.preventDefault) ev.preventDefault();
}

function startPaperResize(ev,mode,pageIndex){
  var paper=document.getElementById('paper-'+pageIndex);
  if(!paper) return;
  pTargetPage=pageIndex;
  pMode=mode||'';
  pStartX=ev.clientX;
  pStartY=ev.clientY;
  pStartW=paper.offsetWidth;
  pStartH=paper.offsetHeight;
  if(ev.preventDefault) ev.preventDefault();
}

function startMiddlePan(ev){
  if(!ev || ev.button!==1) return;
  if(pMode || hDiv>=0 || vCol>=0) return;
  var viewport=document.getElementById('viewport');
  if(!viewport) return;
  middlePanActive=true;
  middlePanViewport=viewport;
  middlePanStartX=ev.clientX;
  middlePanStartY=ev.clientY;
  middlePanStartLeft=viewport.scrollLeft;
  middlePanStartTop=viewport.scrollTop;
  viewport.style.cursor='grabbing';
  document.body.style.userSelect='none';
  if(ev.preventDefault) ev.preventDefault();
}

document.onmousemove=function(ev){
  if(middlePanActive && middlePanViewport){
    var pdx=ev.clientX-middlePanStartX;
    var pdy=ev.clientY-middlePanStartY;
    middlePanViewport.scrollLeft=middlePanStartLeft-pdx;
    middlePanViewport.scrollTop=middlePanStartTop-pdy;
    if(ev.preventDefault) ev.preventDefault();
    return;
  }
  if(pMode){
    var paper=document.getElementById('paper-'+pTargetPage);
    if(!paper){
      paper=document.getElementById('paper-0');
    }
    var probeMain=paper ? paper.querySelector('.paper-inner') : null;
    if(!paper || !probeMain) return;

    var dx=ev.clientX-pStartX;
    var dy=ev.clientY-pStartY;
    var cs=window.getComputedStyle(probeMain);
    var padL=parseFloat(cs.paddingLeft)||0;
    var padR=parseFloat(cs.paddingRight)||0;
    var padT=parseFloat(cs.paddingTop)||0;
    var padB=parseFloat(cs.paddingBottom)||0;
    var minInnerW=180;
    var minInnerH=120;
    var minW=Math.max(120, Math.floor(padL+padR+minInnerW));
    var minH=Math.max(120, Math.floor(padT+padB+minInnerH));

    var nw=pStartW, nh=pStartH;
    if(pMode==='corner'){
      // 右下角默认等比缩放；按住 Ctrl 才允许自由缩放
      if(ev.ctrlKey){
        nw=pStartW+dx;
        nh=pStartH+dy;
      }else{
        var sx=(pStartW+dx)/Math.max(1,pStartW);
        var sy=(pStartH+dy)/Math.max(1,pStartH);
        var s=Math.abs(sx-1)>=Math.abs(sy-1)?sx:sy;
        var minScale=Math.max(minW/Math.max(1,pStartW), minH/Math.max(1,pStartH));
        if(s<minScale) s=minScale;
        nw=pStartW*s;
        nh=pStartH*s;
      }
    }else{
      if(pMode==='right') nw=pStartW+dx;
      if(pMode==='bottom') nh=pStartH+dy;
    }

    if(nw<minW) nw=minW;
    if(nh<minH) nh=minH;

    customPaperWidth=nw;
    customPaperHeight=nh;
    var papers=document.querySelectorAll('.paper');
    for(var p=0;p<papers.length;p++){
      papers[p].style.width=nw+'px';
      papers[p].style.height=nh+'px';
    }
    return;
  }

  if(vCol>=0){
    var dy=ev.clientY-sY;
    var page=document.querySelector('[data-page-index=""'+vPage+'""]');
    if(!page) return;
    var cols=page.querySelectorAll('.col-content');
    if(vCol>=0 && vCol<cols.length){
      var minH=80;
      var wrap=cols[vCol].parentNode;
      var handle=wrap.querySelector('.col-vhandle');
      var footer=wrap.querySelector('.col-footer');
      var reserve=0;
      if(handle) reserve+=handle.offsetHeight;
      if(footer) reserve+=footer.offsetHeight;
      // 最大高度限制在图纸页内可用区（页边距已经体现在 wrap 高度中）
      var maxH=Math.max(minH, wrap.clientHeight-reserve);
      var nh=sH+dy;
      if(nh<minH) nh=minH;
      if(nh>maxH) nh=maxH;
      customColumnHeights[vPage+'_'+vCol]=nh;
      distribute();
    }
    return;
  }

  if(hDiv>=0){
    var dx=ev.clientX-sX;
    var page=document.querySelector('[data-page-index=""'+hPage+'""]');
    if(!page) return;
    var w=page.querySelectorAll('.col-wrap');
    var li=hDiv,ri=hDiv+1;
    if(li>=w.length||ri>=w.length) return;

    var minW=80;
    var nl=sW[li]+dx;
    var nr=sW[ri]-dx;
    if(nl<minW){nr-=(minW-nl);nl=minW;}
    if(nr<minW){nl-=(minW-nr);nr=minW;}
    if(nl<minW||nr<minW) return;

    customColumnWidths[li]=nl;
    customColumnWidths[ri]=nr;
    distribute();
  }
};

document.onmouseup=function(){
  var resized = !!pMode;
  middlePanActive=false;
  if(middlePanViewport){
    middlePanViewport.style.cursor='';
  }
  middlePanViewport=null;
  document.body.style.userSelect='';
  hPage=-1;
  hDiv=-1;
  vPage=-1;
  vCol=-1;
  if(resized){
    // 拖拽中不重排，松手后一次性重排，避免画面频繁跳动
    distribute();
    var paper=document.getElementById('paper-0');
    if(paper){
      var nextScale=paper.offsetWidth/Math.max(1, PAGE_WIDTH_MM);
      postPaperScaleToHost(nextScale);
    }
  }
  pMode='';
};

window.addEventListener('wheel', function(ev){
  if(!ev || !ev.ctrlKey) return;
  postWheelZoomToHost(ev.deltaY||0);
  if(ev.preventDefault) ev.preventDefault();
}, { passive:false });

function init(){
  var viewport=document.getElementById('viewport');
  if(viewport){
    viewport.addEventListener('scroll', handleViewportScroll);
    viewport.addEventListener('mousedown', startMiddlePan, { passive:false });
    viewport.addEventListener('auxclick', function(ev){
      if(ev && ev.button===1 && ev.preventDefault){
        ev.preventDefault();
      }
    });
  }
  distribute();
}
if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,50);}
else{window.onload=init;}
";

        #endregion
    }
}
