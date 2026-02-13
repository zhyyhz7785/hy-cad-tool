namespace HyCADTool.MarkdownEditor.Html
{
    /// <summary>
    /// 生成嵌入 Vditor 编辑器的 HTML 页面（IR 即时渲染模式 / 暗色主题）
    /// </summary>
    public static class VditorHtmlTemplate
    {
        private const string VDITOR_VERSION = "3.10.8";
        private const string CDN_BASE = "https://unpkg.com/vditor@" + VDITOR_VERSION;

        public static string Generate(string initialMarkdown)
        {
            string escapedMd = EscapeForJs(initialMarkdown ?? "");

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
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
  .vditor-ir .vditor-reset {{
    padding: 8px 16px !important;
  }}
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
  if (vditor) vditor.setValue(md, true);
}}

function getContent() {{
  return vditor ? vditor.getValue() : '';
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
