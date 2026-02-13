using Markdig;
using System;
using System.Text;
using HyCADTool.MarkdownEditor.Models;

namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// Markdown → 交互式分栏 HTML 预览（用于 WebBrowser IE11 控件）
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

            int cols = Math.Max(1, Math.Min(6, columnCount));
            double scale = Math.Max(0.1, Math.Min(3.0, previewScale));
            string columnsHtml = BuildColumnsHtml(cols);
            string dynamicCss = BuildDynamicCss(scale, config ?? new EditorConfig());

            return "<!DOCTYPE html>\n<html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />"
                + "<style>" + dynamicCss + "</style>"
                + "</head><body>"
                + "<div id=\"source\" style=\"display:none;\">" + body + "</div>"
                + "<div class=\"container\" id=\"main\">" + columnsHtml + "</div>"
                + "<script>" + JS + "</script>"
                + "</body></html>";
        }

        private static string BuildDynamicCss(double previewScale, EditorConfig cfg)
        {
            double basePx = 12.0 * previewScale;

            string h1Margin = $"margin:{cfg.H1SpaceBefore:F1}em 0 {cfg.H1SpaceAfter:F1}em";
            string h2Margin = $"margin:{cfg.H2SpaceBefore:F1}em 0 {cfg.H2SpaceAfter:F1}em";
            string h3Margin = $"margin:{cfg.H3SpaceBefore:F1}em 0 {cfg.H3SpaceAfter:F1}em";
            string pMargin = $"margin:0 0 {cfg.PSpaceAfter:F1}em";
            string liMargin = $"margin:0 0 {cfg.LiSpaceAfter:F1}em";
            string bqMargin = $"margin:{cfg.QuoteSpaceBefore:F1}em 0 {cfg.QuoteSpaceAfter:F1}em";

            return CSS_TEMPLATE
                .Replace("font-size:12px", $"font-size:{basePx:F1}px")
                .Replace("/*H1_MARGIN*/", h1Margin)
                .Replace("/*H2_MARGIN*/", h2Margin)
                .Replace("/*H3_MARGIN*/", h3Margin)
                .Replace("/*P_MARGIN*/", pMargin)
                .Replace("/*LI_MARGIN*/", liMargin)
                .Replace("/*BQ_MARGIN*/", bqMargin);
        }

        private static string BuildColumnsHtml(int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append("<div class=\"col-wrap\">");
                sb.Append("<div class=\"col-header\">第 ").Append(i + 1).Append(" 栏 <span class=\"col-chars\" id=\"chars-").Append(i).Append("\"></span></div>");
                sb.Append("<div class=\"col-content\" id=\"col-").Append(i).Append("\"></div>");
                sb.Append("<div class=\"vhandle\" onmousedown=\"startV(event,").Append(i).Append(")\"></div>");
                sb.Append("<div class=\"col-info\" id=\"info-").Append(i).Append("\"></div>");
                sb.Append("</div>");

                if (i < count - 1)
                {
                    sb.Append("<div class=\"hhandle\" onmousedown=\"startH(event,").Append(i).Append(")\"></div>");
                }
            }
            return sb.ToString();
        }

        #region CSS

        private const string CSS_TEMPLATE = @"
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%;overflow:hidden}
body{background:#1e1e1e;color:#d4d4d4;font-family:'Microsoft YaHei','Segoe UI',sans-serif;font-size:12px;line-height:1.55}

.container{display:-ms-flexbox;display:flex;-ms-flex-direction:row;flex-direction:row;width:100%;height:100%}
.col-wrap{-ms-flex:1;flex:1;display:-ms-flexbox;display:flex;-ms-flex-direction:column;flex-direction:column;min-width:0}
.col-header{background:#264f78;border-left:4px solid #007ACC;padding:4px 10px;font-size:11px;font-weight:600;-ms-flex-negative:0;flex-shrink:0;display:-ms-flexbox;display:flex;-ms-flex-pack:justify;justify-content:space-between}
.col-chars{font-weight:normal;color:#8cc4e8}
.col-content{-ms-flex:1;flex:1;overflow:hidden;padding:8px 10px;background:#252526}
.col-content.last{overflow-y:auto}
.col-info{-ms-flex-negative:0;flex-shrink:0;background:#1e1e1e;padding:3px 10px;font-size:10px;color:#555;border-top:1px solid #333}

.vhandle{height:6px;background:#333;cursor:ns-resize;-ms-flex-negative:0;flex-shrink:0}
.vhandle:hover{background:#007ACC}
.hhandle{width:5px;background:#333;cursor:ew-resize;-ms-flex-negative:0;flex-shrink:0}
.hhandle:hover{background:#007ACC}

.empty{color:#555;font-style:italic;text-align:center;padding:30px}

h1{font-size:1.6em;color:#4ec9b0;/*H1_MARGIN*/;border-bottom:1px solid #3e3e42;padding-bottom:3px}
h2{font-size:1.3em;color:#4ec9b0;/*H2_MARGIN*/}
h3{font-size:1.1em;color:#dcdcaa;/*H3_MARGIN*/}
h4,h5,h6{font-size:1em;color:#9cdcfe;margin:5px 0 2px}
p{/*P_MARGIN*/}
strong{color:#fff}
em{color:#c586c0}
ul,ol{padding-left:18px;margin:4px 0}
li{/*LI_MARGIN*/}
code{background:#1e1e1e;color:#ce9178;padding:1px 4px;border-radius:2px;font-family:Consolas,monospace;font-size:0.9em}
pre{background:#1e1e1e;border:1px solid #3e3e42;border-radius:3px;padding:6px;margin:4px 0;overflow-x:auto}
pre code{background:none;padding:0}
blockquote{border-left:3px solid #569cd6;padding:3px 10px;/*BQ_MARGIN*/;color:#9cdcfe;background:#1e1e1e}
hr{border:none;border-top:1px solid #3e3e42;margin:8px 0}
table{border-collapse:collapse;width:100%;margin:6px 0}
th,td{border:1px solid #3e3e42;padding:3px 6px;font-size:0.9em;text-align:left}
th{background:#1e1e1e;color:#4ec9b0;font-weight:bold}
tr:nth-child(even){background:#2d2d30}
";

        #endregion

        #region JavaScript

        private const string JS = @"
var vCol=-1,hDiv=-1,sY=0,sX=0,sH=0,sW=[];
window.colChars=[];
window.colParas=[];
function getColChars(){return window.colChars.join(',');}
function getColParas(){
  var parts=[];
  for(var i=0;i<window.colParas.length;i++){parts.push(window.colParas[i].join(','));}
  return parts.join('|');
}

function distribute(){
  var src=document.getElementById('source');
  var cols=document.querySelectorAll('.col-content');
  var i,e,clone,ci=0;

  for(i=0;i<cols.length;i++){cols[i].innerHTML='';cols[i].className='col-content';}
  window.colParas=[];
  for(i=0;i<cols.length;i++) window.colParas.push([]);

  var els=src.children;
  if(!els||els.length===0){if(cols[0])cols[0].innerHTML='<p class=""empty"">(无内容)</p>';return;}

  for(e=0;e<els.length;e++){
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

  window.colChars=[];
  for(i=0;i<cols.length;i++){
    var probe=document.createElement('span');
    probe.style.cssText='visibility:hidden;position:absolute;white-space:nowrap;font-size:'+window.getComputedStyle(cols[i]).fontSize;
    probe.innerHTML='测试文字共十个字符宽度计算';
    cols[i].appendChild(probe);
    var charPx=probe.offsetWidth/12;
    cols[i].removeChild(probe);
    var contentW=cols[i].clientWidth-20;
    var cpl=Math.floor(contentW/charPx);
    if(cpl<1)cpl=1;
    window.colChars.push(cpl);
    var ch=document.getElementById('chars-'+i);
    if(ch) ch.innerHTML=cpl+' 字/行';
    var info=document.getElementById('info-'+i);
    if(info) info.innerHTML=cols[i].children.length+' 段';
  }
}

function startV(ev,idx){vCol=idx;sY=ev.clientY;var c=document.querySelectorAll('.col-content')[idx];sH=c.offsetHeight;ev.preventDefault();}
function startH(ev,idx){hDiv=idx;sX=ev.clientX;var w=document.querySelectorAll('.col-wrap');sW=[];for(var i=0;i<w.length;i++)sW.push(w[i].offsetWidth);ev.preventDefault();}

document.onmousemove=function(ev){
  if(vCol>=0){
    var d=ev.clientY-sY;
    var c=document.querySelectorAll('.col-content')[vCol];
    var nh=sH+d; if(nh<60)nh=60;
    c.style.height=nh+'px';c.style.webkitFlex='none';c.style.msFlex='none';c.style.flex='none';
    distribute();
  }
  if(hDiv>=0){
    var dx=ev.clientX-sX;
    var w=document.querySelectorAll('.col-wrap');
    var li=hDiv,ri=hDiv+1;
    if(li<w.length&&ri<w.length){
      var nl=sW[li]+dx; if(nl<60)nl=60;
      var nr=sW[ri]-dx; if(nr<60)nr=60;
      w[li].style.webkitFlex='none';w[li].style.msFlex='none';w[li].style.flex='none';w[li].style.width=nl+'px';
      w[ri].style.webkitFlex='none';w[ri].style.msFlex='none';w[ri].style.flex='none';w[ri].style.width=nr+'px';
    }
    distribute();
  }
};

document.onmouseup=function(){vCol=-1;hDiv=-1;};

function init(){distribute();}
if(document.readyState==='complete'||document.readyState==='interactive'){setTimeout(init,50);}
else{window.onload=init;}
";

        #endregion
    }
}
