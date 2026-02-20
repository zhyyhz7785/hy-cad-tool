using Markdig;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private const bool UseFlowExperiencePaperMode = true;
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        internal static bool IsFlowExperiencePaperMode => UseFlowExperiencePaperMode;

        public static string RenderBodyHtml(string markdown)
        {
            return string.IsNullOrEmpty(markdown)
                ? "<p class=\"empty\">(无内容)</p>"
                : Markdown.ToHtml(markdown, Pipeline);
        }

        /// <summary>
        /// Flow 模式专用 body 渲染：先做 PreserveBlankLines 预处理，与 ToFlowExperienceHtml 保持一致。
        /// 用于原地增量更新（避免 NavigateToString 销毁 DOM）。
        /// </summary>
        internal static string RenderFlowBodyHtml(string markdown)
        {
            string preprocessed = PreserveBlankLines(markdown);
            return string.IsNullOrEmpty(preprocessed)
                ? "<p class=\"empty\">(无内容)</p>"
                : Markdown.ToHtml(preprocessed, Pipeline);
        }

        public static string ToInteractiveHtml(
            string markdown,
            int columnCount,
            double previewScale = 1.0,
            EditorConfig config = null,
            LayoutResult layoutResult = null)
        {
            if (UseFlowExperiencePaperMode)
                return ToFlowExperienceHtml(markdown, columnCount, previewScale, config);

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

        private static readonly Regex ConsecutiveBlankLines = new Regex(@"\n{3,}", RegexOptions.Compiled);

        private static string PreserveBlankLines(string md)
        {
            if (string.IsNullOrEmpty(md)) return md;
            return ConsecutiveBlankLines.Replace(md, m =>
            {
                int extra = m.Value.Length - 2;
                var sb = new StringBuilder("\n\n");
                for (int i = 0; i < extra; i++)
                {
                    if (i > 0) sb.Append("\n\n");
                    sb.Append('\u2060'); // WORD JOINER, invisible, survives editor round-trip
                }
                sb.Append("\n\n");
                return sb.ToString();
            });
        }

        private static string ToFlowExperienceHtml(string markdown, int columnCount, double previewScale, EditorConfig config)
        {
            string preprocessed = PreserveBlankLines(markdown);
            string body = string.IsNullOrEmpty(preprocessed)
                ? "<p class=\"empty\">(无内容)</p>"
                : Markdown.ToHtml(preprocessed, Pipeline);
            EditorConfig cfg = config ?? new EditorConfig();
            int cols = Math.Max(1, Math.Min(10, columnCount));
            double scale = Math.Max(0.1, Math.Min(5.0, previewScale));
            double textSizeMm = Math.Max(0.1, cfg.TextSize);
            double textXScale = Math.Max(0.1, cfg.TextXScale);
            double fontWidthFactor = ResolveFontWidthFactor(cfg);
            double basePx = textSizeMm * scale;
            double charWidthPx = Math.Max(4, basePx * textXScale * fontWidthFactor);
            double paperWidthPx = Round(cfg.PageWidthMm * scale);
            double paperHeightPx = Round(cfg.PageHeightMm * scale);
            double leftPx = Round(cfg.MarginLeftMm * scale);
            double rightPx = Round(cfg.MarginRightMm * scale);
            double topPx = Round(cfg.MarginTopMm * scale);
            double bottomPx = Round(cfg.MarginBottomMm * scale);
            double gapPx = Math.Max(6, Round(Math.Max(0, cfg.ColumnGutter) * scale));
            double borderWidth = Math.Max(0.5, Math.Min(5, cfg.BorderWidth));
            double handleWidth = Math.Max(1, Math.Min(10, cfg.HandleWidth));
            double handleActiveWidth = Math.Max(handleWidth, Math.Min(14, cfg.HandleActiveWidth));
            string footerHtml = BuildFooterHtml(cols);
            string fontFamily = ResolvePreviewFontFamily(cfg);

            const string flowCssTemplate = @"
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{
  overflow:hidden;
  background:#0d1117;
  color:#d4d4d4;
  font-family:/*FLOW_FONT_FAMILY*/;
  font-size:/*FLOW_BASE_FONT_SIZE*/;
  line-height:/*FLOW_LINE_HEIGHT*/;
}
.viewport{
  width:100%;
  height:calc(100% - 24px);
  overflow:auto;
  padding:0 6px 12px 0;
}
.pages-flow{
  display:flex;
  flex-direction:column;
  gap:14px;
  align-items:flex-start;
}
.paper-wrap{position:relative}
.paper{
  position:relative;
  width:/*FLOW_PAPER_WIDTH*/;
  height:/*FLOW_PAPER_HEIGHT*/;
  background:#ffffff;
  color:#111111;
  overflow:hidden;
}
.paper-inner{
  position:absolute;
  left:/*FLOW_PAD_LEFT*/;
  right:/*FLOW_PAD_RIGHT*/;
  top:/*FLOW_PAD_TOP*/;
  bottom:/*FLOW_PAD_BOTTOM*/;
  display:flex;
  flex-direction:row;
  align-items:flex-start;
  overflow:hidden;
  background:#f8fafc;
  z-index:5;
}
.col-wrap{
  display:flex;
  flex-direction:column;
  flex:1;
  padding-top:/*FLOW_BORDER_WIDTH*/;
  padding-bottom:/*FLOW_BORDER_WIDTH*/;
  min-width:80px;
  min-height:0;
  overflow:hidden;
}
.col-top-handle,
.col-bottom-handle{
  position:relative;
  z-index:7;
  height:/*FLOW_HANDLE_WIDTH*/;
  flex-shrink:0;
  background:#58a6ff;
  cursor:ns-resize;
  transition:height 0.15s ease;
}
.col-top-handle:hover,
.col-bottom-handle:hover,
.col-top-handle:active,
.col-bottom-handle:active{height:/*FLOW_HANDLE_ACTIVE_WIDTH*/}
.col-top-spacer{
  flex:none;
  height:0;
  flex-shrink:0;
}
.col-content{
  flex:none;
  min-height:80px;
  overflow:hidden;
  padding:8px 10px;
  transform-origin:left top;
  transform:scaleX(/*FLOW_TEXT_X_SCALE_CSS*/);
  width:calc(100% / /*FLOW_TEXT_X_SCALE_CSS*/);
  background:#ffffff;
  color:#111111;
  outline:none;
  caret-color:#111111;
}
.col-content.active{
  box-shadow:inset 0 0 0 1px #58a6ff;
}
.col-gap-handle{
  width:/*FLOW_GAP_PX*/;
  min-width:6px;
  flex-shrink:0;
  cursor:ew-resize;
  position:relative;
  z-index:6;
  align-self:stretch;
  background:transparent;
}
.col-gap-handle:before{
  content:'';
  position:absolute;
  left:50%;
  top:0;
  bottom:0;
  width:1px;
  margin-left:-0.5px;
  background:#6e7681;
}
.col-gap-handle:hover{background:#2f3942}
.col-gap-handle:hover:before{width:2px;margin-left:-1px;background:#58a6ff}
.paper-resize-right{
  position:absolute;
  top:0;
  right:0;
  width:6px;
  height:100%;
  cursor:ew-resize;
  background:transparent;
}
.paper-resize-bottom{
  position:absolute;
  left:0;
  bottom:0;
  width:100%;
  height:6px;
  cursor:ns-resize;
  background:transparent;
}
.paper-resize-corner{
  position:absolute;
  right:0;
  bottom:0;
  width:12px;
  height:12px;
  cursor:nwse-resize;
  background:#30363d;
  border-top:1px solid #6e7681;
  border-left:1px solid #6e7681;
}
.margin-guide{
  position:absolute;
  z-index:6;
  background:#000000;
  pointer-events:none;
}
.margin-guide.left,.margin-guide.right{
  top:/*FLOW_PAD_TOP*/;
  bottom:/*FLOW_PAD_BOTTOM*/;
  width:/*FLOW_BORDER_WIDTH*/;
}
.margin-guide.top,.margin-guide.bottom{
  left:/*FLOW_PAD_LEFT*/;
  right:/*FLOW_PAD_RIGHT*/;
  height:/*FLOW_BORDER_WIDTH*/;
}
.margin-guide.left{left:/*FLOW_PAD_LEFT*/}
.margin-guide.right{right:/*FLOW_PAD_RIGHT*/}
.margin-guide.top{top:/*FLOW_PAD_TOP*/}
.margin-guide.bottom{bottom:/*FLOW_PAD_BOTTOM*/}
.paper-footer{
  display:flex;
  flex-direction:row;
  height:24px;
  padding:4px 0;
  color:#8b949e;
  font-size:11px;
  border-top:1px solid #24292e;
  background:#111418;
  flex-shrink:0;
}
.col-footer-item{flex:1;text-align:center}
.col-footer-sep{
  width:/*FLOW_GAP_PX*/;
  min-width:6px;
  background:transparent;
  flex-shrink:0;
  position:relative;
}
.col-footer-sep:before{
  content:'';
  position:absolute;
  left:50%;
  top:0;
  bottom:0;
  width:1px;
  margin-left:-0.5px;
  background:#4b5560;
}
.col-chars{color:#58a6ff}
.empty{color:#6e7681;font-style:italic}
h1{font-size:1.6em;margin:1.2em 0 .7em;border-bottom:1px solid #d0d7de;padding-bottom:3px}
h2{font-size:1.3em;margin:1.0em 0 .5em}
h3{font-size:1.1em;margin:.9em 0 .4em}
h4,h5,h6{font-size:1em;margin:.8em 0 .3em}
p{margin:0 0 .5em}
ul,ol{padding-left:1.4em;margin:.3em 0}
li{margin:0 0 .2em}
blockquote{border-left:3px solid #6e7781;padding:3px .8em;margin:.5em 0;color:#333;background:#f6f8fa}
code{background:#f0f3f6;color:#24292f;padding:1px 4px;border-radius:2px;font-family:Consolas,monospace;font-size:.9em}
pre{background:#f6f8fa;border:1px solid #d0d7de;border-radius:3px;padding:6px;margin:4px 0;overflow-x:auto}
pre code{background:none;padding:0}
table{border-collapse:collapse;width:auto;max-width:100%;margin:6px 0}
th,td{border:1px solid #d0d7de;padding:3px 6px;font-size:.9em;text-align:left;word-break:break-word;overflow-wrap:break-word}
th{background:#f6f8fa;color:#111;font-weight:bold}
tr:nth-child(even){background:#f8fafc}
";

            const string flowJsTemplate = @"
var PREVIEW_SCALE=/*FLOW_PREVIEW_SCALE*/;
var CHAR_WIDTH_PX=/*FLOW_CHAR_WIDTH_PX*/;
var COLUMN_COUNT=/*FLOW_COLUMN_COUNT*/;
var PAPER_WIDTH=/*FLOW_PAPER_WIDTH_NUM*/;
var PAPER_HEIGHT=/*FLOW_PAPER_HEIGHT_NUM*/;
var MARGIN_LEFT=/*FLOW_PAD_LEFT_NUM*/;
var MARGIN_RIGHT=/*FLOW_PAD_RIGHT_NUM*/;
var MARGIN_TOP=/*FLOW_PAD_TOP_NUM*/;
var MARGIN_BOTTOM=/*FLOW_PAD_BOTTOM_NUM*/;
var COLUMN_GAP=/*FLOW_GAP_NUM*/;
var HANDLE_WIDTH=/*FLOW_HANDLE_WIDTH_NUM*/;
var FRAME_BORDER_WIDTH=/*FLOW_BORDER_WIDTH_NUM*/;

var sourceRoot=null;
var pagesFlowEl=null;
var viewportEl=null;
var mainPaperWrap=null;
var mainPaperEl=null;
var mainInnerEl=null;
var footerEl=null;

var pageWraps=[];
var pagePapers=[];
var pageInners=[];
var pageColumns=[];
var currentPageIndex=0;

var contentVersion=0;
var lastSentHash='';
var notifyTimer=0;
var reflowTimer=0;

var middlePanActive=false;
var middlePanViewport=null;
var middlePanStartX=0;
var middlePanStartY=0;
var middlePanStartLeft=0;
var middlePanStartTop=0;

var dragMode='';
var dragIndex=-1;
var dragStartX=0;
var dragStartY=0;
var dragStartState=null;

var columnWidths=[];
var columnHeights=[];
var columnTopOffsets=[];

window.colChars=[];
window.colParas=[];
window.previewStats={schemaVersion:3,currentPage:1,pageCount:1,charsPerColumn:[],columnParagraphIndicesText:'',columnParagraphIndices:[],columns:[],pages:[],blockTypeCounts:{}};

function clampNumber(value,min,max,fallback){
  var n=Number(value);
  if(!isFinite(n)) return fallback;
  if(n<min) return min;
  if(n>max) return max;
  return n;
}

function simpleHash(text){
  text=text||'';
  var hash=0;
  for(var i=0;i<text.length;i++){
    hash=((hash<<5)-hash)+text.charCodeAt(i);
    hash|=0;
  }
  return String(hash);
}

function toPx(value){
  var n=Number(value);
  if(!isFinite(n)) n=0;
  return n.toFixed(3).replace(/\.?0+$/,'') + 'px';
}

function getHost(){
  if(!window.chrome||!window.chrome.webview||!window.chrome.webview.postMessage) return null;
  return window.chrome.webview;
}

function postHost(msg){
  var host=getHost();
  if(!host) return;
  host.postMessage(msg);
}

function postPageState(){
  postHost({
    type:'pageState',
    currentPage:Math.max(1,currentPageIndex+1),
    pageCount:Math.max(1,pageWraps.length)
  });
}

function postPaperScale(){ postHost({type:'paperScale', value:PREVIEW_SCALE}); }

function postPaperGeometry(){
  postHost({
    type:'paperGeometry',
    pageWidthMm:PAPER_WIDTH/Math.max(0.01,PREVIEW_SCALE),
    pageHeightMm:PAPER_HEIGHT/Math.max(0.01,PREVIEW_SCALE)
  });
}

function postPaperMargins(){
  postHost({
    type:'paperMargins',
    marginLeftMm:MARGIN_LEFT/Math.max(0.01,PREVIEW_SCALE),
    marginRightMm:MARGIN_RIGHT/Math.max(0.01,PREVIEW_SCALE),
    marginTopMm:MARGIN_TOP/Math.max(0.01,PREVIEW_SCALE),
    marginBottomMm:MARGIN_BOTTOM/Math.max(0.01,PREVIEW_SCALE)
  });
}

function createEmptyColParas(){
  var colParas=[], i=0;
  for(i=0;i<COLUMN_COUNT;i++) colParas.push([]);
  return colParas;
}

function toColParasText(colParas){
  var parts=[];
  if(!colParas) return '';
  for(var i=0;i<colParas.length;i++) parts.push((colParas[i]||[]).join(','));
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
  return t || 'unknown';
}

function getInnerWidth(){
  return Math.max(120, PAPER_WIDTH - MARGIN_LEFT - MARGIN_RIGHT);
}

function getInnerHeight(){
  return Math.max(100, PAPER_HEIGHT - MARGIN_TOP - MARGIN_BOTTOM);
}

function ensureMarginBounds(){
  var maxH=Math.max(0, PAPER_WIDTH-120);
  var maxV=Math.max(0, PAPER_HEIGHT-100);
  MARGIN_LEFT=clampNumber(MARGIN_LEFT,0,maxH,0);
  MARGIN_RIGHT=clampNumber(MARGIN_RIGHT,0,maxH,0);
  MARGIN_TOP=clampNumber(MARGIN_TOP,0,maxV,0);
  MARGIN_BOTTOM=clampNumber(MARGIN_BOTTOM,0,maxV,0);
  if(MARGIN_LEFT+MARGIN_RIGHT>maxH){
    var ofh=MARGIN_LEFT+MARGIN_RIGHT-maxH;
    MARGIN_RIGHT=Math.max(0, MARGIN_RIGHT-ofh);
  }
  if(MARGIN_TOP+MARGIN_BOTTOM>maxV){
    var ofv=MARGIN_TOP+MARGIN_BOTTOM-maxV;
    MARGIN_BOTTOM=Math.max(0, MARGIN_BOTTOM-ofv);
  }
}

function resetColumnCaches(){
  columnWidths=[];
  columnHeights=[];
  columnTopOffsets=[];
}

function getDefaultColumnWidth(){
  var totalGap=Math.max(0,COLUMN_COUNT-1)*COLUMN_GAP;
  return Math.max(80, (getInnerWidth()-totalGap)/Math.max(1,COLUMN_COUNT));
}

function getDefaultColumnHeight(){
  return Math.max(80, getInnerHeight()-HANDLE_WIDTH*2-FRAME_BORDER_WIDTH*2);
}

function normalizeColumnCaches(){
  var defaultWidth=getDefaultColumnWidth();
  var defaultHeight=getDefaultColumnHeight();
  while(columnWidths.length<COLUMN_COUNT) columnWidths.push(defaultWidth);
  while(columnHeights.length<COLUMN_COUNT) columnHeights.push(defaultHeight);
  while(columnTopOffsets.length<COLUMN_COUNT) columnTopOffsets.push(0);
  if(columnWidths.length>COLUMN_COUNT) columnWidths.length=COLUMN_COUNT;
  if(columnHeights.length>COLUMN_COUNT) columnHeights.length=COLUMN_COUNT;
  if(columnTopOffsets.length>COLUMN_COUNT) columnTopOffsets.length=COLUMN_COUNT;
  var maxInner=getInnerHeight()-HANDLE_WIDTH*2-FRAME_BORDER_WIDTH*2;
  for(var i=0;i<COLUMN_COUNT;i++){
    columnWidths[i]=Math.max(80,columnWidths[i]);
    columnTopOffsets[i]=Math.max(0, Math.min(maxInner-80, columnTopOffsets[i]||0));
    columnHeights[i]=Math.max(80, Math.min(maxInner-columnTopOffsets[i], columnHeights[i]));
  }
}

function redistributeColumnWidthsEvenly(){
  var defaultWidth=getDefaultColumnWidth();
  var defaultHeight=getDefaultColumnHeight();
  for(var i=0;i<COLUMN_COUNT;i++){
    columnWidths[i]=defaultWidth;
    columnHeights[i]=defaultHeight;
    columnTopOffsets[i]=0;
  }
  normalizeColumnCaches();
}

function syncFooterSeparators(){
  var seps=document.querySelectorAll('.col-footer-sep');
  for(var i=0;i<seps.length;i++){
    seps[i].style.width=toPx(COLUMN_GAP);
    seps[i].style.minWidth='6px';
  }
}

function rebuildFooter(){
  if(!footerEl) return;
  var html='';
  for(var i=0;i<COLUMN_COUNT;i++){
    html += '<span class=""col-footer-item"">第 '+(i+1)+' 栏 <span class=""col-chars"" id=""chars-'+i+'""></span> · <span id=""info-'+i+'""></span></span>';
    if(i<COLUMN_COUNT-1){
      html += '<span class=""col-footer-sep""></span>';
    }
  }
  footerEl.innerHTML=html;
  syncFooterSeparators();
}

function clearActiveColumns(){
  var cols=document.querySelectorAll('.col-content.active');
  for(var i=0;i<cols.length;i++) cols[i].classList.remove('active');
}

function setActiveColumn(col){
  if(!col) return;
  clearActiveColumns();
  col.classList.add('active');
}

function getAllColumnsOrdered(){
  var cols=[];
  for(var p=0;p<pageColumns.length;p++){
    var row=pageColumns[p] || [];
    for(var c=0;c<row.length;c++){
      cols.push(row[c]);
    }
  }
  return cols;
}

function getNodePath(root,node){
  var path=[];
  var cur=node;
  while(cur && cur!==root){
    var parent=cur.parentNode;
    if(!parent) return [];
    var idx=0;
    var child=parent.firstChild;
    while(child && child!==cur){
      idx++;
      child=child.nextSibling;
    }
    path.unshift(idx);
    cur=parent;
  }
  return cur===root ? path : [];
}

function resolveNodeByPath(root,path){
  if(!root || !path) return null;
  var cur=root;
  for(var i=0;i<path.length;i++){
    if(!cur || !cur.childNodes || path[i]<0 || path[i]>=cur.childNodes.length) return null;
    cur=cur.childNodes[path[i]];
  }
  return cur;
}

function getBlockElementFromNode(col,node){
  var cur=node;
  if(cur && cur.nodeType===3) cur=cur.parentNode;
  while(cur && cur!==col){
    if(cur.parentNode===col) return cur;
    cur=cur.parentNode;
  }
  return null;
}

function setCaretToNode(sel,node,offset){
  if(!sel || !node) return false;
  try{
    var range=document.createRange();
    if(node.nodeType===3){
      range.setStart(node, Math.min(Math.max(0,offset||0), node.nodeValue ? node.nodeValue.length : 0));
    }else{
      var cc=node.childNodes ? node.childNodes.length : 0;
      range.setStart(node, Math.min(Math.max(0,offset||0), cc));
    }
    range.collapse(true);
    sel.removeAllRanges();
    sel.addRange(range);
    return true;
  }catch(_){
    return false;
  }
}

function setCaretToBlockEnd(sel,blockNode){
  if(!sel || !blockNode) return false;
  var walker=document.createTreeWalker(blockNode, NodeFilter.SHOW_TEXT, null, false);
  var lastText=null;
  while(walker.nextNode()){
    lastText=walker.currentNode;
  }
  if(lastText){
    return setCaretToNode(sel,lastText,lastText.nodeValue ? lastText.nodeValue.length : 0);
  }
  return setCaretToNode(sel,blockNode,blockNode.childNodes ? blockNode.childNodes.length : 0);
}

function captureCaretBookmark(col){
  if(!col || document.activeElement!==col) return null;
  var sel=window.getSelection ? window.getSelection() : null;
  if(!sel || sel.rangeCount<=0) return null;
  var range=sel.getRangeAt(0);
  var path=getNodePath(col, range.startContainer);
  var block=getBlockElementFromNode(col, range.startContainer);
  var blockIndex=-1;
  var textOffset=-1;
  if(block && block.getAttribute){
    var raw=block.getAttribute('data-block-index');
    var parsed=parseInt(raw||'',10);
    if(isFinite(parsed)) blockIndex=parsed;
    textOffset=getTextOffsetInBlock(block, range.startContainer, range.startOffset);
  }
  var pageIndex=parseInt(col.getAttribute('data-page-index')||'0',10);
  var colIndex=parseInt(col.getAttribute('data-col-index')||'0',10);
  return {
    path:path,
    offset:range.startOffset||0,
    blockIndex:blockIndex,
    textOffset:textOffset,
    pageIndex:isFinite(pageIndex)?pageIndex:0,
    colIndex:isFinite(colIndex)?colIndex:0
  };
}

function getTextOffsetInBlock(block, container, offset){
  if(!block) return -1;
  var walker=document.createTreeWalker(block, NodeFilter.SHOW_TEXT, null, false);
  var total=0;
  while(walker.nextNode()){
    if(walker.currentNode===container) return total+(offset||0);
    total+=(walker.currentNode.nodeValue||'').length;
  }
  return total;
}

function resolveTextOffset(block, textOffset){
  if(!block || !isFinite(textOffset) || textOffset<0) return null;
  var walker=document.createTreeWalker(block, NodeFilter.SHOW_TEXT, null, false);
  var remaining=textOffset;
  while(walker.nextNode()){
    var len=(walker.currentNode.nodeValue||'').length;
    if(remaining<=len) return {node:walker.currentNode, offset:remaining};
    remaining-=len;
  }
  return null;
}

function findBlockAcrossColumns(blockIndex){
  if(!isFinite(blockIndex) || blockIndex<0) return null;
  var nodes=document.querySelectorAll('[data-block-index='+String(blockIndex)+']');
  if(!nodes || nodes.length===0) return null;
  var node=nodes[0];
  var col=node.parentNode;
  while(col && !(col.classList && col.classList.contains('col-content'))){
    col=col.parentNode;
  }
  if(!col) return null;
  return {col:col,node:node};
}

function restoreCaretBookmark(bookmark){
  if(!bookmark) return;
  var sel=window.getSelection ? window.getSelection() : null;
  if(!sel) return;
  var col=null;
  if(isFinite(bookmark.pageIndex) && isFinite(bookmark.colIndex)){
    var row=pageColumns[bookmark.pageIndex] || [];
    col=row[bookmark.colIndex] || null;
  }
  if(!col){
    var all=getAllColumnsOrdered();
    col=all.length>0 ? all[0] : null;
  }
  if(!col) return;

  // 策略 1：按 DOM path 精确定位
  var node=(bookmark.path && bookmark.path.length) ? resolveNodeByPath(col, bookmark.path) : null;
  if(node && !col.contains(node)) node=null;
  if(node){
    var ok=setCaretToNode(sel,node,bookmark.offset||0);
    if(ok){ setActiveColumn(col); try{col.focus();}catch(_){} return; }
  }

  // 策略 2：按 blockIndex 找到块，再用 textOffset 精确定位到字符
  if(isFinite(bookmark.blockIndex) && bookmark.blockIndex>=0){
    var located=findBlockAcrossColumns(bookmark.blockIndex);
    if(located){
      col=located.col;
      var block=located.node;
      if(isFinite(bookmark.textOffset) && bookmark.textOffset>=0){
        var resolved=resolveTextOffset(block, bookmark.textOffset);
        if(resolved){
          var ok2=setCaretToNode(sel, resolved.node, resolved.offset);
          if(ok2){ setActiveColumn(col); try{col.focus();}catch(_){} return; }
        }
      }
      // textOffset 失败则定位到块末尾
      setCaretToBlockEnd(sel, block);
      setActiveColumn(col);
      try{col.focus();}catch(_){}
      return;
    }
  }

  // 策略 3：最后兜底，定位到栏末尾
  node=null;
  if(col.children && col.children.length>0){
    node=col.children[col.children.length-1];
  }else{
    node=col;
  }
  setCaretToBlockEnd(sel, node.nodeType===1 ? node : (node.parentNode || col));
  setActiveColumn(col);
  try{ col.focus(); }catch(_){}
}

function cloneNodeList(nodes){
  var blocks=[], i=0;
  for(i=0;i<nodes.length;i++) blocks.push(nodes[i].cloneNode(true));
  return blocks;
}

function collectBlocksFromSource(){
  if(!sourceRoot) return [];
  var blocks=cloneNodeList(sourceRoot.children || []);
  if(blocks.length===0){
    var p=document.createElement('p');
    p.className='empty';
    p.textContent='(无内容)';
    blocks.push(p);
  }
  return blocks;
}

function collectBlocksFromColumns(){
  var blocks=[];
  for(var p=0;p<pageColumns.length;p++){
    var row=pageColumns[p] || [];
    for(var c=0;c<row.length;c++){
      var col=row[c];
      var children=col ? (col.children || []) : [];
      for(var i=0;i<children.length;i++) blocks.push(children[i].cloneNode(true));
    }
  }
  if(blocks.length===0) return collectBlocksFromSource();
  return blocks;
}

function nextPosition(pageIndex,colIndex){
  var nc=colIndex+1;
  var np=pageIndex;
  if(nc>=COLUMN_COUNT){
    nc=0;
    np++;
  }
  return { pageIndex:np, colIndex:nc };
}

function buildPageColumns(pageIndex){
  normalizeColumnCaches();
  var inner=pageInners[pageIndex];
  if(!inner) return;
  inner.innerHTML='';
  pageColumns[pageIndex]=[];
  for(var c=0;c<COLUMN_COUNT;c++){
    var wrap=document.createElement('div');
    wrap.className='col-wrap';
    wrap.setAttribute('data-col-index', String(c));
    wrap.style.width=toPx(columnWidths[c]);
    wrap.style.flex='0 0 auto';

    var topHandle=document.createElement('div');
    topHandle.className='col-top-handle';
    bindDragHandle(topHandle, 'col-top', c);

    var topSpacer=document.createElement('div');
    topSpacer.className='col-top-spacer';
    topSpacer.style.height=toPx(columnTopOffsets[c]||0);

    var col=document.createElement('div');
    col.className='col-content';
    col.setAttribute('contenteditable','true');
    col.setAttribute('data-page-index', String(pageIndex));
    col.setAttribute('data-col-index', String(c));
    col.style.height=toPx(columnHeights[c]);
    bindColumnInputEvents(col);

    var bottomHandle=document.createElement('div');
    bottomHandle.className='col-bottom-handle';
    bindDragHandle(bottomHandle, 'col-bottom', c);

    wrap.appendChild(topSpacer);
    wrap.appendChild(topHandle);
    wrap.appendChild(col);
    wrap.appendChild(bottomHandle);
    inner.appendChild(wrap);
    pageColumns[pageIndex].push(col);

    if(c<COLUMN_COUNT-1){
      var gap=document.createElement('div');
      gap.className='col-gap-handle';
      gap.style.width=toPx(COLUMN_GAP);
      bindDragHandle(gap, 'col-gap', c);
      inner.appendChild(gap);
    }
  }
}

function createExtraPage(pageIndex){
  var wrap=document.createElement('div');
  wrap.className='paper-wrap';
  wrap.setAttribute('data-page-index', String(pageIndex));

  var paper=document.createElement('div');
  paper.className='paper';
  paper.style.width=toPx(PAPER_WIDTH);
  paper.style.height=toPx(PAPER_HEIGHT);

  var inner=document.createElement('div');
  inner.className='paper-inner';
  inner.style.left=toPx(MARGIN_LEFT);
  inner.style.right=toPx(MARGIN_RIGHT);
  inner.style.top=toPx(MARGIN_TOP);
  inner.style.bottom=toPx(MARGIN_BOTTOM);

  paper.appendChild(inner);
  wrap.appendChild(paper);
  pagesFlowEl.appendChild(wrap);
  pageWraps.push(wrap);
  pagePapers.push(paper);
  pageInners.push(inner);
  buildPageColumns(pageIndex);
}

function resetToSinglePage(){
  var wraps=pagesFlowEl.querySelectorAll('.paper-wrap');
  for(var i=1;i<wraps.length;i++){
    if(wraps[i].parentNode) wraps[i].parentNode.removeChild(wraps[i]);
  }
  pageWraps=[mainPaperWrap];
  pagePapers=[mainPaperEl];
  pageInners=[mainInnerEl];
  pageColumns=[];
  mainPaperWrap.setAttribute('data-page-index','0');
  buildPageColumns(0);
}

function ensurePage(pageIndex){
  while(pageInners.length<=pageIndex){
    createExtraPage(pageInners.length);
  }
}

function trimPagesTo(keepCount){
  var safe=Math.max(1,keepCount);
  while(pageInners.length>safe){
    var idx=pageInners.length-1;
    var wrap=pageWraps[idx];
    if(wrap && wrap.parentNode) wrap.parentNode.removeChild(wrap);
    pageWraps.pop();
    pagePapers.pop();
    pageInners.pop();
    pageColumns.pop();
  }
}

function getColumn(pageIndex,colIndex){
  ensurePage(pageIndex);
  if(!pageColumns[pageIndex]) buildPageColumns(pageIndex);
  return (pageColumns[pageIndex] && colIndex>=0 && colIndex<pageColumns[pageIndex].length)
    ? pageColumns[pageIndex][colIndex]
    : null;
}

function isColumnOverflow(col){
  if(!col) return false;
  return col.scrollHeight > col.clientHeight + 2;
}

function isParagraphSplittable(block){
  if(!block || block.nodeType!==1) return false;
  var tag=(block.tagName||'').toLowerCase();
  if(tag!=='p' && tag!=='blockquote') return false;
  if(block.children && block.children.length>0) return false;
  var text=(block.innerText||block.textContent||'').replace(/\r/g,'');
  return text.length>1;
}

function findSafeSplitIndex(text, idx){
  if(!text) return idx;
  var safe=Math.max(1, Math.min(idx, text.length-1));
  var left=safe;
  while(left>1){
    var ch=text.charAt(left);
    if(/\s/.test(ch)) return left;
    left--;
    if(safe-left>24) break;
  }
  return safe;
}

function splitParagraphToNext(pageIndex,colIndex,blockIndex){
  var col=getColumn(pageIndex,colIndex);
  if(!col || !col.lastElementChild) return false;
  var block=col.lastElementChild;
  if(!isParagraphSplittable(block)) return false;
  var original=(block.innerText||block.textContent||'').replace(/\r/g,'');
  if(original.length<2) return false;

  var low=1, high=original.length-1, best=-1;
  while(low<=high){
    var mid=(low+high)>>1;
    var probe=findSafeSplitIndex(original, mid);
    block.textContent=original.slice(0, probe);
    if(!isColumnOverflow(col)){
      best=probe;
      low=mid+1;
    }else{
      high=mid-1;
    }
  }
  if(best<=0 || best>=original.length){
    block.textContent=original;
    return false;
  }
  var head=original.slice(0,best).replace(/\s+$/,'');
  var tail=original.slice(best).replace(/^\s+/,'');
  if(!head || !tail){
    block.textContent=original;
    return false;
  }
  block.textContent=head;
  if(isColumnOverflow(col)){
    block.textContent=original;
    return false;
  }
  var nextPos=nextPosition(pageIndex,colIndex);
  var nextCol=getColumn(nextPos.pageIndex,nextPos.colIndex);
  var tailNode=block.cloneNode(false);
  tailNode.textContent=tail;
  tailNode.setAttribute('data-block-index', String(blockIndex));
  nextCol.insertBefore(tailNode,nextCol.firstChild);
  return true;
}

function normalizeOverflowFrom(pageIndex,colIndex){
  var guard=0;
  while(guard<4000){
    guard++;
    var changed=false;
    for(var p=pageIndex;p<pageInners.length;p++){
      for(var c=(p===pageIndex?colIndex:0); c<COLUMN_COUNT; c++){
        var col=getColumn(p,c);
        while(isColumnOverflow(col)){
          var last=col.lastElementChild;
          if(!last) break;
          var blockIndex=parseInt(last.getAttribute('data-block-index')||'-1',10);
          if(col.children.length===1){
            if(isFinite(blockIndex) && splitParagraphToNext(p,c,blockIndex)){
              changed=true;
              continue;
            }
            break;
          }
          col.removeChild(last);
          var nextPos=nextPosition(p,c);
          var nextCol=getColumn(nextPos.pageIndex,nextPos.colIndex);
          nextCol.insertBefore(last,nextCol.firstChild);
          changed=true;
        }
      }
    }
    if(!changed) break;
  }
}

function syncColumnSizesToDom(){
  normalizeColumnCaches();
  for(var p=0;p<pageInners.length;p++){
    var wraps=pageInners[p].querySelectorAll('.col-wrap');
    var cols=pageInners[p].querySelectorAll('.col-content');
    var spacers=pageInners[p].querySelectorAll('.col-top-spacer');
    for(var i=0;i<wraps.length;i++){
      var idx=parseInt(wraps[i].getAttribute('data-col-index')||'0',10);
      if(!isFinite(idx) || idx<0 || idx>=COLUMN_COUNT) idx=0;
      wraps[i].style.width=toPx(columnWidths[idx]);
    }
    for(var c=0;c<cols.length;c++){
      var ci=parseInt(cols[c].getAttribute('data-col-index')||'0',10);
      if(!isFinite(ci) || ci<0 || ci>=COLUMN_COUNT) ci=0;
      cols[c].style.height=toPx(columnHeights[ci]);
    }
    for(var s=0;s<spacers.length;s++){
      var wrap=spacers[s].parentElement;
      var si=wrap ? parseInt(wrap.getAttribute('data-col-index')||'0',10) : 0;
      if(!isFinite(si) || si<0 || si>=COLUMN_COUNT) si=0;
      spacers[s].style.height=toPx(columnTopOffsets[si]||0);
    }
    var gaps=pageInners[p].querySelectorAll('.col-gap-handle');
    for(var g=0;g<gaps.length;g++) gaps[g].style.width=toPx(COLUMN_GAP);
  }
  syncFooterSeparators();
}

function distributeBlocksSequential(blocks){
  resetToSinglePage();
  normalizeColumnCaches();
  syncColumnSizesToDom();

  var pageIndex=0;
  var colIndex=0;
  for(var b=0;b<blocks.length;b++){
    var placed=false;
    var loopGuard=0;
    while(!placed && loopGuard<2000){
      loopGuard++;
      var col=getColumn(pageIndex,colIndex);
      var node=blocks[b].cloneNode(true);
      node.setAttribute('data-block-index', String(b));
      col.appendChild(node);
      if(!isColumnOverflow(col)){
        placed=true;
        break;
      }
      if(col.children.length===1){
        if(!splitParagraphToNext(pageIndex,colIndex,b)){
          placed=true;
          var n0=nextPosition(pageIndex,colIndex);
          pageIndex=n0.pageIndex;
          colIndex=n0.colIndex;
        }else{
          normalizeOverflowFrom(pageIndex,colIndex);
          var n1=nextPosition(pageIndex,colIndex);
          pageIndex=n1.pageIndex;
          colIndex=n1.colIndex;
          placed=true;
        }
        break;
      }
      col.removeChild(node);
      var n=nextPosition(pageIndex,colIndex);
      pageIndex=n.pageIndex;
      colIndex=n.colIndex;
    }
  }

  normalizeOverflowFrom(0,0);
  var usedPages=Math.max(1,pageInners.length);
  for(var p=pageInners.length-1;p>=1;p--){
    var hasAny=false;
    var cols=pageColumns[p] || [];
    for(var c=0;c<cols.length;c++){
      if(cols[c] && cols[c].children && cols[c].children.length>0){
        hasAny=true;
        break;
      }
    }
    if(hasAny) break;
    usedPages=p;
  }
  trimPagesTo(usedPages);
  syncColumnSizesToDom();
}

function updatePaperGeometryStyles(){
  ensureMarginBounds();
  for(var p=0;p<pagePapers.length;p++){
    var paper=pagePapers[p];
    var inner=pageInners[p];
    if(!paper || !inner) continue;
    paper.style.width=toPx(PAPER_WIDTH);
    paper.style.height=toPx(PAPER_HEIGHT);
    inner.style.left=toPx(MARGIN_LEFT);
    inner.style.right=toPx(MARGIN_RIGHT);
    inner.style.top=toPx(MARGIN_TOP);
    inner.style.bottom=toPx(MARGIN_BOTTOM);
  }
  var guideLeft=document.getElementById('margin-guide-left');
  var guideRight=document.getElementById('margin-guide-right');
  var guideTop=document.getElementById('margin-guide-top');
  var guideBottom=document.getElementById('margin-guide-bottom');
  if(guideLeft){
    guideLeft.style.left=toPx(MARGIN_LEFT);
    guideLeft.style.top=toPx(MARGIN_TOP);
    guideLeft.style.bottom=toPx(MARGIN_BOTTOM);
  }
  if(guideRight){
    guideRight.style.right=toPx(MARGIN_RIGHT);
    guideRight.style.top=toPx(MARGIN_TOP);
    guideRight.style.bottom=toPx(MARGIN_BOTTOM);
  }
  if(guideTop){
    guideTop.style.top=toPx(MARGIN_TOP);
    guideTop.style.left=toPx(MARGIN_LEFT);
    guideTop.style.right=toPx(MARGIN_RIGHT);
  }
  if(guideBottom){
    guideBottom.style.bottom=toPx(MARGIN_BOTTOM);
    guideBottom.style.left=toPx(MARGIN_LEFT);
    guideBottom.style.right=toPx(MARGIN_RIGHT);
  }
}

function normalizeContentEditableDivs(col){
  if(!col) return;
  var sel=window.getSelection();
  var anchorN=sel&&sel.rangeCount>0?sel.getRangeAt(0).startContainer:null;
  var anchorO=sel&&sel.rangeCount>0?sel.getRangeAt(0).startOffset:0;
  var didChange=false;
  var children=col.childNodes;
  for(var i=children.length-1;i>=0;i--){
    var ch=children[i];
    if(ch.nodeType===1 && (ch.tagName||'').toLowerCase()==='div' && !ch.classList.contains('col-top-spacer')){
      var p=document.createElement('p');
      while(ch.firstChild) p.appendChild(ch.firstChild);
      col.replaceChild(p,ch);
      didChange=true;
    }
  }
  if(didChange&&anchorN&&sel){try{var rr=document.createRange();rr.setStart(anchorN,anchorO);rr.collapse(true);sel.removeAllRanges();sel.addRange(rr);}catch(_){}}
}

function debouncedColumnReflow(){
  if(reflowTimer) clearTimeout(reflowTimer);
  reflowTimer=setTimeout(function(){
    var cols=getAllColumnsOrdered();
    var needsReflow=false;
    for(var i=0;i<cols.length;i++){
      if(isColumnOverflow(cols[i])){ needsReflow=true; break; }
    }
    if(!needsReflow) return;
    var activeEl=document.activeElement;
    var bookmark=null;
    if(activeEl && activeEl.classList && activeEl.classList.contains('col-content')){
      bookmark=captureCaretBookmark(activeEl);
    }
    updateSourceMirrorFromColumns();
    rebuildColumnsFromSource();
    if(bookmark) restoreCaretBookmark(bookmark);
  }, 400);
}

function bindColumnInputEvents(col){
  try{ document.execCommand('defaultParagraphSeparator',false,'p'); }catch(_){}
  col.addEventListener('paste', function(ev){
    if(!ev) return;
    if(ev.preventDefault) ev.preventDefault();
    var text='';
    if(ev.clipboardData && ev.clipboardData.getData) text=ev.clipboardData.getData('text/plain')||'';
    else if(window.clipboardData && window.clipboardData.getData) text=window.clipboardData.getData('Text')||'';
    try{ document.execCommand('insertText', false, text); }catch(_){}
  });
  col.addEventListener('focus', function(){ setActiveColumn(col); });
  col.addEventListener('mousedown', function(){ setActiveColumn(col); });
  col.addEventListener('keydown', function(ev){
    if(!ev || (ev.key!=='Enter' && ev.keyCode!==13)) return;
    ev.preventDefault();
    if(ev.shiftKey){
      try{ document.execCommand('insertLineBreak',false,null); }catch(_){
        var sel=window.getSelection();
        if(sel && sel.rangeCount>0){
          var r=sel.getRangeAt(0); r.deleteContents();
          var br=document.createElement('br'); r.insertNode(br);
          r.setStartAfter(br); r.collapse(true); sel.removeAllRanges(); sel.addRange(r);
        }
      }
    }else{
      try{ document.execCommand('insertParagraph',false,null); }catch(_){}
    }
    normalizeContentEditableDivs(col);
    updateSourceMirrorFromColumns();
    debouncedColumnReflow();
    notifyContentChanged();
  });
  col.addEventListener('input', function(){
    normalizeContentEditableDivs(col);
    updateSourceMirrorFromColumns();
    debouncedColumnReflow();
    notifyContentChanged();
  });
}

function bindDragHandle(el, mode, index){
  if(!el) return;
  el.addEventListener('mousedown', function(ev){
    if(ev && ev.button!==0) return;
    if(ev && ev.stopPropagation) ev.stopPropagation();
    dragMode=mode;
    dragIndex=index;
    dragStartX=ev.clientX;
    dragStartY=ev.clientY;
    dragStartState={
      pageWidth:PAPER_WIDTH,
      pageHeight:PAPER_HEIGHT,
      marginLeft:MARGIN_LEFT,
      marginRight:MARGIN_RIGHT,
      marginTop:MARGIN_TOP,
      marginBottom:MARGIN_BOTTOM,
      columnWidths:columnWidths.slice(),
      columnHeights:columnHeights.slice(),
      columnTopOffsets:columnTopOffsets.slice()
    };
    if(ev.preventDefault) ev.preventDefault();
  });
}

function estimateCharsPerLine(colWidth){
  return Math.max(1, Math.floor(Math.max(20,colWidth-20)/Math.max(4,CHAR_WIDTH_PX)));
}

function refreshFooterFromCurrentPage(){
  var stats=window.previewStats;
  if(!stats || !stats.pages || stats.pages.length===0) return;
  var page=stats.pages[Math.max(0, Math.min(currentPageIndex, stats.pages.length-1))];
  if(!page) return;
  for(var i=0;i<COLUMN_COUNT;i++){
    var charsEl=document.getElementById('chars-'+i);
    var infoEl=document.getElementById('info-'+i);
    var col=(page.columns && i<page.columns.length) ? page.columns[i] : null;
    if(charsEl) charsEl.textContent=((page.charsPerColumn && i<page.charsPerColumn.length)?page.charsPerColumn[i]:0)+' 字/行';
    if(infoEl){
      var paraCount=col ? (col.paragraphCount||0) : 0;
      var totalChars=col ? (col.totalChars||0) : 0;
      infoEl.textContent=paraCount+' 段 · '+totalChars+' 字';
    }
  }
}

function rebuildPreviewStats(){
  var pages=[];
  var globalBlockTypes={};
  for(var p=0;p<pageColumns.length;p++){
    var row=pageColumns[p] || [];
    var charsPerColumn=[];
    var colParas=createEmptyColParas();
    var columns=[];
    var pageBlockTypes={};
    var blockCursor=0;
    for(var c=0;c<COLUMN_COUNT;c++){
      var col=(c<row.length)?row[c]:null;
      var paraIndices=[];
      var totalChars=0;
      var bt={};
      if(col){
        for(var i=0;i<col.children.length;i++){
          paraIndices.push(blockCursor++);
          var text=(col.children[i].innerText||col.children[i].textContent||'').replace(/\s+/g,'');
          totalChars += text.length;
          var key=normalizeBlockType(col.children[i] ? col.children[i].tagName : '');
          bt[key]=(bt[key]||0)+1;
          pageBlockTypes[key]=(pageBlockTypes[key]||0)+1;
          globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
        }
      }
      colParas[c]=paraIndices;
      var cpl=estimateCharsPerLine(columnWidths[c] || getDefaultColumnWidth());
      charsPerColumn.push(cpl);
      columns.push({
        index:c,
        charsPerLine:cpl,
        paragraphCount:paraIndices.length,
        totalChars:totalChars,
        totalDisplayUnits:totalChars,
        avgDisplayUnitsPerChar:paraIndices.length>0?1:0,
        blockTypes:bt
      });
    }
    pages.push({
      pageIndex:p,
      charsPerColumn:charsPerColumn,
      columnParagraphIndicesText:toColParasText(colParas),
      columnParagraphIndices:colParas,
      columns:columns,
      blockTypeCounts:pageBlockTypes
    });
  }
  if(pages.length===0){
    pages.push({
      pageIndex:0,
      charsPerColumn:[],
      columnParagraphIndicesText:'',
      columnParagraphIndices:createEmptyColParas(),
      columns:[],
      blockTypeCounts:{}
    });
  }
  currentPageIndex=Math.max(0, Math.min(currentPageIndex, pages.length-1));
  window.colChars=pages[0].charsPerColumn||[];
  window.colParas=pages[0].columnParagraphIndices||createEmptyColParas();
  window.previewStats={
    schemaVersion:3,
    currentPage:currentPageIndex+1,
    pageCount:pages.length,
    charsPerColumn:pages[0].charsPerColumn||[],
    columnParagraphIndicesText:pages[0].columnParagraphIndicesText||'',
    columnParagraphIndices:pages[0].columnParagraphIndices||createEmptyColParas(),
    columns:pages[0].columns||[],
    pages:pages,
    blockTypeCounts:globalBlockTypes
  };
  refreshFooterFromCurrentPage();
}

function inlineToMarkdown(node){
  if(!node) return '';
  if(node.nodeType===3) return (node.nodeValue||'').replace(/\r/g,'');
  if(node.nodeType!==1) return '';
  var tag=(node.tagName||'').toLowerCase();
  if(tag==='br') return '  \n';
  var inner='';
  for(var i=0;i<node.childNodes.length;i++) inner += inlineToMarkdown(node.childNodes[i]);
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

function tableToMarkdown(tableEl){
  if(!tableEl) return '';
  var rows=tableEl.querySelectorAll('tr');
  if(!rows || rows.length===0) return '';
  var data=[], maxCols=0;
  for(var r=0;r<rows.length;r++){
    var rowCells=rows[r].querySelectorAll('th,td');
    var line=[];
    for(var c=0;c<rowCells.length;c++){
      line.push(inlineToMarkdown(rowCells[c]).replace(/\n+/g,' ').trim());
    }
    if(line.length>maxCols) maxCols=line.length;
    data.push(line);
  }
  if(maxCols<=0) return '';
  for(var i=0;i<data.length;i++) while(data[i].length<maxCols) data[i].push('');
  var lines=['| '+data[0].join(' | ')+' |'];
  var sep=[]; for(var s=0;s<maxCols;s++) sep.push('---');
  lines.push('| '+sep.join(' | ')+' |');
  for(var rr=1;rr<data.length;rr++) lines.push('| '+data[rr].join(' | ')+' |');
  return lines.join('\n');
}

function blockToMarkdown(el){
  if(!el || el.nodeType!==1) return '';
  var tag=(el.tagName||'').toLowerCase();
  if(/^h[1-6]$/.test(tag)){
    var lv=parseInt(tag.charAt(1),10); if(!isFinite(lv)||lv<1) lv=1;
    return Array(lv+1).join('#')+' '+inlineToMarkdown(el).trim();
  }
  if(tag==='p'){ var pc=inlineToMarkdown(el).trim(); return pc||'\u2060'; }
  if(tag==='blockquote') return '> '+inlineToMarkdown(el).replace(/\n/g,'\n> ').trim();
  if(tag==='hr') return '---';
  if(tag==='pre') return '```\n'+((el.textContent||'').replace(/\n+$/,''))+'\n```';
  if(tag==='ul' || tag==='ol'){
    var lines=[];
    var li=el.querySelectorAll(':scope > li');
    for(var i=0;i<li.length;i++){
      var prefix=(tag==='ol')?((i+1)+'. '):'- ';
      lines.push(prefix+inlineToMarkdown(li[i]).trim());
    }
    return lines.join('\n');
  }
  if(tag==='table') return tableToMarkdown(el);
  return inlineToMarkdown(el).trim();
}

function extractMarkdown(){
  if(!sourceRoot) return '';
  var lines=[];
  var blocks=sourceRoot.children || [];
  for(var i=0;i<blocks.length;i++){
    var md=blockToMarkdown(blocks[i]);
    if(md && md.trim()) lines.push(md.trim());
  }
  return lines.join('\n\n');
}

function updateSourceMirrorFromColumns(){
  if(!sourceRoot) return;
  sourceRoot.innerHTML='';
  for(var p=0;p<pageColumns.length;p++){
    var row=pageColumns[p] || [];
    for(var c=0;c<row.length;c++){
      var col=row[c];
      if(!col) continue;
      var children=col.children || [];
      for(var i=0;i<children.length;i++){
        sourceRoot.appendChild(children[i].cloneNode(true));
      }
    }
  }
}

function notifyContentChanged(){
  if(notifyTimer) clearTimeout(notifyTimer);
  notifyTimer=setTimeout(function(){
    var markdown=extractMarkdown();
    var hash=simpleHash(markdown);
    if(hash===lastSentHash) return;
    lastSentHash=hash;
    contentVersion++;
    postHost({
      type:'contentChanged',
      markdown:markdown,
      version:contentVersion,
      hash:hash,
      blockCount:(sourceRoot && sourceRoot.children) ? sourceRoot.children.length : 0
    });
  }, 180);
}

function runCommand(cmd,value){
  try{
    if(!document.execCommand) return false;
    if(value===undefined) return document.execCommand(cmd,false,null);
    return document.execCommand(cmd,false,value);
  }catch(_){
    return false;
  }
}

function getActionTargetColumn(){
  var active=document.activeElement;
  if(active && active.classList && active.classList.contains('col-content')) return active;
  var cols=getAllColumnsOrdered();
  return cols.length>0 ? cols[0] : null;
}

function applyEditorAction(action){
  action=(action||'').trim();
  if(!action) return false;
  var col=getActionTargetColumn();
  if(!col) return false;
  setActiveColumn(col);
  try{ col.focus(); }catch(_){}
  var bookmark=captureCaretBookmark(col);
  var handled=true;
  switch(action){
    case 'Undo': handled=runCommand('undo'); break;
    case 'Redo': handled=runCommand('redo'); break;
    case 'Cut': handled=runCommand('cut'); break;
    case 'Copy': handled=runCommand('copy'); break;
    case 'Paste': handled=runCommand('paste'); break;
    case 'SelectAll': handled=runCommand('selectAll'); break;
    case 'FindReplace': handled=false; break;
    case 'Bold': handled=runCommand('bold'); break;
    case 'Italic': handled=runCommand('italic'); break;
    case 'Underline': handled=runCommand('underline'); break;
    case 'Strikethrough': handled=runCommand('strikeThrough'); break;
    case 'OrderedList': handled=runCommand('insertOrderedList'); break;
    case 'UnorderedList':
    case 'TaskList': handled=runCommand('insertUnorderedList'); break;
    case 'Quote': handled=runCommand('formatBlock','blockquote'); break;
    case 'Paragraph': handled=runCommand('formatBlock','p'); break;
    case 'H1': handled=runCommand('formatBlock','h1'); break;
    case 'H2': handled=runCommand('formatBlock','h2'); break;
    case 'H3': handled=runCommand('formatBlock','h3'); break;
    case 'H4': handled=runCommand('formatBlock','h4'); break;
    case 'InlineCode': handled=runCommand('insertText','`text`'); break;
    case 'CodeBlock': handled=runCommand('insertText','\n```\ncode\n```\n'); break;
    case 'MathBlock': handled=runCommand('insertText','\n$$\n\n$$\n'); break;
    case 'HorizontalRule': handled=runCommand('insertHorizontalRule'); break;
    case 'Table': handled=runCommand('insertText','\n| 列1 | 列2 |\n| --- | --- |\n| 内容 | 内容 |\n'); break;
    case 'Toc': handled=runCommand('insertText','\n[toc]\n'); break;
    case 'Footnote': handled=runCommand('insertText','[^1]\n\n[^1]: '); break;
    case 'Highlight': handled=runCommand('insertText','==高亮=='); break;
    case 'Superscript': handled=runCommand('superscript'); break;
    case 'Subscript': handled=runCommand('subscript'); break;
    case 'Comment': handled=runCommand('insertText','<!-- 注释 -->'); break;
    case 'InlineMath': handled=runCommand('insertText','$x$'); break;
    case 'Link': handled=runCommand('createLink','https://'); break;
    case 'ClearFormatting': handled=runCommand('removeFormat'); break;
    default: handled=false; break;
  }
  if(handled){
    updateSourceMirrorFromColumns();
    rebuildColumnsFromSource();
    restoreCaretBookmark(bookmark);
    notifyContentChanged();
  }
  return !!handled;
}

function rebuildColumnsFromSource(){
  var blocks=collectBlocksFromSource();
  distributeBlocksSequential(blocks);
  rebuildPreviewStats();
}

function rebuildColumnsFromCurrent(){
  updateSourceMirrorFromColumns();
  rebuildColumnsFromSource();
}

function setCurrentPage(index, notify){
  currentPageIndex=Math.max(0, Math.min(index, Math.max(0,pageWraps.length-1)));
  if(window.previewStats){
    window.previewStats.currentPage=currentPageIndex+1;
    window.previewStats.pageCount=Math.max(1,pageWraps.length);
  }
  refreshFooterFromCurrentPage();
  if(notify) postPageState();
}

function detectCurrentPageByScroll(){
  if(!viewportEl || pageWraps.length===0) return 0;
  var target=viewportEl.scrollTop + viewportEl.clientHeight*0.5;
  var nearest=0;
  var minDist=Number.MAX_VALUE;
  for(var i=0;i<pageWraps.length;i++){
    var mid=pageWraps[i].offsetTop + pageWraps[i].offsetHeight*0.5;
    var d=Math.abs(mid-target);
    if(d<minDist){
      minDist=d;
      nearest=i;
    }
  }
  return nearest;
}

function handleViewportScroll(){
  setCurrentPage(detectCurrentPageByScroll(), true);
}

function jumpToPage(pageNo){
  if(!viewportEl || pageWraps.length===0) return;
  var idx=Math.max(0, Math.min(pageWraps.length-1, (pageNo||1)-1));
  viewportEl.scrollTop=Math.max(0, pageWraps[idx].offsetTop-2);
  setCurrentPage(idx, true);
}

function scrollToHeading(text){
  var keyword=(text||'').trim();
  if(!keyword) return false;
  var cols=getAllColumnsOrdered();
  for(var i=0;i<cols.length;i++){
    var col=cols[i];
    if(!col) continue;
    var heads=col.querySelectorAll('h1,h2,h3,h4,h5,h6');
    for(var h=0;h<heads.length;h++){
      var t=(heads[h].textContent||'').trim();
      if(t.indexOf(keyword)<0) continue;
      var pageIndex=parseInt(col.getAttribute('data-page-index')||'0',10);
      if(!isFinite(pageIndex)) pageIndex=0;
      setCurrentPage(pageIndex, true);
      if(viewportEl && pageWraps.length>pageIndex){
        viewportEl.scrollTop=Math.max(0, pageWraps[pageIndex].offsetTop-2);
      }
      setActiveColumn(col);
      try{ col.focus(); }catch(_){}
      try{
        var range=document.createRange();
        var sel=window.getSelection();
        range.selectNodeContents(heads[h]);
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
      }catch(_){}
      heads[h].scrollIntoView({behavior:'smooth', block:'start'});
      return true;
    }
  }
  return false;
}

function applyGeometryRealtime(){
  updatePaperGeometryStyles();
  redistributeColumnWidthsEvenly();
  syncColumnSizesToDom();
  rebuildPreviewStats();
}

function applyGeometryFinal(){
  updatePaperGeometryStyles();
  redistributeColumnWidthsEvenly();
  rebuildColumnsFromCurrent();
}

function setPaperColumnLayout(columnCount, gapPx){
  var nextCount=Math.round(clampNumber(columnCount,1,10,COLUMN_COUNT));
  var nextGap=clampNumber(gapPx,6,260,COLUMN_GAP);
  var bookmark=captureCaretBookmark(getActionTargetColumn());
  COLUMN_GAP=nextGap;
  if(nextCount!==COLUMN_COUNT){
    COLUMN_COUNT=nextCount;
    resetColumnCaches();
    rebuildFooter();
  }
  rebuildColumnsFromCurrent();
  restoreCaretBookmark(bookmark);
  return true;
}

function setPaperGeometry(pageWidthMm, pageHeightMm){
  var w=Math.max(120, Number(pageWidthMm||0) * Math.max(0.01,PREVIEW_SCALE));
  var h=Math.max(120, Number(pageHeightMm||0) * Math.max(0.01,PREVIEW_SCALE));
  if(isFinite(w) && w>0) PAPER_WIDTH=w;
  if(isFinite(h) && h>0) PAPER_HEIGHT=h;
  applyGeometryFinal();
  return true;
}

function setPaperMargins(leftMm,rightMm,topMm,bottomMm){
  var scale=Math.max(0.01,PREVIEW_SCALE);
  MARGIN_LEFT=Math.max(0, Number(leftMm||0) * scale);
  MARGIN_RIGHT=Math.max(0, Number(rightMm||0) * scale);
  MARGIN_TOP=Math.max(0, Number(topMm||0) * scale);
  MARGIN_BOTTOM=Math.max(0, Number(bottomMm||0) * scale);
  applyGeometryFinal();
  return true;
}

function resetPaperLayout(){
  resetColumnCaches();
  updatePaperGeometryStyles();
  rebuildColumnsFromSource();
  updateSourceMirrorFromColumns();
  lastSentHash=simpleHash(extractMarkdown());
  postPageState();
  return true;
}

function getPreviewStats(){ return window.previewStats || null; }
function getColChars(){ return (window.previewStats&&window.previewStats.charsPerColumn?window.previewStats.charsPerColumn:[]).join(','); }
function getColParas(){ return window.previewStats&&window.previewStats.columnParagraphIndicesText?window.previewStats.columnParagraphIndicesText:''; }

function startMiddlePan(ev){
  if(!ev || ev.button!==1) return;
  if(!viewportEl) return;
  middlePanActive=true;
  middlePanViewport=viewportEl;
  middlePanStartX=ev.clientX;
  middlePanStartY=ev.clientY;
  middlePanStartLeft=viewportEl.scrollLeft;
  middlePanStartTop=viewportEl.scrollTop;
  viewportEl.style.cursor='grabbing';
  if(ev.preventDefault) ev.preventDefault();
}

function updateSource(bodyHtml,layoutObj,customWidthsArr){
  if(!sourceRoot) return false;
  var activeCol=document.activeElement;
  var bookmark=null;
  if(activeCol && activeCol.classList && activeCol.classList.contains('col-content')){
    bookmark=captureCaretBookmark(activeCol);
  }
  var scrollTop=viewportEl ? viewportEl.scrollTop : 0;
  var scrollLeft=viewportEl ? viewportEl.scrollLeft : 0;

  // 块级差量更新：只替换变化的块，保持未变化块的 DOM 引用
  var temp=document.createElement('div');
  temp.innerHTML=bodyHtml||'<p class=""empty"">(无内容)</p>';
  var oldBlocks=sourceRoot.children ? Array.prototype.slice.call(sourceRoot.children) : [];
  var newBlocks=temp.children ? Array.prototype.slice.call(temp.children) : [];
  var changed=false;
  var maxLen=Math.max(oldBlocks.length, newBlocks.length);
  // 从尾部删除多余旧块
  for(var d=oldBlocks.length-1; d>=newBlocks.length; d--){
    sourceRoot.removeChild(oldBlocks[d]);
    changed=true;
  }
  // 逐块比较，只替换变化的块
  var curOld=sourceRoot.children ? Array.prototype.slice.call(sourceRoot.children) : [];
  for(var i=0; i<newBlocks.length; i++){
    if(i<curOld.length){
      if(curOld[i].outerHTML!==newBlocks[i].outerHTML){
        sourceRoot.replaceChild(newBlocks[i], curOld[i]);
        changed=true;
      }
    }else{
      sourceRoot.appendChild(newBlocks[i]);
      changed=true;
    }
  }

  if(changed){
    rebuildColumnsFromSource();
  }
  updateSourceMirrorFromColumns();
  lastSentHash=simpleHash(extractMarkdown());
  if(viewportEl){ viewportEl.scrollTop=scrollTop; viewportEl.scrollLeft=scrollLeft; }
  if(bookmark) restoreCaretBookmark(bookmark);
  return true;
}

function updateSourceIncremental(bodyHtml,layoutObj,customWidthsArr,dirtyStart,dirtyEnd,incrementalMeta){
  updateSource(bodyHtml,layoutObj,customWidthsArr);
  return true;
}

window.addEventListener('wheel', function(ev){
  if(!ev || !ev.ctrlKey) return;
  postHost({ type:'wheelZoom', deltaY:ev.deltaY||0 });
  if(ev.preventDefault) ev.preventDefault();
}, { passive:false });

document.addEventListener('mousemove', function(ev){
  if(middlePanActive && middlePanViewport){
    var pdx=ev.clientX-middlePanStartX;
    var pdy=ev.clientY-middlePanStartY;
    middlePanViewport.scrollLeft=middlePanStartLeft-pdx;
    middlePanViewport.scrollTop=middlePanStartTop-pdy;
    if(ev.preventDefault) ev.preventDefault();
    return;
  }
  if(!dragMode || !dragStartState) return;
  if(dragMode==='col-gap'){
    var idx=dragIndex;
    if(idx<0 || idx>=COLUMN_COUNT-1) return;
    var dx=ev.clientX-dragStartX;
    var left=dragStartState.columnWidths[idx]+dx;
    var right=dragStartState.columnWidths[idx+1]-dx;
    if(left<80){ right -= (80-left); left=80; }
    if(right<80){ left -= (80-right); right=80; }
    if(left<80 || right<80) return;
    columnWidths[idx]=left;
    columnWidths[idx+1]=right;
    syncColumnSizesToDom();
    rebuildPreviewStats();
  }else if(dragMode==='col-top'){
    var ci=dragIndex;
    if(ci<0 || ci>=COLUMN_COUNT) return;
    var dy=ev.clientY-dragStartY;
    var origOff=dragStartState.columnTopOffsets[ci]||0;
    var origH=dragStartState.columnHeights[ci];
    var maxInner=getInnerHeight()-HANDLE_WIDTH*2-FRAME_BORDER_WIDTH*2;
    var newOff=Math.max(0, Math.min(maxInner-80, origOff+dy));
    var deltaOff=newOff-origOff;
    var newH=Math.max(80, Math.min(maxInner-newOff, origH-deltaOff));
    columnTopOffsets[ci]=newOff;
    columnHeights[ci]=newH;
    syncColumnSizesToDom();
    rebuildPreviewStats();
  }else if(dragMode==='col-bottom'){
    var ci=dragIndex;
    if(ci<0 || ci>=COLUMN_COUNT) return;
    var dy=ev.clientY-dragStartY;
    var h0=dragStartState.columnHeights[ci];
    var off=columnTopOffsets[ci]||0;
    var maxH=getInnerHeight()-HANDLE_WIDTH*2-FRAME_BORDER_WIDTH*2-off;
    columnHeights[ci]=Math.max(80,Math.min(maxH, h0+dy));
    syncColumnSizesToDom();
    rebuildPreviewStats();
  }else if(dragMode==='paper-right' || dragMode==='paper-bottom' || dragMode==='paper-corner'){
    var ddx=ev.clientX-dragStartX;
    var ddy=ev.clientY-dragStartY;
    if(dragMode==='paper-right'){
      PAPER_WIDTH=Math.max(180,dragStartState.pageWidth+ddx);
    }else if(dragMode==='paper-bottom'){
      PAPER_HEIGHT=Math.max(180,dragStartState.pageHeight+ddy);
    }else if(dragMode==='paper-corner'){
      if(ev && ev.ctrlKey){
        PAPER_WIDTH=Math.max(180,dragStartState.pageWidth+ddx);
        PAPER_HEIGHT=Math.max(180,dragStartState.pageHeight+ddy);
      }else{
        var startW=Math.max(180,dragStartState.pageWidth);
        var startH=Math.max(180,dragStartState.pageHeight);
        var ratio=startW/Math.max(1,startH);
        var candW=Math.max(180,startW+ddx);
        var candH=Math.max(180,startH+ddy);
        var nx=Math.abs(ddx)/Math.max(1,startW);
        var ny=Math.abs(ddy)/Math.max(1,startH);
        if(nx>=ny){
          PAPER_WIDTH=candW;
          PAPER_HEIGHT=Math.max(180,PAPER_WIDTH/ratio);
        }else{
          PAPER_HEIGHT=candH;
          PAPER_WIDTH=Math.max(180,PAPER_HEIGHT*ratio);
        }
      }
    }
    applyGeometryRealtime();
  }
});

document.addEventListener('mouseup', function(){
  if(middlePanActive){
    middlePanActive=false;
    if(middlePanViewport){
      middlePanViewport.style.cursor='';
      middlePanViewport=null;
    }
  }
  if(!dragMode) return;
  var resizedPaper=(dragMode==='paper-right' || dragMode==='paper-bottom' || dragMode==='paper-corner');
  dragMode='';
  dragIndex=-1;
  dragStartState=null;
  if(resizedPaper){
    updatePaperGeometryStyles();
    redistributeColumnWidthsEvenly();
  }
  rebuildColumnsFromCurrent();
  updateSourceMirrorFromColumns();
  if(resizedPaper){
    postPaperGeometry();
    postPaperScale();
  }
});

function bindStaticHandles(){
  bindDragHandle(document.getElementById('paper-resize-right'), 'paper-right', -1);
  bindDragHandle(document.getElementById('paper-resize-bottom'), 'paper-bottom', -1);
  bindDragHandle(document.getElementById('paper-resize-corner'), 'paper-corner', -1);
}

function init(){
  sourceRoot=document.getElementById('source');
  viewportEl=document.getElementById('viewport');
  pagesFlowEl=document.getElementById('pages-flow');
  mainPaperWrap=document.getElementById('paper-wrap');
  mainPaperEl=document.getElementById('paper');
  mainInnerEl=document.getElementById('paper-inner');
  footerEl=document.getElementById('paper-footer');
  if(!sourceRoot || !viewportEl || !pagesFlowEl || !mainPaperWrap || !mainPaperEl || !mainInnerEl || !footerEl) return;
  bindStaticHandles();
  rebuildFooter();
  resetToSinglePage();
  updatePaperGeometryStyles();
  rebuildColumnsFromSource();
  updateSourceMirrorFromColumns();
  lastSentHash=simpleHash(extractMarkdown());
  currentPageIndex=0;
  postPageState();
  postPaperScale();
  viewportEl.addEventListener('mousedown', startMiddlePan, { passive:false });
  viewportEl.addEventListener('auxclick', function(ev){
    if(ev && ev.button===1 && ev.preventDefault){
      ev.preventDefault();
    }
  }, { passive:false });
  viewportEl.addEventListener('scroll', handleViewportScroll);
}

if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,40);}
else{window.onload=init;}
";

            string flowCss = flowCssTemplate
                .Replace("/*FLOW_FONT_FAMILY*/", fontFamily)
                .Replace("/*FLOW_BASE_FONT_SIZE*/", $"{basePx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_LINE_HEIGHT*/", Math.Max(1.0, cfg.LineSpacingFactor).ToString("0.###", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_TEXT_X_SCALE_CSS*/", textXScale.ToString("0.###", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAPER_WIDTH*/", $"{paperWidthPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_PAPER_HEIGHT*/", $"{paperHeightPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_PAD_LEFT*/", $"{leftPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_PAD_RIGHT*/", $"{rightPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_PAD_TOP*/", $"{topPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_PAD_BOTTOM*/", $"{bottomPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_GAP_PX*/", $"{gapPx.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_BORDER_WIDTH*/", $"{borderWidth.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_HANDLE_WIDTH*/", $"{handleWidth.ToString("0.###", CultureInfo.InvariantCulture)}px")
                .Replace("/*FLOW_HANDLE_ACTIVE_WIDTH*/", $"{handleActiveWidth.ToString("0.###", CultureInfo.InvariantCulture)}px");

            string flowJs = flowJsTemplate
                .Replace("/*FLOW_PREVIEW_SCALE*/", scale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_CHAR_WIDTH_PX*/", charWidthPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_COLUMN_COUNT*/", cols.ToString(CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAPER_WIDTH_NUM*/", paperWidthPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAPER_HEIGHT_NUM*/", paperHeightPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAD_LEFT_NUM*/", leftPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAD_RIGHT_NUM*/", rightPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAD_TOP_NUM*/", topPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_PAD_BOTTOM_NUM*/", bottomPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_GAP_NUM*/", gapPx.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_HANDLE_WIDTH_NUM*/", handleWidth.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*FLOW_BORDER_WIDTH_NUM*/", borderWidth.ToString("0.#####", CultureInfo.InvariantCulture));

            return "<!DOCTYPE html>\n<html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />"
                + "<style>" + flowCss + "</style>"
                + "</head><body>"
                + "<div id=\"source\" style=\"display:none;\">" + body + "</div>"
                + "<div class=\"viewport\" id=\"viewport\"><div class=\"pages-flow\" id=\"pages-flow\"><div class=\"paper-wrap\" id=\"paper-wrap\">"
                + "<div class=\"paper\" id=\"paper\">"
                + "<div class=\"margin-guide left\" id=\"margin-guide-left\"></div>"
                + "<div class=\"margin-guide right\" id=\"margin-guide-right\"></div>"
                + "<div class=\"margin-guide top\" id=\"margin-guide-top\"></div>"
                + "<div class=\"margin-guide bottom\" id=\"margin-guide-bottom\"></div>"
                + "<div class=\"paper-inner\" id=\"paper-inner\"></div>"
                + "<div class=\"paper-resize-right\" id=\"paper-resize-right\"></div>"
                + "<div class=\"paper-resize-bottom\" id=\"paper-resize-bottom\"></div>"
                + "<div class=\"paper-resize-corner\" id=\"paper-resize-corner\"></div>"
                + "</div></div></div></div>"
                + footerHtml
                + "<script>" + flowJs + "</script>"
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

        internal static string BuildCustomColumnWidthsJson(LayoutResult layoutResult, double previewScale)
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
:root{--paper-column-gap:/*GUTTER*/;}
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
.col-content{-ms-flex:none;flex:none;overflow:hidden;padding:8px 10px;background:#ffffff;color:#111111;caret-color:#111}
.col-content.last{overflow:hidden}
.col-content[contenteditable='true']{outline:none}
.col-content[contenteditable='true']:focus{box-shadow:inset 0 0 0 1px #58a6ff}
.col-vhandle{height:6px;background:#2b3138;cursor:ns-resize;-ms-flex-negative:0;flex-shrink:0}
.col-vhandle:hover{background:#6e7681}
.col-tables{flex-shrink:0;padding:4px 10px;background:#f8f9fb;border-top:1px dashed #adb5bd;overflow-x:auto;overflow-y:hidden}
.col-tables:empty{display:none}
.col-gap{
  width:var(--paper-column-gap);min-width:6px;background:transparent;
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
  width:var(--paper-column-gap);min-width:6px;background:transparent;
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
var contentVersion=0;
var lastSentMarkdownHash='';
var lastKnownMarkdown='';
var localEditSequence=0;
var lastNotifiedEditSequence=0;
var lastInputTimestamp=0;
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

function clampNumber(value, min, max, fallback){
  var n=Number(value);
  if(!isFinite(n)) return fallback;
  if(n<min) return min;
  if(n>max) return max;
  return n;
}

function rebuildFooter(columnCount){
  var footer=document.getElementById('paper-footer');
  if(!footer) return;
  var html='';
  for(var i=0;i<columnCount;i++){
    html += '<span class=""col-footer-item"">第 '+(i+1)+' 栏 <span class=""col-chars"" id=""chars-'+i+'""></span> · <span id=""info-'+i+'""></span></span>';
    if(i<columnCount-1){
      html += '<span class=""col-footer-sep""></span>';
    }
  }
  footer.innerHTML=html;
}

function applyPaperColumnGapStyle(gapPx){
  var gap=clampNumber(gapPx, 6, 240, 24);
  document.documentElement.style.setProperty('--paper-column-gap', gap+'px');
  var gaps=document.querySelectorAll('.col-gap');
  for(var i=0;i<gaps.length;i++){
    gaps[i].style.width=gap+'px';
    gaps[i].style.minWidth='6px';
  }
  var seps=document.querySelectorAll('.col-footer-sep');
  for(var s=0;s<seps.length;s++){
    seps[s].style.width=gap+'px';
    seps[s].style.minWidth='6px';
  }

  var wraps=document.querySelectorAll('.page-wrap');
  if(wraps && wraps.length>0){
    var idx=Math.max(0, Math.min(currentPageIndex, wraps.length-1));
    syncFooterWidths({ wrap: wraps[idx] });
  }
}

function setPaperColumnLayout(columnCount, gapPx){
  try{
    var nextCount=Math.round(clampNumber(columnCount, 1, 10, COLUMN_COUNT || 1));
    var nextGap=clampNumber(gapPx, 6, 240, 24);
    applyPaperColumnGapStyle(nextGap);

    if(nextCount===COLUMN_COUNT){
      return true;
    }

    COLUMN_COUNT=nextCount;
    customColumnWidths=[];
    customColumnHeights={};
    rebuildFooter(COLUMN_COUNT);
    distribute();
    return true;
  }catch(_){
    return false;
  }
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

function simpleHash(text){
  text=text||'';
  var hash=0;
  for(var i=0;i<text.length;i++){
    hash=((hash<<5)-hash)+text.charCodeAt(i);
    hash|=0;
  }
  return String(hash);
}

function countDomBlocks(){
  var wraps=document.querySelectorAll('.page-wrap');
  var total=0;
  for(var p=0;p<wraps.length;p++){
    var cols=wraps[p].querySelectorAll('.col-content');
    for(var c=0;c<cols.length;c++){
      total+=cols[c].children ? cols[c].children.length : 0;
    }
  }
  return total;
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
    clone.setAttribute('data-block-index', String(blockIndex));
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

function getNodePath(root,node){
  var path=[];
  var cur=node;
  while(cur && cur!==root){
    var parent=cur.parentNode;
    if(!parent) return [];
    var idx=0;
    var child=parent.firstChild;
    while(child && child!==cur){
      idx++;
      child=child.nextSibling;
    }
    path.unshift(idx);
    cur=parent;
  }
  return cur===root ? path : [];
}

function resolveNodeByPath(root,path){
  if(!root || !path) return null;
  var cur=root;
  for(var i=0;i<path.length;i++){
    if(!cur || !cur.childNodes || path[i]<0 || path[i]>=cur.childNodes.length) return null;
    cur=cur.childNodes[path[i]];
  }
  return cur;
}

function getBlockElementFromNode(col,node){
  var cur=node;
  if(cur && cur.nodeType===3) cur=cur.parentNode;
  while(cur && cur!==col){
    if(cur.parentNode===col) return cur;
    cur=cur.parentNode;
  }
  return null;
}

function getColumnByPosition(pageIndex,colIndex){
  if(!isFinite(pageIndex) || !isFinite(colIndex)) return null;
  return getColumn(pageIndex,colIndex);
}

function findBlockAcrossColumns(blockIndex,startPage,startCol){
  if(!isFinite(blockIndex) || blockIndex<0) return null;
  var totalPages=document.querySelectorAll('.page-wrap').length;
  if(totalPages<=0) totalPages=1;
  var page=Math.max(0, startPage||0);
  var col=Math.max(0, startCol||0);
  var safe=0;
  while(safe<totalPages*COLUMN_COUNT+8){
    safe++;
    var curCol=getColumnByPosition(page,col);
    if(curCol){
      var selector='[data-block-index='+String(blockIndex)+']';
      var node=curCol.querySelector(selector);
      if(node){
        return { col:curCol, node:node };
      }
    }
    var next=getNextPosition(page,col);
    page=next.pageIndex;
    col=next.colIndex;
    if(page>=totalPages) break;
  }

  var globalNode=document.querySelector('[data-block-index='+String(blockIndex)+']');
  if(globalNode){
    var globalCol=globalNode.parentNode;
    while(globalCol && !(globalCol.classList && globalCol.classList.contains('col-content'))){
      globalCol=globalCol.parentNode;
    }
    if(globalCol){
      return { col:globalCol, node:globalNode };
    }
  }
  return null;
}

function setCaretToNode(sel,node,offset){
  if(!sel || !node) return false;
  try{
    var range=document.createRange();
    if(node.nodeType===3){
      range.setStart(node, Math.min(Math.max(0,offset||0), node.nodeValue ? node.nodeValue.length : 0));
    }else{
      var childCount=node.childNodes ? node.childNodes.length : 0;
      range.setStart(node, Math.min(Math.max(0,offset||0), childCount));
    }
    range.collapse(true);
    sel.removeAllRanges();
    sel.addRange(range);
    return true;
  }catch(_){
    return false;
  }
}

function setCaretToBlockEnd(sel,blockNode){
  if(!sel || !blockNode) return false;
  var walker=document.createTreeWalker(blockNode, NodeFilter.SHOW_TEXT, null, false);
  var lastText=null;
  while(walker.nextNode()){
    lastText=walker.currentNode;
  }
  if(lastText){
    return setCaretToNode(sel,lastText,lastText.nodeValue ? lastText.nodeValue.length : 0);
  }
  return setCaretToNode(sel,blockNode,blockNode.childNodes ? blockNode.childNodes.length : 0);
}

function captureCaretBookmark(col){
  if(!col || document.activeElement!==col) return null;
  var sel=window.getSelection ? window.getSelection() : null;
  if(!sel || sel.rangeCount<=0) return null;
  var range=sel.getRangeAt(0);
  var node=range.startContainer;
  var offset=range.startOffset;
  var path=getNodePath(col,node);
  var block=getBlockElementFromNode(col,node);
  var blockIndex=-1;
  if(block && block.getAttribute){
    var raw=block.getAttribute('data-block-index');
    var parsed=parseInt(raw||'',10);
    if(isFinite(parsed) && parsed>=0) blockIndex=parsed;
  }
  var pageIndex=parseInt(col.getAttribute('data-page-index')||'0',10);
  var colIndex=parseInt(col.getAttribute('data-col-index')||'0',10);
  if(!isFinite(pageIndex)) pageIndex=0;
  if(!isFinite(colIndex)) colIndex=0;
  if(!path || path.length===0){
    return {
      fallback:true,
      blockIndex:blockIndex,
      pageIndex:pageIndex,
      colIndex:colIndex,
      fallbackChildIndex:col.children.length>0 ? Math.max(0,col.children.length-1) : 0
    };
  }
  return {
    path:path,
    offset:offset,
    blockIndex:blockIndex,
    pageIndex:pageIndex,
    colIndex:colIndex
  };
}

function restoreCaretBookmark(col,bookmark){
  if(!col || !bookmark) return;
  var sel=window.getSelection ? window.getSelection() : null;
  if(!sel) return;

  var targetCol=col;
  var node=null;
  var offset=0;
  if(bookmark.path && bookmark.path.length){
    node=resolveNodeByPath(col,bookmark.path);
    if(node && !col.contains(node)){
      node=null;
    }
    offset=bookmark.offset||0;
  }

  if(!node && isFinite(bookmark.blockIndex) && bookmark.blockIndex>=0){
    var located=findBlockAcrossColumns(
      bookmark.blockIndex,
      isFinite(bookmark.pageIndex) ? bookmark.pageIndex : parseInt(col.getAttribute('data-page-index')||'0',10),
      isFinite(bookmark.colIndex) ? bookmark.colIndex : parseInt(col.getAttribute('data-col-index')||'0',10));
    if(located && located.node){
      targetCol=located.col||targetCol;
      node=located.node;
      offset=located.node.childNodes ? located.node.childNodes.length : 0;
    }
  }

  if(!node){
    var blockIdx=(bookmark.fallbackChildIndex||0);
    if(targetCol.children && targetCol.children.length>0){
      if(blockIdx>=targetCol.children.length) blockIdx=targetCol.children.length-1;
      node=targetCol.children[blockIdx];
      offset=(node && node.childNodes && node.childNodes.length>0) ? node.childNodes.length : 0;
    }else{
      node=targetCol;
      offset=0;
    }
  }

  var restored=setCaretToNode(sel,node,offset);
  if(!restored && node){
    restored=setCaretToBlockEnd(sel,node.nodeType===1 ? node : (node.parentNode||targetCol));
  }
  if(targetCol && targetCol.focus){
    try{ targetCol.focus(); }catch(_){}
  }
}

function canFitBlock(col,block){
  if(!col || !block) return false;
  col.appendChild(block);
  var fits=!isColumnOverflow(col);
  col.removeChild(block);
  return fits;
}

function isTextSplittableBlock(block){
  if(!block || block.nodeType!==1) return false;
  var tag=(block.tagName||'').toLowerCase();
  if(tag!=='p' && tag!=='blockquote') return false;
  // 仅对纯文本段落做切分，避免破坏复杂内联结构。
  if(block.children && block.children.length>0) return false;
  var text=(block.innerText||block.textContent||'').replace(/\r/g,'');
  return text.length>1;
}

function findSafeSplitIndex(text, idx){
  if(!text) return idx;
  var safe=Math.max(1, Math.min(idx, text.length-1));
  var left=safe;
  // 优先在空白处分割，避免截断单词/中文词组过于生硬。
  while(left>1){
    var ch=text.charAt(left);
    if(/\s/.test(ch)) return left;
    left--;
    if(safe-left>24) break;
  }
  return safe;
}

function splitTextBlockToNextColumn(col, block, nextCol){
  if(!col || !block || !nextCol) return false;
  if(!isTextSplittableBlock(block)) return false;

  var original=(block.innerText||block.textContent||'').replace(/\r/g,'');
  if(original.length<2) return false;

  var low=1, high=original.length-1, best=-1;
  while(low<=high){
    var mid=(low+high)>>1;
    var probeIdx=findSafeSplitIndex(original, mid);
    block.textContent=original.slice(0, probeIdx);
    if(!isColumnOverflow(col)){
      best=probeIdx;
      low=mid+1;
    }else{
      high=mid-1;
    }
  }

  if(best<=0 || best>=original.length){
    block.textContent=original;
    return false;
  }

  var head=original.slice(0, best).replace(/\s+$/,'');
  var tail=original.slice(best).replace(/^\s+/,'');
  if(!head || !tail){
    block.textContent=original;
    return false;
  }

  block.textContent=head;
  // 极端情况下清理空白后仍溢出，回滚。
  if(isColumnOverflow(col)){
    block.textContent=original;
    return false;
  }

  var tailNode=block.cloneNode(false);
  tailNode.textContent=tail;
  if(block.getAttribute){
    var blockIndex=block.getAttribute('data-block-index');
    if(blockIndex!==null && blockIndex!==undefined){
      tailNode.setAttribute('data-block-index', blockIndex);
    }
  }
  nextCol.insertBefore(tailNode, nextCol.firstChild);
  return true;
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
    var nextPos=getNextPosition(pageIndex,colIndex);
    var nextCol=ensureColumn(nextPos.pageIndex,nextPos.colIndex);
    if(!nextCol) break;

    var moveNode=col.lastElementChild;
    if(!moveNode) break;
    if(lockedBlock && moveNode===lockedBlock){
      // 编辑块引发溢出时，优先尝试“切分当前段落”到下一栏。
      if(splitTextBlockToNextColumn(col, moveNode, nextCol)){
        continue;
      }
      // 切分失败再整块前推，避免 hidden 裁切造成“消失”。
      moveNode.setAttribute('data-flow-locked-move','1');
    }
    if(!moveNode) break;
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
    if(candidate.getAttribute && candidate.getAttribute('data-flow-locked-move')==='1'){
      break;
    }
    if(!canFitBlock(col,candidate)) break;
    col.appendChild(candidate);
  }
}

function clearFlowLockedMoveFlags(){
  var moved=document.querySelectorAll('[data-flow-locked-move=1]');
  for(var i=0;i<moved.length;i++){
    moved[i].removeAttribute('data-flow-locked-move');
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

function cloneMap(src){
  var map={}, k='';
  src=src||{};
  for(k in src){
    if(Object.prototype.hasOwnProperty.call(src,k)){
      map[k]=src[k];
    }
  }
  return map;
}

function addMap(target, src, sign){
  target=target||{};
  src=src||{};
  var k='', next=0;
  for(k in src){
    if(!Object.prototype.hasOwnProperty.call(src,k)) continue;
    next=(target[k]||0)+(sign*src[k]);
    if(next<=0) delete target[k];
    else target[k]=next;
  }
  return target;
}

function getColumnParagraphIndex(node, fallback){
  if(!node) return fallback;
  var raw=node.getAttribute ? node.getAttribute('data-block-index') : null;
  var idx=parseInt(raw||'',10);
  if(isFinite(idx) && idx>=0) return idx;
  return fallback;
}

function buildPageStatsFromDom(pageIndex){
  var wrap=document.querySelector('[data-page-index=""'+pageIndex+'""]');
  if(!wrap){
    return {
      pageIndex:pageIndex,
      charsPerColumn:new Array(COLUMN_COUNT).fill(0),
      columnParagraphIndicesText:toColParasText(createEmptyColParas()),
      columnParagraphIndices:createEmptyColParas(),
      columns:(function(){var arr=[];for(var i=0;i<COLUMN_COUNT;i++) arr.push(createEmptyColumnStats(i));return arr;})(),
      blockTypeCounts:{}
    };
  }

  var cols=wrap.querySelectorAll('.col-content');
  if(cols.length>0){
    cols[cols.length-1].className='col-content last';
  }

  var pageBlockTypes={};
  var colStats=[];
  var colChars=[];
  var colParas=createEmptyColParas();

  for(var c=0;c<COLUMN_COUNT;c++){
    var col=(c<cols.length)?cols[c]:null;
    if(!col){
      colChars.push(0);
      colStats.push(createEmptyColumnStats(c));
      continue;
    }

    var paraIndices=[];
    var bt={};
    for(var b=0;b<col.children.length;b++){
      var node=col.children[b];
      paraIndices.push(getColumnParagraphIndex(node, paraIndices.length));
      var key=normalizeBlockType(node ? node.tagName : '');
      bt[key]=(bt[key]||0)+1;
      pageBlockTypes[key]=(pageBlockTypes[key]||0)+1;
    }
    colParas[c]=paraIndices;

    var rawText=(col.innerText||col.textContent||'');
    var metrics=calcTextMetrics(rawText);
    var cpl=computeCharsPerLine(col,metrics);
    colChars.push(cpl);
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

  return {
    pageIndex:pageIndex,
    charsPerColumn:colChars,
    columnParagraphIndicesText:toColParasText(colParas),
    columnParagraphIndices:colParas,
    columns:colStats,
    blockTypeCounts:pageBlockTypes
  };
}

function updateStatsForDirtyRanges(pageIndices){
  if(!pageIndices || pageIndices.length===0){
    rebuildPreviewStatsFromDom();
    return;
  }

  var wraps=document.querySelectorAll('.page-wrap');
  var pageCount=Math.max(1, wraps.length);
  if(!window.previewStats || !window.previewStats.pages){
    rebuildPreviewStatsFromDom();
    return;
  }

  var stats=window.previewStats;
  var pages=(stats.pages||[]).slice(0);
  var global=cloneMap(stats.blockTypeCounts||{});

  // 页数收缩时，先扣减被移除页贡献。
  if(pages.length>pageCount){
    for(var cut=pageCount;cut<pages.length;cut++){
      addMap(global, pages[cut] ? pages[cut].blockTypeCounts : {}, -1);
    }
    pages.length=pageCount;
  }

  var map={}, normalized=[];
  for(var i=0;i<pageIndices.length;i++){
    var p=parseInt(pageIndices[i],10);
    if(!isFinite(p) || p<0 || p>=pageCount || map[p]) continue;
    map[p]=true;
    normalized.push(p);
  }
  if(normalized.length===0){
    rebuildPreviewStatsFromDom();
    return;
  }

  for(var j=0;j<normalized.length;j++){
    var pageIndex=normalized[j];
    var oldPage=pages[pageIndex];
    var nextPage=buildPageStatsFromDom(pageIndex);
    addMap(global, oldPage ? oldPage.blockTypeCounts : {}, -1);
    addMap(global, nextPage.blockTypeCounts, +1);
    pages[pageIndex]=nextPage;
  }

  if(pages.length===0){
    rebuildPreviewStatsFromDom();
    return;
  }

  var firstPage=pages[0];
  window.colChars=firstPage.charsPerColumn||[];
  window.colParas=firstPage.columnParagraphIndices||createEmptyColParas();
  window.previewStats={
    schemaVersion:3,
    currentPage:Math.min(currentPageIndex+1, pageCount),
    pageCount:pageCount,
    charsPerColumn:firstPage.charsPerColumn||[],
    columnParagraphIndicesText:firstPage.columnParagraphIndicesText||'',
    columnParagraphIndices:firstPage.columnParagraphIndices||createEmptyColParas(),
    columns:firstPage.columns||[],
    pages:pages,
    blockTypeCounts:global
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
  clearFlowLockedMoveFlags();
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
  if(tag==='br') return '  \n';
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
  if(tag==='p'){ var pc2=inlineToMarkdown(el).trim(); return pc2||'\u2060'; }
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

function extractMarkdownFromContainerChildren(container){
  if(!container || !container.children) return '';
  var lines=[];
  for(var i=0;i<container.children.length;i++){
    var md=blockToMarkdown(container.children[i]);
    if(md && md.trim()){
      lines.push(md.trim());
    }
  }
  return lines.join('\n\n');
}

function extractMarkdownFromPagesDom(){
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

function extractMarkdownFromSourceDom(){
  var src=document.getElementById('source');
  if(!src || !src.children || src.children.length===0) return '';
  return extractMarkdownFromContainerChildren(src);
}

function extractMarkdownFromBodyHtml(bodyHtml){
  var temp=document.createElement('div');
  temp.innerHTML=bodyHtml||'';
  return extractMarkdownFromContainerChildren(temp);
}

function extractAllMarkdown(){
  var fromPages=extractMarkdownFromPagesDom();
  if(fromPages && fromPages.trim()){
    return fromPages;
  }
  return extractMarkdownFromSourceDom();
}

function resolveTargetColumns(targetColumns){
  if(!targetColumns || targetColumns.length===0){
    return document.querySelectorAll('.col-content');
  }
  return targetColumns;
}

function separateTablesFromColumns(targetColumns){
  var colContents=resolveTargetColumns(targetColumns);
  for(var i=0;i<colContents.length;i++){
    var col=colContents[i];
    if(!col) continue;
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

function normalizeTableLayout(targetColumns){
  var areas=[];
  if(targetColumns && targetColumns.length){
    for(var i0=0;i0<targetColumns.length;i0++){
      var col0=targetColumns[i0];
      if(!col0 || !col0.parentNode) continue;
      var area0=col0.parentNode.querySelector('.col-tables');
      if(area0) areas.push(area0);
    }
  }else{
    areas=document.querySelectorAll('.col-tables');
  }
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

function renderMathInColumns(targetColumns){
  if(typeof renderMathInElement!=='function') return;
  var cols=resolveTargetColumns(targetColumns);
  for(var i=0;i<cols.length;i++){
    if(!cols[i]) continue;
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
    var blockCount=countDomBlocks();
    if(blockCount>0 && (!markdown || !markdown.trim())){
      var sourceFallback=extractMarkdownFromSourceDom();
      if(sourceFallback && sourceFallback.trim()){
        markdown=sourceFallback;
      }else{
        markdown=lastKnownMarkdown||'';
      }
    }
    if(markdown && markdown.trim()){
      lastKnownMarkdown=markdown;
    }
    var hash=simpleHash(markdown);
    if(hash===lastSentMarkdownHash){
      return;
    }
    lastSentMarkdownHash=hash;
    contentVersion++;
    window.chrome.webview.postMessage({
      type:'contentChanged',
      markdown:markdown,
      version:contentVersion,
      hash:hash,
      blockCount:blockCount
    });
    lastNotifiedEditSequence=localEditSequence;
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
    localEditSequence++;
    lastInputTimestamp=Date.now();
    var p=parseInt(col.getAttribute('data-page-index')||pageIndex,10);
    var c=parseInt(col.getAttribute('data-col-index')||colIndex,10);
    if(!isFinite(p)) p=0;
    if(!isFinite(c)) c=0;
    var caretBookmark=captureCaretBookmark(col);
    var locked=getEditingBlockInColumn(col);
    cascadeReflow(p,c,locked);
    restoreCaretBookmark(col, caretBookmark);
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

function getLayoutAffectedPages(incrementalMeta, pageCount){
  if(!incrementalMeta) return [];
  var raw=incrementalMeta.affectedPageIndices||incrementalMeta.AffectedPageIndices||[];
  if(!raw || raw.length===0) return [];
  var map={}, result=[], i=0, v=0;
  for(i=0;i<raw.length;i++){
    v=parseInt(raw[i],10);
    if(!isFinite(v) || v<0) continue;
    if(pageCount>0 && v>=pageCount) continue;
    if(map[v]) continue;
    map[v]=true;
    result.push(v);
  }
  result.sort(function(a,b){ return a-b; });
  return result;
}

function ensurePageCount(pageCount){
  if(!isFinite(pageCount) || pageCount<0) pageCount=0;
  for(var i=0;i<pageCount;i++){
    ensurePage(i);
  }
}

function trimPagesToCount(pageCount){
  var wraps=document.querySelectorAll('.page-wrap');
  for(var i=wraps.length-1;i>=0;i--){
    var idx=parseInt(wraps[i].getAttribute('data-page-index')||'-1',10);
    if(!isFinite(idx) || idx<0) continue;
    if(idx>=pageCount && wraps[i].parentNode){
      wraps[i].parentNode.removeChild(wraps[i]);
    }
  }
}

function collectColumnsFromPages(pageIndices){
  var cols=[], i=0, c=0;
  for(i=0;i<pageIndices.length;i++){
    var pageIndex=pageIndices[i];
    for(c=0;c<COLUMN_COUNT;c++){
      var col=getColumn(pageIndex,c);
      if(col) cols.push(col);
    }
  }
  return cols;
}

function patchPageFromLayout(pageIndex, pageDef, sourceElements){
  for(var c=0;c<COLUMN_COUNT;c++){
    var col=getColumn(pageIndex,c);
    if(!col) continue;
    col.innerHTML='';
    var wrap=col.parentNode;
    if(wrap){
      var tablesArea=wrap.querySelector('.col-tables');
      if(tablesArea) tablesArea.innerHTML='';
    }

    var indices=getLayoutColIndices(pageDef,c);
    for(var i=0;i<indices.length;i++){
      var idx=indices[i];
      if(idx<0 || idx>=sourceElements.length) continue;
      var clone=sourceElements[idx].cloneNode(true);
      clone.setAttribute('data-block-index', String(idx));
      col.appendChild(clone);
    }
  }
}

function patchSourceFromDirtyRange(src, bodyHtml, dirtyStart, dirtyEnd){
  if(!src) return false;
  var next=document.createElement('div');
  next.innerHTML=bodyHtml||'';
  var newLen=next.children ? next.children.length : 0;
  if(newLen===0){
    src.innerHTML='';
    return true;
  }

  var start=parseInt(dirtyStart,10);
  if(!isFinite(start) || start<0) start=0;
  if(start>newLen) start=newLen;

  // 统一从脏区起替换到末尾，兼容插入/删除导致的索引整体后移。
  while(src.children.length>start){
    src.removeChild(src.lastChild);
  }
  for(var i=start;i<newLen;i++){
    src.appendChild(next.children[i].cloneNode(true));
  }
  return true;
}

function applyLayoutPatch(layoutObj, customWidthsArr, incrementalMeta){
  if(layoutObj!==undefined) LAYOUT_RESULT=layoutObj;
  if(customWidthsArr!==undefined) customColumnWidths=customWidthsArr||[];
  var pagesDef=getLayoutPages(LAYOUT_RESULT);
  if(!pagesDef || pagesDef.length===0) return false;
  var affectedPages=getLayoutAffectedPages(incrementalMeta, pagesDef.length);
  if(!affectedPages || affectedPages.length===0) return false;

  ensurePageCount(pagesDef.length);
  var src=document.getElementById('source');
  if(!src) return false;
  var sourceElements=src.children;

  for(var i=0;i<affectedPages.length;i++){
    var p=affectedPages[i];
    if(p<0 || p>=pagesDef.length) continue;
    patchPageFromLayout(p, pagesDef[p], sourceElements);
  }

  trimPagesToCount(pagesDef.length);
  var dirtyColumns=collectColumnsFromPages(affectedPages);
  separateTablesFromColumns(dirtyColumns);
  renderMathInColumns(dirtyColumns);
  normalizeTableLayout(dirtyColumns);
  updateStatsForDirtyRanges(affectedPages);
  var targetPage=Math.min(currentPageIndex, Math.max(0, pagesDef.length-1));
  setCurrentPage(targetPage, true);
  return true;
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
        clone.setAttribute('data-block-index', String(idx));
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
    clone.setAttribute('data-block-index', String(e));
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

function getActionTargetColumn(){
  var active=document.activeElement;
  if(active && active.classList && active.classList.contains('col-content')){
    return active;
  }
  var col=getColumn(currentPageIndex,0);
  if(col) return col;
  var any=document.querySelector('.col-content');
  return any || null;
}

function applyEditorAction(action){
  action=(action||'').trim();
  if(!action) return false;
  var col=getActionTargetColumn();
  if(!col) return false;
  try{ col.focus(); }catch(_){}

  var handled=true;
  try{
    switch(action){
      case 'Undo': handled=document.execCommand ? document.execCommand('undo') : false; break;
      case 'Redo': handled=document.execCommand ? document.execCommand('redo') : false; break;
      case 'Cut': handled=document.execCommand ? document.execCommand('cut') : false; break;
      case 'Copy': handled=document.execCommand ? document.execCommand('copy') : false; break;
      case 'Paste': handled=document.execCommand ? document.execCommand('paste') : false; break;
      case 'SelectAll': handled=document.execCommand ? document.execCommand('selectAll') : false; break;
      case 'FindReplace': handled=false; break;
      case 'Bold': handled=document.execCommand ? document.execCommand('bold') : false; break;
      case 'Italic': handled=document.execCommand ? document.execCommand('italic') : false; break;
      case 'Underline': handled=document.execCommand ? document.execCommand('underline') : false; break;
      case 'Strikethrough': handled=document.execCommand ? document.execCommand('strikeThrough') : false; break;
      case 'OrderedList': handled=document.execCommand ? document.execCommand('insertOrderedList') : false; break;
      case 'UnorderedList':
      case 'TaskList':
        handled=document.execCommand ? document.execCommand('insertUnorderedList') : false; break;
      case 'Quote': handled=document.execCommand ? document.execCommand('formatBlock', false, 'blockquote') : false; break;
      case 'Paragraph': handled=document.execCommand ? document.execCommand('formatBlock', false, 'p') : false; break;
      case 'H1': handled=document.execCommand ? document.execCommand('formatBlock', false, 'h1') : false; break;
      case 'H2': handled=document.execCommand ? document.execCommand('formatBlock', false, 'h2') : false; break;
      case 'H3': handled=document.execCommand ? document.execCommand('formatBlock', false, 'h3') : false; break;
      case 'H4': handled=document.execCommand ? document.execCommand('formatBlock', false, 'h4') : false; break;
      case 'InlineCode':
        handled=document.execCommand ? document.execCommand('insertText', false, '`text`') : false; break;
      case 'CodeBlock':
        handled=document.execCommand ? document.execCommand('insertText', false, '\n```\ncode\n```\n') : false; break;
      case 'MathBlock':
        handled=document.execCommand ? document.execCommand('insertText', false, '\n$$\n\n$$\n') : false; break;
      case 'HorizontalRule':
        handled=document.execCommand ? document.execCommand('insertHorizontalRule') : false; break;
      case 'Table':
        handled=document.execCommand ? document.execCommand('insertText', false, '\n| 列1 | 列2 |\n| --- | --- |\n| 内容 | 内容 |\n') : false; break;
      case 'Toc':
        handled=document.execCommand ? document.execCommand('insertText', false, '\n[toc]\n') : false; break;
      case 'Footnote':
        handled=document.execCommand ? document.execCommand('insertText', false, '[^1]\n\n[^1]: ') : false; break;
      case 'Highlight':
        handled=document.execCommand ? document.execCommand('insertText', false, '==高亮==') : false; break;
      case 'Superscript': handled=document.execCommand ? document.execCommand('superscript') : false; break;
      case 'Subscript': handled=document.execCommand ? document.execCommand('subscript') : false; break;
      case 'Comment':
        handled=document.execCommand ? document.execCommand('insertText', false, '<!-- 注释 -->') : false; break;
      case 'InlineMath':
        handled=document.execCommand ? document.execCommand('insertText', false, '$x$') : false; break;
      case 'Link':
        handled=document.execCommand ? document.execCommand('createLink', false, 'https://') : false; break;
      case 'ClearFormatting':
        handled=document.execCommand ? document.execCommand('removeFormat') : false; break;
      default:
        handled=false;
        break;
    }
  }catch(_){
    handled=false;
  }

  if(handled){
    var p=parseInt(col.getAttribute('data-page-index')||currentPageIndex,10);
    var c=parseInt(col.getAttribute('data-col-index')||0,10);
    if(!isFinite(p)) p=0;
    if(!isFinite(c)) c=0;
    cascadeReflow(p,c,getEditingBlockInColumn(col));
    notifyContentChanged();
  }
  return !!handled;
}

window.addEventListener('wheel', function(ev){
  if(!ev || !ev.ctrlKey) return;
  postWheelZoomToHost(ev.deltaY||0);
  if(ev.preventDefault) ev.preventDefault();
}, { passive:false });

function updateSource(bodyHtml,layoutObj,customWidthsArr){
  var src=document.getElementById('source');
  if(!src) return;
  src.innerHTML=bodyHtml;
  if(layoutObj!==undefined) LAYOUT_RESULT=layoutObj;
  if(customWidthsArr!==undefined) customColumnWidths=customWidthsArr||[];
  distribute();
}

function updateSourceIncremental(bodyHtml,layoutObj,customWidthsArr,dirtyStart,dirtyEnd,incrementalMeta){
  var active=document.activeElement;
  var editingActive=!!(active && active.classList && active.classList.contains('col-content'));
  var hasPendingEdits=(localEditSequence>lastNotifiedEditSequence)
    || (editingActive && (Date.now()-lastInputTimestamp)<420);
  var liveMarkdown=extractMarkdownFromPagesDom();
  var incomingMarkdown=extractMarkdownFromBodyHtml(bodyHtml);
  var liveHash=simpleHash(liveMarkdown||'');
  var incomingHash=simpleHash(incomingMarkdown||'');
  var domBlockCount=countDomBlocks();
  if(domBlockCount>0 && (!incomingMarkdown || !incomingMarkdown.trim())){
    // 有可见内容但 incoming 为空，判定为暂时不应用，避免触发全量覆盖。
    return true;
  }
  if(hasPendingEdits && liveMarkdown && liveMarkdown.trim() && incomingHash!==liveHash){
    // 本地编辑领先于 incoming，先保留本地真值，等待下一轮版本追平。
    return true;
  }

  var src=document.getElementById('source');
  if(!src) return false;
  var patched=patchSourceFromDirtyRange(src, bodyHtml, dirtyStart, dirtyEnd);
  if(!patched){
    updateSource(bodyHtml,layoutObj,customWidthsArr);
    return false;
  }
  var layoutPatched=applyLayoutPatch(layoutObj,customWidthsArr,incrementalMeta);
  if(!layoutPatched){
    updateSource(bodyHtml,layoutObj,customWidthsArr);
    return false;
  }
  if(incomingMarkdown && incomingMarkdown.trim()){
    lastKnownMarkdown=incomingMarkdown;
  }
  return true;
}

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
  try{
    var initMd=extractAllMarkdown();
    if(initMd && initMd.trim()){
      lastKnownMarkdown=initMd;
      lastSentMarkdownHash=simpleHash(initMd);
    }
  }catch(_){}
}
if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,50);}
else{window.onload=init;}
";

        #endregion
    }
}
