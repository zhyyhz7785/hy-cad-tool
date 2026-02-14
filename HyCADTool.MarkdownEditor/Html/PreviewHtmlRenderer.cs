using Markdig;
using System;
using System.Globalization;
using System.Text;
using HyCADTool.MarkdownEditor.Models;

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

        public static string ToInteractiveHtml(string markdown, int columnCount, double previewScale = 1.0, EditorConfig config = null)
        {
            string body = string.IsNullOrEmpty(markdown)
                ? "<p class=\"empty\">(无内容)</p>"
                : Markdown.ToHtml(markdown, Pipeline);

            int cols = Math.Max(1, Math.Min(10, columnCount));
            double scale = Math.Max(0.1, Math.Min(5.0, previewScale));
            EditorConfig cfg = config ?? new EditorConfig();
            string columnsHtml = BuildColumnsHtml(cols);
            string footerHtml = BuildFooterHtml(cols);
            string dynamicCss = BuildDynamicCss(scale, cfg);
            string dynamicJs = BuildDynamicJs(scale, cfg);

            return "<!DOCTYPE html>\n<html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />"
                + "<style>" + dynamicCss + "</style>"
                + "</head><body>"
                + "<div id=\"source\" style=\"display:none;\">" + body + "</div>"
                + "<div class=\"viewport\"><div class=\"paper\" id=\"paper\"><div class=\"paper-inner\" id=\"main\">"
                + columnsHtml
                + "</div>"
                + "<div class=\"paper-resize-right\" onmousedown=\"startPaperResize(event,'right')\"></div>"
                + "<div class=\"paper-resize-bottom\" onmousedown=\"startPaperResize(event,'bottom')\"></div>"
                + "<div class=\"paper-resize-corner\" onmousedown=\"startPaperResize(event,'corner')\"></div>"
                + "</div>"
                + footerHtml
                + "</div>"
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

            var tokens = new (string token, string value)[]
            {
                ("/*BASE_FONT_SIZE*/", $"{basePx.ToString("0.###", CultureInfo.InvariantCulture)}px"),
                ("/*BASE_LINE_HEIGHT*/", lineHeightFactor.ToString("0.###", CultureInfo.InvariantCulture)),
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

        private static string BuildDynamicJs(double previewScale, EditorConfig cfg)
        {
            double safePreviewScale = Math.Max(0.01, previewScale);
            double textSizeMm = Math.Max(0.1, cfg.TextSize);
            double textXScale = Math.Max(0.1, cfg.TextXScale);
            double pageWidthMm = Math.Max(1.0, cfg.PageWidthMm);
            const double contentPaddingX = 20.0; // .col-content 左右 padding 合计

            return JS_TEMPLATE
                .Replace("/*PREVIEW_SCALE*/", safePreviewScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_SIZE_MM*/", textSizeMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*TEXT_X_SCALE*/", textXScale.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*PAGE_WIDTH_MM*/", pageWidthMm.ToString("0.#####", CultureInfo.InvariantCulture))
                .Replace("/*CONTENT_PADDING_X*/", contentPaddingX.ToString("0.#####", CultureInfo.InvariantCulture));
        }

        private static double Round(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static string BuildColumnsHtml(int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append("<div class=\"col-wrap\">");
                sb.Append("<div class=\"col-content\" id=\"col-").Append(i).Append("\"></div>");
                sb.Append("<div class=\"col-vhandle\" onmousedown=\"startV(event,").Append(i).Append(")\"></div>");
                sb.Append("</div>");

                if (i < count - 1)
                {
                    sb.Append("<div class=\"col-gap\" onmousedown=\"startH(event,").Append(i).Append(")\"></div>");
                }
            }
            return sb.ToString();
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
  font-family:'Microsoft YaHei','Segoe UI',sans-serif;
  font-size:/*BASE_FONT_SIZE*/;line-height:/*BASE_LINE_HEIGHT*/
}

.viewport{min-width:100%;min-height:100%;padding:0;overflow:hidden}
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
  padding:4px 0;color:#8b949e;font-size:11px;
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
window.previewStats={schemaVersion:2,charsPerColumn:[],columnParagraphIndicesText:'',columnParagraphIndices:[],columns:[],blockTypeCounts:{}};
var hDiv=-1,sX=0,sW=[];
var vCol=-1,sY=0,sH=0;
var pMode='',pStartX=0,pStartY=0,pStartW=0,pStartH=0;
var PREVIEW_SCALE=/*PREVIEW_SCALE*/;
var TEXT_SIZE_MM=/*TEXT_SIZE_MM*/;
var TEXT_X_SCALE=/*TEXT_X_SCALE*/;
var PAGE_WIDTH_MM=/*PAGE_WIDTH_MM*/;
var CONTENT_PADDING_X=/*CONTENT_PADDING_X*/;
var CHAR_WIDTH_MM=Math.max(0.01,TEXT_SIZE_MM*TEXT_X_SCALE);

function getPreviewStats(){ return window.previewStats || null; }
function getColChars(){return (window.previewStats && window.previewStats.charsPerColumn ? window.previewStats.charsPerColumn : []).join(',');}
function getColParas(){return window.previewStats && window.previewStats.columnParagraphIndicesText ? window.previewStats.columnParagraphIndicesText : '';}
function getPaperSize(){
  var p=document.getElementById('paper');
  if(!p) return '';
  return p.offsetWidth + ',' + p.offsetHeight;
}

function postPaperScaleToHost(scale){
  if(!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) return;
  if(!isFinite(scale) || scale<=0) return;
  window.chrome.webview.postMessage({ type:'paperScale', value:scale });
}

function postWheelZoomToHost(deltaY){
  if(!window.chrome || !window.chrome.webview || !window.chrome.webview.postMessage) return;
  if(!isFinite(deltaY)) return;
  window.chrome.webview.postMessage({ type:'wheelZoom', deltaY:deltaY });
}

function toColParasText(){
  var parts=[];
  for(var i=0;i<window.colParas.length;i++){parts.push(window.colParas[i].join(','));}
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

function buildBlockTypeCountForColumn(sourceElements, paraIndices){
  var map={}, i=0, idx=0, el=null, key='';
  for(i=0;i<paraIndices.length;i++){
    idx=paraIndices[i];
    el=(sourceElements && idx>=0 && idx<sourceElements.length) ? sourceElements[idx] : null;
    key=normalizeBlockType(el ? el.tagName : '');
    map[key]=(map[key]||0)+1;
  }
  return map;
}

function setEmptyStats(cols){
  var i=0, colStats=[], colChars=[], emptyMap={};
  for(i=0;i<cols.length;i++){
    colChars.push(0);
    colStats.push({
      index:i,
      charsPerLine:0,
      paragraphCount:0,
      totalChars:0,
      totalDisplayUnits:0,
      avgDisplayUnitsPerChar:0,
      blockTypes:{}
    });
    var ch0=document.getElementById('chars-'+i);
    if(ch0) ch0.innerHTML='0 字/行';
    var info0=document.getElementById('info-'+i);
    if(info0) info0.innerHTML='0 段 · 0 字';
  }
  window.colChars=colChars;
  window.previewStats={
    schemaVersion:2,
    charsPerColumn:colChars,
    columnParagraphIndicesText:toColParasText(),
    columnParagraphIndices:window.colParas,
    columns:colStats,
    blockTypeCounts:emptyMap
  };
}

function syncFooter(){
  var wraps=document.querySelectorAll('.col-wrap');
  var items=document.querySelectorAll('.col-footer-item');
  var seps=document.querySelectorAll('.col-footer-sep');
  var gaps=document.querySelectorAll('.col-gap');
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

function distribute(){
  var src=document.getElementById('source');
  var cols=document.querySelectorAll('.col-content');
  var i,e,clone,ci=0;
  var globalBlockTypes={};

  for(i=0;i<cols.length;i++){cols[i].innerHTML='';cols[i].className='col-content';}
  window.colParas=[];
  for(i=0;i<cols.length;i++) window.colParas.push([]);

  var els=src.children;
  if(!els||els.length===0){
    if(cols[0])cols[0].innerHTML='<p class=""empty"">(无内容)</p>';
    setEmptyStats(cols);
    syncFooter();
    return;
  }

  for(e=0;e<els.length;e++){
    var key=normalizeBlockType(els[e] ? els[e].tagName : '');
    globalBlockTypes[key]=(globalBlockTypes[key]||0)+1;
    if(ci>=cols.length) ci=cols.length-1;
    clone=els[e].cloneNode(true);
    cols[ci].appendChild(clone);
    if(cols[ci].scrollHeight>cols[ci].clientHeight+2){
      cols[ci].removeChild(clone);
      ci++;
      if(ci<cols.length){cols[ci].appendChild(clone);window.colParas[ci].push(e);}
      else{ci=cols.length-1;cols[ci].appendChild(clone);window.colParas[ci].push(e);}
    } else {
      window.colParas[ci].push(e);
    }
  }
  if(cols.length>0) cols[cols.length-1].className='col-content last';

  var colChars=[], colStats=[];
  for(i=0;i<cols.length;i++){
    var contentW=Math.max(1, cols[i].clientWidth-CONTENT_PADDING_X);
    var contentMm=contentW/Math.max(0.01, PREVIEW_SCALE);
    var unitPerLine=Math.floor(contentMm/CHAR_WIDTH_MM);
    var rawText=(cols[i].innerText||cols[i].textContent||'');
    var metrics=calcTextMetrics(rawText);
    var avgUnitsPerChar=metrics.chars>0 ? (metrics.units/metrics.chars) : 1;
    var cpl=Math.floor(unitPerLine/Math.max(0.5, avgUnitsPerChar));
    if(cpl<1)cpl=1;
    colChars.push(cpl);
    var ch=document.getElementById('chars-'+i);
    if(ch) ch.innerHTML=cpl+' 字/行';
    var info=document.getElementById('info-'+i);
    if(info) info.innerHTML=window.colParas[i].length+' 段 · '+metrics.chars+' 字';
    colStats.push({
      index:i,
      charsPerLine:cpl,
      paragraphCount:window.colParas[i].length,
      totalChars:metrics.chars,
      totalDisplayUnits:metrics.units,
      avgDisplayUnitsPerChar:avgUnitsPerChar,
      blockTypes:buildBlockTypeCountForColumn(els, window.colParas[i])
    });
  }
  window.colChars=colChars;
  window.previewStats={
    schemaVersion:2,
    charsPerColumn:colChars,
    columnParagraphIndicesText:toColParasText(),
    columnParagraphIndices:window.colParas,
    columns:colStats,
    blockTypeCounts:globalBlockTypes
  };

  syncFooter();
}

function startH(ev,idx){
  hDiv=idx;
  sX=ev.clientX;
  var w=document.querySelectorAll('.col-wrap');
  sW=[];
  for(var i=0;i<w.length;i++) sW.push(w[i].offsetWidth);
  if(ev.preventDefault) ev.preventDefault();
}

function startV(ev,idx){
  vCol=idx;
  sY=ev.clientY;
  var cols=document.querySelectorAll('.col-content');
  if(idx>=0 && idx<cols.length){
    sH=cols[idx].offsetHeight;
  }
  if(ev.preventDefault) ev.preventDefault();
}

function startPaperResize(ev,mode){
  var paper=document.getElementById('paper');
  if(!paper) return;
  pMode=mode||'';
  pStartX=ev.clientX;
  pStartY=ev.clientY;
  pStartW=paper.offsetWidth;
  pStartH=paper.offsetHeight;
  if(ev.preventDefault) ev.preventDefault();
}

document.onmousemove=function(ev){
  if(pMode){
    var paper=document.getElementById('paper');
    var main=document.getElementById('main');
    if(!paper || !main) return;

    var dx=ev.clientX-pStartX;
    var dy=ev.clientY-pStartY;
    var cs=window.getComputedStyle(main);
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

    paper.style.width=nw+'px';
    paper.style.height=nh+'px';
    return;
  }

  if(vCol>=0){
    var dy=ev.clientY-sY;
    var cols=document.querySelectorAll('.col-content');
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
      cols[vCol].style.webkitFlex='none';
      cols[vCol].style.msFlex='none';
      cols[vCol].style.flex='none';
      cols[vCol].style.height=nh+'px';
      distribute();
    }
    return;
  }

  if(hDiv>=0){
    var dx=ev.clientX-sX;
    var w=document.querySelectorAll('.col-wrap');
    var li=hDiv,ri=hDiv+1;
    if(li>=w.length||ri>=w.length) return;

    var minW=80;
    var nl=sW[li]+dx;
    var nr=sW[ri]-dx;
    if(nl<minW){nr-=(minW-nl);nl=minW;}
    if(nr<minW){nl-=(minW-nr);nr=minW;}
    if(nl<minW||nr<minW) return;

    w[li].style.webkitFlex='none'; w[li].style.msFlex='none'; w[li].style.flex='none'; w[li].style.width=nl+'px';
    w[ri].style.webkitFlex='none'; w[ri].style.msFlex='none'; w[ri].style.flex='none'; w[ri].style.width=nr+'px';
    distribute();
  }
};

document.onmouseup=function(){
  var resized = !!pMode;
  hDiv=-1;
  vCol=-1;
  if(resized){
    // 拖拽中不重排，松手后一次性重排，避免画面频繁跳动
    distribute();
    var paper=document.getElementById('paper');
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

function init(){distribute();}
if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,50);}
else{window.onload=init;}
";

        #endregion
    }
}
