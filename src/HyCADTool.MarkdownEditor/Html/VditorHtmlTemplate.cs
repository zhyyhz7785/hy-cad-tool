using System.IO;

namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// 生成嵌入 Vditor 编辑器的 HTML 页面（IR 即时渲染模式，支持明暗主题）
    /// </summary>
    public static class VditorHtmlTemplate
    {
        private const string VDITOR_VERSION = "3.10.8";

        /// <summary>CSS 主题文件所在目录（构建后复制到输出目录）</summary>
        private static string ThemesDirectory
        {
            get
            {
                string assemblyDir = Path.GetDirectoryName(typeof(VditorHtmlTemplate).Assembly.Location) ?? "";
                return Path.Combine(assemblyDir, "Html", "Themes");
            }
        }

        private static string LoadThemeCss(bool isDark)
        {
            string fileName = isDark ? "vditor-dark.css" : "vditor-light.css";
            string path = Path.Combine(ThemesDirectory, fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);
            return isDark ? FallbackDarkCss : FallbackLightCss;
        }

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
        /// <param name="isDark">true=暗色主题, false=白色主题</param>
        public static string Generate(string initialMarkdown, string cdnBase = null, bool isDark = true)
        {
            string cdn = cdnBase ?? VditorCacheManager.CdnFallback;
            string escapedMd = EscapeForJs(initialMarkdown ?? "");
            string themeCss = LoadThemeCss(isDark);
            string vditorTheme = isDark ? "dark" : "classic";
            string contentTheme = isDark ? "dark" : "light";
            string darkCssEscaped = EscapeForJs(LoadThemeCss(true));
            string lightCssEscaped = EscapeForJs(LoadThemeCss(false));

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<link rel=""stylesheet"" href=""{cdn}/dist/index.css"" />
<script src=""{cdn}/dist/index.min.js""></script>
<style id=""custom-theme-css"">
{themeCss}
</style>
<script>
window.__darkCss = {darkCssEscaped};
window.__lightCss = {lightCssEscaped};
</script>
</head>
<body>
<div id=""vditor""></div>
<script>
var vditor = new Vditor('vditor', {{
  mode: 'ir',
  theme: '{vditorTheme}',
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
    'undo', 'redo',
    {{name:'indent',tipPosition:'n'}},
    {{name:'outdent',tipPosition:'n'}},
    {{name:'insert-before',tipPosition:'n'}},
    {{name:'insert-after',tipPosition:'n'}}
  ],
  toolbarConfig: {{
    pin: true
  }},
  preview: {{
    theme: {{
      current: '{contentTheme}',
      path: '{cdn}/dist/css/content-theme'
    }},
    hljs: {{
      style: '{(isDark ? "native" : "github")}',
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

    // 拦截 Vditor 内置快捷键中与 Typora 冲突的部分
    // WPF PreviewKeyDown 先于 WebView2，但 WebView2 内部 keydown 仍需阻止 Vditor 默认行为
    var irEl = document.querySelector('.vditor-ir');
    if (irEl) {{
      irEl.addEventListener('keydown', function(e) {{
        // Ctrl+U: Vditor=代码块, Typora=下划线 → 阻止 Vditor
        // Ctrl+D: Vditor=删除线, Typora 不用 → 阻止 Vditor
        // Ctrl+H: Vditor=标题菜单, Typora=无 → 阻止 Vditor
        // Ctrl+L: Vditor=无序列表, Typora=无 → 阻止 Vditor
        // Ctrl+O: Vditor=有序列表, Typora=无 → 阻止 Vditor
        // Ctrl+J: Vditor=任务列表, Typora=无 → 阻止 Vditor
        // Ctrl+G: Vditor=行内代码, Typora=无 → 阻止 Vditor
        // Ctrl+K: Vditor=链接, Typora=链接 → 两者一致，不拦截
        // Ctrl+数字: Vditor 无，Typora=标题 → 无需拦截
        if (e.ctrlKey && !e.shiftKey && !e.altKey) {{
          var block = ['u','d','h','l','o','j','g','m',';'];
          if (block.indexOf(e.key.toLowerCase()) >= 0) {{
            e.preventDefault();
            e.stopPropagation();
            return;
          }}
        }}
        // Ctrl+Shift+H: Vditor=水平线, Typora=无 → 阻止
        if (e.ctrlKey && e.shiftKey && !e.altKey) {{
          var blockShift = ['h','b','e','d','x','u'];
          if (blockShift.indexOf(e.key.toLowerCase()) >= 0) {{
            e.preventDefault();
            e.stopPropagation();
            return;
          }}
        }}
      }}, true);
    }}
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

/** 提升标题级别（H2→H1, H3→H2, 普通段落→H6） */
function promoteHeading() {{
  if (!vditor) return;
  var el = vditor[vditor.currentMode].element;
  var sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return;
  var node = sel.anchorNode;
  while (node && node !== el) {{
    if (/^H[1-6]$/i.test(node.nodeName)) {{
      var lv = parseInt(node.nodeName[1]);
      if (lv > 1) {{ insertHeading(lv - 1); }}
      return;
    }}
    node = node.parentNode;
  }}
  insertHeading(6);
}}

/** 降低标题级别（H1→H2, H5→H6, H6→普通段落） */
function demoteHeading() {{
  if (!vditor) return;
  var el = vditor[vditor.currentMode].element;
  var sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return;
  var node = sel.anchorNode;
  while (node && node !== el) {{
    if (/^H[1-6]$/i.test(node.nodeName)) {{
      var lv = parseInt(node.nodeName[1]);
      if (lv < 6) {{ insertHeading(lv + 1); }}
      else {{ insertParagraph(); }}
      return;
    }}
    node = node.parentNode;
  }}
}}

/** 列表缩进 */
function indentList() {{ clickToolbar('indent'); }}

/** 列表减少缩进 */
function outdentList() {{ clickToolbar('outdent'); }}

/** 在上方插入空段落 */
function insertParagraphAbove() {{ clickToolbar('insert-before'); }}

/** 在下方插入空段落 */
function insertParagraphBelow() {{ clickToolbar('insert-after'); }}

/** 切换任务列表完成状态 */
function toggleTaskStatus() {{
  if (!vditor) return;
  var sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return;
  var node = sel.anchorNode;
  var el = vditor[vditor.currentMode].element;
  while (node && node !== el) {{
    if (node.nodeName === 'LI' || (node.nodeType === 1 && node.classList && node.classList.contains('vditor-task'))) {{
      var cb = node.querySelector('input[type=""checkbox""]');
      if (cb) {{
        cb.checked = !cb.checked;
        cb.dispatchEvent(new Event('change', {{ bubbles: true }}));
      }}
      return;
    }}
    node = node.parentNode;
  }}
}}

/** 插入图像 Markdown */
function insertImage(alt, url) {{
  if (!vditor) return;
  vditor.insertValue('![' + (alt || '') + '](' + (url || '') + ')');
}}

/** 插入链接引用 */
function insertLinkReference() {{
  if (!vditor) return;
  vditor.insertValue('[链接文本][ref]\\n\\n[ref]: url ""标题""');
}}

/** 运行时切换 Vditor 主题（不重建 DOM） */
function switchEditorTheme(isDark) {{
  if (!vditor) return;
  vditor.setTheme(isDark ? 'dark' : 'classic', isDark ? 'dark' : 'light');
  var custom = document.getElementById('custom-theme-css');
  if (custom) {{
    custom.textContent = isDark ? (window.__darkCss || '') : (window.__lightCss || '');
  }}
}}

// 隐藏编程专用工具栏按钮（indent/outdent/insert-before/insert-after）
(function() {{
  var hideNames = ['indent','outdent','insert-before','insert-after'];
  function hideButtons() {{
    if (!vditor || !vditor.toolbar || !vditor.toolbar.elements) return;
    hideNames.forEach(function(n) {{
      var el = vditor.toolbar.elements[n];
      if (el) el.style.display = 'none';
    }});
  }}
  if (document.readyState === 'complete') setTimeout(hideButtons, 100);
  else window.addEventListener('load', function() {{ setTimeout(hideButtons, 100); }});
}})();

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

        private const string FallbackDarkCss = @"
html, body { margin:0; padding:0; height:100%; overflow:hidden; background:#111418; color:#c9d1d9; }
#vditor { height:100%; background:#111418; }
:root { --toolbar-h:34px; }
.vditor-ir .vditor-reset { padding: calc(8px + var(--toolbar-h)) 16px 8px 16px !important; }
.vditor { border:0!important; background:#111418!important; }
.vditor-toolbar { background:#161b22!important; border-bottom:1px solid #24292e!important; }
.vditor-toolbar__item { color:#8b949e!important; }
.vditor-toolbar__item:hover,.vditor-toolbar__item--current { background:#24292e!important; color:#c9d1d9!important; }
.vditor-content,.vditor-ir { background:#111418!important; }
.vditor-reset { color:#c9d1d9!important; }
.vditor-counter { background:#161b22!important; color:#6e7681!important; }
* { scrollbar-width:none; } ::-webkit-scrollbar { display:none; }";

        private const string FallbackLightCss = @"
html, body { margin:0; padding:0; height:100%; overflow:hidden; background:#ffffff; color:#24292f; }
#vditor { height:100%; background:#ffffff; }
:root { --toolbar-h:34px; }
.vditor-ir .vditor-reset { padding: calc(8px + var(--toolbar-h)) 16px 8px 16px !important; }
.vditor { border:0!important; background:#ffffff!important; }
.vditor-toolbar { background:#f5f7fa!important; border-bottom:1px solid #d0d7de!important; }
.vditor-toolbar__item { color:#57606a!important; }
.vditor-toolbar__item:hover,.vditor-toolbar__item--current { background:#eef2f6!important; color:#24292f!important; }
.vditor-content,.vditor-ir { background:#ffffff!important; }
.vditor-reset { color:#24292f!important; }
.vditor-counter { background:#f5f7fa!important; color:#6e7781!important; }
* { scrollbar-width:none; } ::-webkit-scrollbar { display:none; }";
    }
}
