using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HyCADTool.MarkdownEditor.Services;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    public class FilePathChangedEventArgs : EventArgs
    {
        public string OldPath { get; }
        public string NewPath { get; }

        public FilePathChangedEventArgs(string oldPath, string newPath)
        {
            OldPath = oldPath ?? "";
            NewPath = newPath ?? "";
        }
    }

    public partial class LeftPanelControl : UserControl
    {
        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);

        private bool _showFileTab = true;
        private bool _updatingFileList;
        private bool _isDarkTheme = true;
        private string _currentFilePath = "";
        private readonly FileTreeService _fileTreeService = new FileTreeService();

        public event EventHandler<string> FileOpenRequested;
        public event EventHandler<string> OutlineHeadingSelected;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<FilePathChangedEventArgs> CurrentFilePathChanged;
        public event EventHandler CurrentFileClearedRequested;

        public LeftPanelControl()
        {
            InitializeComponent();
            ApplyLeftPanelTab();
        }

        public void SetThemeMode(bool isDarkTheme)
        {
            _isDarkTheme = isDarkTheme;
        }

        public void RefreshOutline(string markdown)
        {
            OutlineList.Items.Clear();
            if (string.IsNullOrWhiteSpace(markdown)) return;

            foreach (Match match in HeadingRegex.Matches(markdown))
            {
                int level = match.Groups[1].Value.Length;
                string title = match.Groups[2].Value.Trim();
                string indent = new string(' ', (level - 1) * 2);
                OutlineList.Items.Add(new ListBoxItem
                {
                    Content = indent + title,
                    Tag = title,
                    Padding = new Thickness(4 + (level - 1) * 12, 2, 4, 2),
                    FontSize = level <= 2 ? 12 : 11,
                    Foreground = level == 1
                        ? ThemeBrush("ThemeTextPrimaryBrush")
                        : level == 2
                            ? ThemeBrush("ThemeTextSecondaryBrush")
                            : ThemeBrush("ThemeTextMutedBrush"),
                });
            }
        }

        public void RefreshFileList(string currentFilePath)
        {
            _updatingFileList = true;
            _currentFilePath = currentFilePath ?? "";
            try
            {
                FileTree.Items.Clear();
                if (string.IsNullOrWhiteSpace(_currentFilePath)) return;

                string rootDir = Path.GetDirectoryName(_currentFilePath);
                if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) return;

                var rootNode = _fileTreeService.BuildTreeNode(rootDir, _currentFilePath, ThemeBrush);
                rootNode.IsExpanded = true;
                FileTree.Items.Add(rootNode);
            }
            finally
            {
                _updatingFileList = false;
            }
        }

        private Brush ThemeBrush(string key)
        {
            if (TryFindResource(key) is Brush brush)
                return brush;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c9d1d9"));
        }

        private void ApplyLeftPanelTab()
        {
            if (FileTree == null || OutlineList == null || FileTabBtn == null || OutlineTabBtn == null) return;

            FileTree.Visibility = _showFileTab ? Visibility.Visible : Visibility.Collapsed;
            OutlineList.Visibility = _showFileTab ? Visibility.Collapsed : Visibility.Visible;

            FileTabBtn.Foreground = _showFileTab ? ThemeBrush("ThemeTextPrimaryBrush") : ThemeBrush("ThemeTextSecondaryBrush");
            OutlineTabBtn.Foreground = _showFileTab ? ThemeBrush("ThemeTextSecondaryBrush") : ThemeBrush("ThemeTextPrimaryBrush");
        }

        private string GetSelectedTreePath()
        {
            if (FileTree.SelectedItem is TreeViewItem ti && ti.Tag is string p) return p;
            return null;
        }

        private string ResolveDir(string path) => _fileTreeService.ResolveDir(path);

        private static SolidColorBrush BrushFromHex(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        private string PromptInput(string title, string prompt, string defaultValue)
        {
            bool dark = _isDarkTheme;
            string winBg = dark ? "#1c2128" : "#ffffff";
            string textPrimary = dark ? "#c9d1d9" : "#24292f";
            string inputBg = dark ? "#0d1117" : "#ffffff";
            string border = dark ? "#30363d" : "#d0d7de";
            string btnSecondaryBg = dark ? "#30363d" : "#e5e7eb";

            var win = new Window
            {
                Title = title,
                Width = 360,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = BrushFromHex(winBg),
                Owner = Window.GetWindow(this),
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = prompt,
                Foreground = BrushFromHex(textPrimary),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var tb = new TextBox
            {
                Text = defaultValue,
                Background = BrushFromHex(inputBg),
                Foreground = BrushFromHex(textPrimary),
                BorderBrush = BrushFromHex(border),
                CaretBrush = BrushFromHex(textPrimary),
                Padding = new Thickness(4, 2, 4, 2)
            };
            tb.SelectAll();
            sp.Children.Add(tb);

            var bp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ok = new Button
            {
                Content = "确定",
                Width = 60,
                Margin = new Thickness(0, 0, 8, 0),
                Background = BrushFromHex("#0e639c"),
                Foreground = BrushFromHex("#ffffff"),
                BorderThickness = new Thickness(0)
            };
            var cancel = new Button
            {
                Content = "取消",
                Width = 60,
                Background = BrushFromHex(btnSecondaryBg),
                Foreground = BrushFromHex(textPrimary),
                BorderThickness = new Thickness(0)
            };
            ok.Click += (_, __) => { win.DialogResult = true; };
            cancel.Click += (_, __) => { win.DialogResult = false; };
            bp.Children.Add(ok);
            bp.Children.Add(cancel);
            sp.Children.Add(bp);
            win.Content = sp;
            tb.Focus();
            return win.ShowDialog() == true ? tb.Text?.Trim() : null;
        }

        private void OnShowFileTab(object sender, RoutedEventArgs e)
        {
            _showFileTab = true;
            ApplyLeftPanelTab();
        }

        private void OnShowOutlineTab(object sender, RoutedEventArgs e)
        {
            _showFileTab = false;
            ApplyLeftPanelTab();
        }

        private void OnOutlineSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OutlineList.SelectedItem is not ListBoxItem item) return;
            if (item.Tag is not string heading || string.IsNullOrWhiteSpace(heading)) return;
            OutlineHeadingSelected?.Invoke(this, heading);
        }

        private void OnFileTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updatingFileList) return;
        }

        private void OnFileTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string path = GetSelectedTreePath();
            if (path != null && File.Exists(path))
                FileOpenRequested?.Invoke(this, path);
        }

        private void OnCtxOpen(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (path != null && File.Exists(path))
            {
                FileOpenRequested?.Invoke(this, path);
            }
            else if (path != null && Directory.Exists(path) && FileTree.SelectedItem is TreeViewItem ti)
            {
                ti.IsExpanded = !ti.IsExpanded;
            }
        }

        private void OnCtxNewFile(object sender, RoutedEventArgs e)
        {
            string dir = ResolveDir(GetSelectedTreePath());
            if (dir == null) return;
            string name = PromptInput("新建文件", "请输入文件名：", "新文档.md");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name += ".md";
            string full = Path.Combine(dir, name);
            if (File.Exists(full))
            {
                MessageBox.Show("文件已存在。", "提示");
                return;
            }

            _fileTreeService.CreateFile(dir, name);
            StatusChanged?.Invoke(this, $"已新建: {name}");
            RefreshFileList(_currentFilePath);
            FileOpenRequested?.Invoke(this, full);
        }

        private void OnCtxNewFolder(object sender, RoutedEventArgs e)
        {
            string dir = ResolveDir(GetSelectedTreePath());
            if (dir == null) return;
            string name = PromptInput("新建文件夹", "请输入文件夹名：", "新文件夹");
            if (string.IsNullOrWhiteSpace(name)) return;
            string full = Path.Combine(dir, name);
            if (Directory.Exists(full))
            {
                MessageBox.Show("文件夹已存在。", "提示");
                return;
            }
            _fileTreeService.CreateFolder(dir, name);
            StatusChanged?.Invoke(this, $"已新建文件夹: {name}");
            RefreshFileList(_currentFilePath);
        }

        private void OnCtxRename(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);
            if (!isFile && !isDir) return;
            string oldName = Path.GetFileName(path);
            string newName = PromptInput("重命名", "请输入新名称：", oldName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
            string newPath = Path.Combine(Path.GetDirectoryName(path) ?? "", newName);
            try
            {
                if (isFile)
                {
                    newPath = _fileTreeService.RenameItem(path, newName);
                    if (string.Equals(_currentFilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentFilePath = newPath;
                        CurrentFilePathChanged?.Invoke(this, new FilePathChangedEventArgs(path, newPath));
                    }
                }
                else
                {
                    _fileTreeService.RenameItem(path, newName);
                }

                StatusChanged?.Invoke(this, $"已重命名: {oldName} → {newName}");
                RefreshFileList(_currentFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重命名失败: {ex.Message}", "错误");
            }
        }

        private void OnCtxDuplicate(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (path == null || !File.Exists(path)) return;
            string copyPath = _fileTreeService.DuplicateFile(path);
            StatusChanged?.Invoke(this, $"已创建副本: {Path.GetFileName(copyPath)}");
            RefreshFileList(_currentFilePath);
        }

        private void OnCtxDelete(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);
            if (!isFile && !isDir) return;
            string name = Path.GetFileName(path);
            if (MessageBox.Show($"确定删除 \"{name}\"？\n此操作不可撤销。", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                _fileTreeService.DeletePath(path);
                if (isFile && string.Equals(_currentFilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _currentFilePath = "";
                    CurrentFileClearedRequested?.Invoke(this, EventArgs.Empty);
                }

                StatusChanged?.Invoke(this, $"已删除: {name}");
                RefreshFileList(_currentFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误");
            }
        }

        private void OnCtxCopyPath(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                Clipboard.SetText(path);
                StatusChanged?.Invoke(this, "已复制路径");
            }
        }

        private void OnCtxOpenFolder(object sender, RoutedEventArgs e)
        {
            var path = GetSelectedTreePath();
            if (string.IsNullOrWhiteSpace(path)) return;
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", dir);
        }
    }
}
