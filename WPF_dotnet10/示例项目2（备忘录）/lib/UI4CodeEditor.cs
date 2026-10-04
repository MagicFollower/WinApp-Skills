using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的代码编辑器控件，基于 AvalonEdit，支持语法高亮和自定义滚动条样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="ICSharpCode.AvalonEdit.TextEditor"/>，默认启用 C# 语法高亮、
    /// 行号显示和自动换行。</para>
    /// </remarks>
    public class UI4CodeEditor : TextEditor
    {
        private static readonly Style ScrollViewerStyle;

        static UI4CodeEditor()
        {
            ScrollViewerStyle = ScrollBarResources.GetScrollViewerStyle();
        }

        public UI4CodeEditor()
        {
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            ShowLineNumbers = true;
            WordWrap = true;
            FontFamily = new FontFamily("Consolas");
            SetResourceReference(FontSizeProperty, "UI4.Font.Size.Code");
            Name = "codeeditor_firstreference";
            Options = new TextEditorOptions
            {
                ConvertTabsToSpaces = true,
                IndentationSize = 4,
                EnableRectangularSelection = false,

            };

            var editor = this;
            ClipboardCommandTakeover.Install(editor);

            var menu = new UI4ContextMenu
            {
                Width = 200
            };

            menu.AddItem(UI4MenuItemType.Undo, () => editor.Undo(), () => editor.CanUndo);
            menu.AddItem(UI4MenuItemType.Redo, () => editor.Redo(), () => editor.CanRedo);
            menu.AddItem(UI4MenuItemType.Cut, () => ClipboardCommandTakeover.Cut(editor), () => !string.IsNullOrEmpty(editor.SelectedText));
            menu.AddItem(UI4MenuItemType.Copy, () => ClipboardCommandTakeover.Copy(editor), () => !string.IsNullOrEmpty(editor.SelectedText));
            menu.AddItem(UI4MenuItemType.Paste, () => ClipboardCommandTakeover.Paste(editor), () => UI4Clipboard.ContainsText());
            menu.AddItem(UI4MenuItemType.Delete, () => editor.SelectedText = "", () => !string.IsNullOrEmpty(editor.SelectedText));
            menu.AddItem(UI4MenuItemType.SelectAll, () => editor.SelectAll(), () => editor.Text.Length > 0);

            menu.Attach(this);

            // 编辑器外壳（底 / 前景）跟随主题。语法高亮配色由 AvalonEdit 的 XSHD 决定，
            // 不走 WPF 资源体系，无法声明式化——属已知限制，见 README 第十节。
            SetResourceReference(BackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(ForegroundProperty, "UI4.Brush.TextForeground");

            // 控件加载完成后为内部的 ScrollViewer 应用自定义样式
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(this);
            if (scrollViewer != null && ScrollViewerStyle != null)
            {
                scrollViewer.Style = ScrollViewerStyle;
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

    }
}