namespace HyCADTool.Features.DesignSpec.Domain.Models
{
    /// <summary>
    /// 生成嵌入 Vditor 编辑器的 HTML 页面（IR 即时渲染模式 / 暗色主题）
    /// Vditor: https://github.com/Vanessa219/vditor
    /// </summary>
    public static class VditorHtmlTemplate
    {
        private const string VDITOR_VERSION = "3.10.8";
        private const string CDN_BASE = "https://unpkg.com/vditor@" + VDITOR_VERSION;

        /// <summary>
        /// 生成完整 HTML 页面字符串
        /// </summary>
        /// <param name="initialMarkdown">初始 Markdown 内容（需 JS 转义）</param>
        public static string Generate(string initialMarkdown)
        {
            string escapedMd = EscapeForJs(initialMarkdown ?? "");

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
<link rel=""stylesheet"" href=""{CDN_BASE}/dist/index.css"" />
<script src=""{CDN_BASE}/dist/index.min.js""></script>
<style>
  html, body {{
    margin: 0; padding: 0;
    height: 100%; overflow: hidden;
    background: #1e1e1e;
  }}
  #vditor {{
    height: 100%;
  }}
  /* 隐藏 Vditor 底部信息栏，节省空间 */
  .vditor-ir .vditor-reset {{
    padding: 8px 16px !important;
  }}
  /* 超细滚动条 */
  ::-webkit-scrollbar {{ width: 6px; height: 6px; }}
  ::-webkit-scrollbar-track {{ background: transparent; }}
  ::-webkit-scrollbar-thumb {{ background: #555; border-radius: 3px; }}
  ::-webkit-scrollbar-thumb:hover {{ background: #888; }}
</style>
</head>
<body>
<div id=""vditor""></div>
<script>
var vditor = new Vditor('vditor', {{
  mode: 'ir',
  theme: 'dark',
  lang: 'zh_CN',
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
    'undo', 'redo', '|',
    'edit-mode', 'outline'
  ],
  toolbarConfig: {{
    pin: true
  }},
  preview: {{
    theme: {{
      current: 'dark',
      path: '{CDN_BASE}/dist/css/content-theme'
    }},
    hljs: {{
      style: 'native',
      lineNumber: true
    }}
  }},
  upload: {{
    // 无服务器上传，使用 base64 内联
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
      return null; // 表示已自行处理
    }},
    accept: 'image/*'
  }},
  input: function(value) {{
    // 将 Markdown 内容发送给 C#
    try {{
      window.chrome.webview.postMessage(JSON.stringify({{ type: 'input', value: value }}));
    }} catch(e) {{}}
  }},
  after: function() {{
    // 编辑器就绪，通知 C#
    try {{
      window.chrome.webview.postMessage(JSON.stringify({{ type: 'ready' }}));
    }} catch(e) {{}}
  }}
}});

// 供 C# 调用的函数
function setContent(md) {{
  if (vditor) vditor.setValue(md, true);
}}

function getContent() {{
  return vditor ? vditor.getValue() : '';
}}
</script>
</body>
</html>";
        }

        /// <summary>
        /// 将字符串转义为 JS 字符串字面量（用反引号包裹的模板字符串）
        /// </summary>
        private static string EscapeForJs(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "''";

            // 使用 JSON 序列化来安全转义
            // Newtonsoft.Json 会正确处理所有特殊字符
            return Newtonsoft.Json.JsonConvert.SerializeObject(text);
        }
    }
}
