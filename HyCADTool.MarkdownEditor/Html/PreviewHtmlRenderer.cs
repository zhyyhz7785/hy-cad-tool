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

            return "<!DOCTYPE html>\n<html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />"
                + "<style>" + dynamicCss + "</style>"
                + "</head><body>"
                + "<div id=\"source\" style=\"display:none;\">" + body + "</div>"
                + "<div class=\"viewport\" id=\"viewport\"><div class=\"pages\" id=\"pages\"></div></div>"
                + footerHtml
                + "<script>" + dynamicJs + "</script>"
                + "</body></html>";
        }

        private static string BuildDynamicCss(double previewScale, EditorConfig cfg)
        {
            double textSizeMm = Math.Max(0.1, cfg.TextSize);
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

            var tokens = new (string token, string value)[]
            {
                ("/*BASE_FONT_SIZE*/", $"{basePx.ToString("0.###", CultureInfo.InvariantCulture)}px"),
                ("/*BASE_LINE_HEIGHT*/", lineHeightFactor.ToString("0.###", CultureInfo.InvariantCulture)),
                ("/*BASE_FONT_FAMILY*/", fontFamily),
                ("/*PAPER_WIDTH*/", $"{Round(pageWidthPx):F0}px"),
                ("/*PAPER_HEIGHT*/", $"{Round(pageHeightPx):F0}px"),
                ("/*PAD_LEFT*/", $"{Round(leftPx):F0}px"),
                ("/*PAD_RIGHT*/", $"{Round(rightPx):F0}px"),
                ("/*PAD_TOP*/", $"{Round(topPx):F0}px"),
                ("/*PAD_BOTTOM*/", $"{Round(bottomPx):F0}px"),
                ("/*GUTTER*/", $"{Round(gutterVisualPx):F0}px"),
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
            const double contentPaddingX = 20.0; // .col-content 左右 padding 合计
            string layoutJson = layoutResult == null ? "null" : JsonConvert.SerializeObject(layoutResult);
            string customColumnWidths = BuildCustomColumnWidthsJson(layoutResult, safePreviewScale);

            return JS_TEMPLATE
                .Replace("/*PREVIEW_SCALE*/", safePreviewScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_SIZE_MM*/", textSizeMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_X_SCALE*/", textXScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*PAGE_WIDTH_MM*/", pageWidthMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*COLUMN_COUNT*/", columnCount.ToString(CultureInfo.InvariantCulture))
                .Replace("/*CONTENT_PADDING_X*/", contentPaddingX.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*LAYOUT_RESULT*/", layoutJson)
                .Replace("/*CUSTOM_COLUMN_WIDTHS*/", customColumnWidths);
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
  overflow-x:hidden;overflow-y:auto;
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

.col-wrap{-ms-flex:1;flex:1;display:-ms-flexbox;display:flex;-ms-flex-direction:column;flex-direction:column;min-width:0}
.col-content{-ms-flex:1;flex:1;overflow:hidden;padding:8px 10px;background:#ffffff;color:#111111}
.col-content.last{overflow:hidden}
.col-vhandle{height:6px;background:#2b3138;cursor:ns-resize;-ms-flex-negative:0;flex-shrink:0}
.col-vhandle:hover{background:#6e7681}
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
ul,ol{padding-left:18px;margin:4px 0}
li{/*LI_MARGIN*/}
code{background:#f0f3f6;color:#24292f;padding:1px 4px;border-radius:2px;font-family:Consolas,monospace;font-size:0.9em}
pre{background:#f6f8fa;border:1px solid #d0d7de;border-radius:3px;padding:6px;margin:4px 0;overflow-x:auto}
pre code{background:none;padding:0}
blockquote{border-left:3px solid #6e7781;padding:3px 10px;/*BQ_MARGIN*/;color:#333;background:#f6f8fa}
hr{border:none;border-top:1px solid #d0d7de;margin:8px 0}
table{border-collapse:collapse;width:100%;margin:6px 0}
th,td{border:1px solid #d0d7de;padding:3px 6px;font-size:0.9em;text-align:left}
th{background:#f6f8fa;color:#111;font-weight:bold}
tr:nth-child(even){background:#f8fafc}
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
var PAGE_WIDTH_MM=/*PAGE_WIDTH_MM*/;
var CONTENT_PADDING_X=/*CONTENT_PADDING_X*/;
var LAYOUT_RESULT=/*LAYOUT_RESULT*/;
var CHAR_WIDTH_MM=Math.max(0.01,TEXT_SIZE_MM*TEXT_X_SCALE);
var hPage=-1,hDiv=-1,sX=0,sW=[];
var vPage=-1,vCol=-1,sY=0,sH=0;
var pMode='',pStartX=0,pStartY=0,pStartW=0,pStartH=0,pTargetPage=0;
var currentPageIndex=0;
var customPaperWidth=0,customPaperHeight=0;
var customColumnWidths=/*CUSTOM_COLUMN_WIDTHS*/;
var customColumnHeights={};
var scrollTicking=false;

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

    colWrap.appendChild(col);
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
        page.colParas[c].push(idx);
        var key=normalizeBlockType(els[idx]?els[idx].tagName:'');
        globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
      }
    }
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
        var contentW=Math.max(1, col.clientWidth-CONTENT_PADDING_X);
        var contentMm=contentW/Math.max(0.01, PREVIEW_SCALE);
        var unitPerLine=Math.floor(contentMm/CHAR_WIDTH_MM);
        var avgUnitsPerChar=metrics.chars>0 ? (metrics.units/metrics.chars) : 1;
        cpl=Math.floor(unitPerLine/Math.max(0.5, avgUnitsPerChar));
        if(cpl<1) cpl=1;
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

  setCurrentPage(0, true);
  return true;
}

function distribute(){
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
  if(distributeByLayout(src, els)) return;

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
      var contentW=Math.max(1, colEl.clientWidth-CONTENT_PADDING_X);
      var contentMm=contentW/Math.max(0.01, PREVIEW_SCALE);
      var unitPerLine=Math.floor(contentMm/CHAR_WIDTH_MM);
      var rawText=(colEl.innerText||colEl.textContent||'');
      var metrics=calcTextMetrics(rawText);
      var avgUnitsPerChar=metrics.chars>0 ? (metrics.units/metrics.chars) : 1;
      var cpl=Math.floor(unitPerLine/Math.max(0.5, avgUnitsPerChar));
      if(cpl<1) cpl=1;
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

  var targetPage=Math.min(currentPageIndex, Math.max(0, pageStats.length-1));
  setCurrentPage(targetPage, true);
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

document.onmousemove=function(ev){
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
  }
  distribute();
}
if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,50);}
else{window.onload=init;}
";

        #endregion
    }
}
