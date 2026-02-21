namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// 生成嵌入 Vditor 编辑器的 HTML 页面（IR 即时渲染模式 / 暗色主题）
    /// </summary>
    public static class VditorHtmlTemplate
    {
        private const string VDITOR_VERSION = "3.10.8";

        /// <summary>
        /// 生成 Vditor 编辑器 HTML
        /// </summary>
        /// <param name="initialMarkdown">初始 Markdown 内容</param>
        /// <param name="cdnBase">
        /// 资源基础 URL（不含 /dist）。
        /// 本地模式: "https://vditor.local"（通过 WebView2 虚拟主机映射到本地文件）
        /// CDN 模式: "https://cdn.jsdelivr.net/npm/vditor@3.10.8"
        /// null 则使用默认 CDN。
        /// </param>
        public static string Generate(string initialMarkdown, string cdnBase = null)
        {
            string cdn = cdnBase ?? VditorCacheManager.CdnFallback;
            string escapedMd = EscapeForJs(initialMarkdown ?? "");

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<link rel=""stylesheet"" href=""{cdn}/dist/index.css"" />
<script src=""{cdn}/dist/index.min.js""></script>
<style>
  html, body {{
    margin: 0; padding: 0;
    height: 100%; overflow: hidden;
    background: #111418;
    color: #c9d1d9;
  }}
  #vditor {{
    height: 100%;
    background: #111418;
  }}
  :root {{
    --toolbar-h: 34px;
  }}
  .vditor-ir .vditor-reset {{
    /* 正文整体下移一个工具条高度 */
    padding: calc(8px + var(--toolbar-h)) 16px 8px 16px !important;
  }}
  .vditor {{
    border: 0 !important;
    background: #111418 !important;
  }}
  .vditor-toolbar {{
    background: #161b22 !important;
    border-bottom: 1px solid #24292e !important;
    display: flex !important;
    flex-wrap: nowrap !important;
    justify-content: flex-start !important;
    align-items: center !important;
    min-height: var(--toolbar-h) !important;
    height: var(--toolbar-h) !important;
    padding: 0 !important;
    margin: 0 !important;
    gap: 0 !important;
    overflow-x: auto !important;
    overflow-y: hidden !important;
    white-space: nowrap !important;
    box-sizing: border-box !important;
  }}
  /* Vditor 内层工具条容器默认会带 left padding/margin，这里全部清零 */
  .vditor-toolbar > div,
  .vditor-toolbar__left,
  .vditor-toolbar__right {{
    margin: 0 !important;
    padding: 0 !important;
    gap: 0 !important;
  }}
  .vditor-toolbar__left::before,
  .vditor-toolbar__right::before {{
    content: none !important;
    margin: 0 !important;
    padding: 0 !important;
  }}
  .vditor-toolbar > * {{
    flex: 0 0 auto !important;
    margin: 0 !important;
  }}
  .vditor-toolbar__item {{
    color: #8b949e !important;
    flex: 0 0 auto !important;
    margin: 0 !important;
    padding: 4px 6px !important;
  }}
  .vditor-toolbar__item:first-child {{
    margin-left: 0 !important;
  }}
  .vditor-toolbar__item:hover,
  .vditor-toolbar__item--current {{
    background: #24292e !important;
    color: #c9d1d9 !important;
  }}
  .vditor-toolbar__divider {{
    background: #24292e !important;
  }}
  .vditor-content,
  .vditor-ir {{
    background: #111418 !important;
  }}
  .vditor-reset {{
    color: #c9d1d9 !important;
  }}
  .vditor-ir pre.vditor-reset__marker--heading {{
    color: #6e7681 !important;
  }}
  .vditor-counter {{
    background: #161b22 !important;
    color: #6e7681 !important;
    border-top: 1px solid #24292e !important;
  }}
  /* 隐藏主视图滚动条（仍可滚轮/触摸板滚动） */
  * {{ scrollbar-width: none; }}
  ::-webkit-scrollbar {{ display: none; }}
</style>
</head>
<body>
<div id=""vditor""></div>
<script>
var vditor = new Vditor('vditor', {{
  mode: 'ir',
  theme: 'dark',
  lang: 'zh_CN',
  cdn: '{cdn}',
  value: {escapedMd},
  height: '100%',
  cache: {{ enable: false }},
  icon: 'ant',
  toolbar: [
    'headings', 'bold', 'italic', 'strike', '|',
    'list', 'ordered-list', 'check', '|',
    'quote', 'code', 'inline-code', '|',
    'link', 'table', 'line', '|',
    'upload', '|',
    'undo', 'redo'
  ],
  toolbarConfig: {{
    pin: true
  }},
  preview: {{
    theme: {{
      current: 'dark',
      path: '{cdn}/dist/css/content-theme'
    }},
    hljs: {{
      style: 'native',
      lineNumber: true
    }}
  }},
  upload: {{
    handler: function(files) {{
      for (var i = 0; i < files.length; i++) {{
        var file = files[i];
        if (file.type.indexOf('image/') === 0) {{
          var reader = new FileReader();
          reader.onload = function(e) {{
            vditor.insertValue('![](' + e.target.result + ')');
          }};
          reader.readAsDataURL(file);
        }}
      }}
      return null;
    }},
    accept: 'image/*'
  }},
  input: function(value) {{
    try {{
      window.chrome.webview.postMessage(JSON.stringify({{ type: 'input', value: value }}));
    }} catch(e) {{}}
  }},
  after: function() {{
    try {{
      window.chrome.webview.postMessage(JSON.stringify({{ type: 'ready' }}));
    }} catch(e) {{}}
  }}
}});

function setContent(md) {{
  if (vditor) {{
    // #region agent log
    function _nlCount(text) {{
      text = text || '';
      return text.split('\n').length - 1;
    }}
    var before = vditor.getValue() || '';
    vditor.setValue(md, true);
    var afterTrue = vditor.getValue() || '';
    vditor.setValue(md, false);
    var afterFalse = vditor.getValue() || '';
    try {{
      window.chrome.webview.postMessage(JSON.stringify({{
        type: 'editorDebug',
        sessionId: '1db17a',
        runId: 'pre-fix',
        hypothesisId: 'H11',
        location: 'VditorHtmlTemplate:setContent',
        message: 'setContent true/false compare',
        data: {{
          targetLength: (md || '').length,
          beforeLength: before.length,
          afterTrueLength: afterTrue.length,
          afterFalseLength: afterFalse.length,
          sameAsTargetTrue: afterTrue === (md || ''),
          sameAsTargetFalse: afterFalse === (md || ''),
          targetNewLines: _nlCount(md),
          afterTrueNewLines: _nlCount(afterTrue),
          afterFalseNewLines: _nlCount(afterFalse)
        }},
        timestamp: Date.now()
      }}));
    }} catch (e) {{}}
    // #endregion
  }}
}}

function getContent() {{
  return vditor ? vditor.getValue() : '';
}}

// ── 菜单命令 helper ──

/** 模拟点击 Vditor 工具栏按钮 */
function clickToolbar(name) {{
  if (!vditor) return;
  var el = vditor.toolbar && vditor.toolbar.elements && vditor.toolbar.elements[name];
  if (el) {{
    var btn = el.querySelector('button') || el.firstElementChild;
    if (btn) {{ btn.click(); return; }}
  }}
  // 回退：通过 data-type 查找
  var btn2 = document.querySelector('.vditor-toolbar [data-type=""' + name + '""]');
  if (btn2) btn2.click();
}}

/** 用前后缀包裹当前选中文本 */
function wrapSelection(prefix, suffix) {{
  if (!vditor) return;
  var sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) {{
    vditor.insertValue(prefix + suffix);
    return;
  }}
  var text = sel.toString();
  // 如果已包裹则去除，否则添加
  if (text.startsWith(prefix) && text.endsWith(suffix)) {{
    vditor.insertValue(text.slice(prefix.length, text.length - suffix.length));
  }} else {{
    vditor.insertValue(prefix + text + suffix);
  }}
}}

/** 插入标题（H1-H6） */
function insertHeading(level) {{
  if (!vditor) return;
  var prefix = '#'.repeat(level) + ' ';
  vditor.insertValue('\n' + prefix);
}}

/** 转换为普通段落（移除标题前缀） */
function insertParagraph() {{
  if (!vditor) return;
  var md = vditor.getValue();
  // 简化实现：在光标位置插入换行
  vditor.insertValue('\n');
}}

/** 清除选中文本的格式 */
function clearFormatting() {{
  if (!vditor) return;
  var sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return;
  var text = sel.toString();
  // 移除 markdown 格式标记
  var clean = text.replace(/(\*\*|__|~~|==|`)/g, '').replace(/^#+\s*/gm, '');
  vditor.insertValue(clean);
}}

/** 触发查找替换（Ctrl+F） */
function triggerFind() {{
  // 模拟 Ctrl+F 键盘事件
  var e = new KeyboardEvent('keydown', {{
    key: 'f', code: 'KeyF', keyCode: 70,
    ctrlKey: true, bubbles: true
  }});
  document.querySelector('.vditor-ir')?.dispatchEvent(e);
}}

/** 滚动到指定标题文本 */
function scrollToHeading(text) {{
  var container = document.querySelector('.vditor-ir .vditor-reset');
  if (!container) return;
  var headings = container.querySelectorAll('h1,h2,h3,h4,h5,h6');
  for (var i = 0; i < headings.length; i++) {{
    if (headings[i].textContent.trim().indexOf(text) >= 0) {{
      headings[i].scrollIntoView({{ behavior: 'smooth', block: 'start' }});
      // 定位光标到标题
      var range = document.createRange();
      var sel = window.getSelection();
      range.selectNodeContents(headings[i]);
      range.collapse(false);
      sel.removeAllRanges();
      sel.addRange(range);
      break;
    }}
  }}
}}
</script>
</body>
</html>";
        }

        private static string EscapeForJs(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "''";
            return Newtonsoft.Json.JsonConvert.SerializeObject(text);
        }
    }
}
