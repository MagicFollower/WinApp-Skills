using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;

namespace StartUI4Controls.Internal
{
    /// <summary>
    /// 文本控件复制 / 剪切 / 粘贴的原生剪贴板接管实现，供库内可编辑控件复用。
    /// </summary>
    /// <remarks>
    /// 控件默认的 Copy/Cut/Paste 走 WPF 的 OLE 剪贴板通道；剪贴板是全局单锁资源，有程序监听
    /// 剪贴板时该通道会在调用线程上反复抢锁，表现为按键后卡顿秒级。这里在命令隧道阶段接管，
    /// 改用 <see cref="UI4Clipboard"/>：UI 线程立即完成选区删除或插入，写入放到后台线程重试。
    /// </remarks>
    static class ClipboardCommandTakeover
    {
        /// <summary>
        /// 在按键隧道阶段处理 Ctrl+C / Ctrl+X / Ctrl+V，返回是否已接管。
        /// 控件自带的框架级处理（WPF OLE 剪贴板通道）因此不会被触发；已自行接管 Ctrl+X 的控件可直接调用本方法。
        /// </summary>
        public static bool TryHandleKey(TextBox box, KeyEventArgs e)
        {
            if (e.Handled || e.KeyboardDevice.Modifiers != ModifierKeys.Control) return false;

            if (e.Key == Key.C)
            {
                if (box.SelectionLength <= 0) return false;
                Copy(box);
            }
            else if (e.Key == Key.X)
            {
                if (box.IsReadOnly || box.SelectionLength <= 0) return false;
                Cut(box);
            }
            else if (e.Key == Key.V)
            {
                if (box.IsReadOnly || !UI4Clipboard.ContainsText()) return false;
                Paste(box);
            }
            else
            {
                return false;
            }

            return true;
        }

        public static void Install(TextBox box)
        {
            // 按键隧道阶段先于控件内部的剪贴板处理，框架自带的 Copy/Cut/Paste 处理因此不会被触发。
            box.PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (TryHandleKey(box, e)) e.Handled = true;
            };

            box.CommandBindings.Add(MakeBinding(ApplicationCommands.Copy,
                () => box.SelectionLength > 0,
                () => Copy(box)));
            box.CommandBindings.Add(MakeBinding(ApplicationCommands.Cut,
                () => !box.IsReadOnly && box.SelectionLength > 0,
                () => Cut(box)));
            box.CommandBindings.Add(MakeBinding(ApplicationCommands.Paste,
                () => !box.IsReadOnly && UI4Clipboard.ContainsText(),
                () => Paste(box)));
        }

        public static void Install(TextEditor editor)
        {
            var area = editor.TextArea;
            if (area == null) return;

            area.PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Handled || e.KeyboardDevice.Modifiers != ModifierKeys.Control) return;

                if (e.Key == Key.C)
                {
                    if (editor.SelectionLength <= 0) return;
                    Copy(editor);
                }
                else if (e.Key == Key.X)
                {
                    if (editor.IsReadOnly || editor.SelectionLength <= 0) return;
                    Cut(editor);
                }
                else if (e.Key == Key.V)
                {
                    if (editor.IsReadOnly || !UI4Clipboard.ContainsText()) return;
                    Paste(editor);
                }
                else
                {
                    return;
                }

                e.Handled = true;
            };

            area.CommandBindings.Add(MakeBinding(ApplicationCommands.Copy,
                () => editor.SelectionLength > 0,
                () => Copy(editor)));
            area.CommandBindings.Add(MakeBinding(ApplicationCommands.Cut,
                () => !editor.IsReadOnly && editor.SelectionLength > 0,
                () => Cut(editor)));
            area.CommandBindings.Add(MakeBinding(ApplicationCommands.Paste,
                () => !editor.IsReadOnly && UI4Clipboard.ContainsText(),
                () => Paste(editor)));
        }

        public static void Copy(TextBox box)
        {
            UI4Clipboard.TrySetTextAsync(box.SelectedText, OnWriteFailed);
        }

        public static void Cut(TextBox box)
        {
            if (box.IsReadOnly || box.SelectionLength <= 0) return;

            UI4Clipboard.TrySetTextAsync(box.SelectedText, OnWriteFailed);
            box.SelectedText = string.Empty;
        }

        public static void Paste(TextBox box)
        {
            if (box.IsReadOnly) return;

            UI4Clipboard.TryGetTextAsync(delegate(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                InvokeOnUi(box, delegate { box.SelectedText = text; });
            });
        }

        public static void Copy(TextEditor editor)
        {
            UI4Clipboard.TrySetTextAsync(editor.SelectedText, OnWriteFailed);
        }

        public static void Cut(TextEditor editor)
        {
            if (editor.IsReadOnly || editor.SelectionLength <= 0) return;

            UI4Clipboard.TrySetTextAsync(editor.SelectedText, OnWriteFailed);
            editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, string.Empty);
        }

        public static void Paste(TextEditor editor)
        {
            if (editor.IsReadOnly) return;

            UI4Clipboard.TryGetTextAsync(delegate(string text)
            {
                if (string.IsNullOrEmpty(text)) return;

                InvokeOnUi(editor, delegate
                {
                    int offset = editor.SelectionStart;
                    int length = editor.SelectionLength;
                    editor.Document.Replace(offset, length, text);
                    editor.CaretOffset = offset + text.Length;
                });
            });
        }

        private static CommandBinding MakeBinding(RoutedCommand command, Func<bool> canRun, Action run)
        {
            var binding = new CommandBinding(command);

            binding.PreviewCanExecute += delegate(object s, CanExecuteRoutedEventArgs e)
            {
                e.CanExecute = canRun();
                e.Handled = true;
            };
            binding.PreviewExecuted += delegate(object s, ExecutedRoutedEventArgs e)
            {
                if (!canRun()) return;
                run();
                e.Handled = true;
            };

            return binding;
        }

        private static void InvokeOnUi(DispatcherObject target, Action action)
        {
            if (target.Dispatcher.CheckAccess()) action();
            else target.Dispatcher.BeginInvoke(action);
        }

        private static void OnWriteFailed(bool ok)
        {
            if (ok) return;
            UI4MessageBox.Show(
                "无法写入剪贴板，请关闭占用剪贴板的程序后重试。",
                "提示", UI4MessageBoxButtons.OK, 360);        }
    }
}
