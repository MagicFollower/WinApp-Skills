using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace StartUI4Controls
{
    /// <summary>
    /// 局部/每窗口主题作用域。在任意 <see cref="FrameworkElement"/>（通常是 Window、UserControl 或一张卡片）上
    /// 设置 <c>ui:UI4ThemeScope.Theme="dark"</c>，其整棵子树改用该主题，与全局 <see cref="UI4Theme"/> 互不干扰：
    /// <code>
    /// &lt;Border ui:UI4ThemeScope.Theme="dark"&gt; ... &lt;/Border&gt;
    /// </code>
    /// 生效通道只有一条：向元素自身 <see cref="FrameworkElement.Resources"/> 的 MergedDictionaries 末位插入
    /// 该主题的令牌字典（<c>UI4.Color.X</c> / <c>UI4.Brush.X</c>，与全局共用同一实例）。
    /// 因此宿主 <c>{DynamicResource}</c> 与构造函数里 <c>SetResourceReference</c> 的控件
    /// （全库 26 个文件 / 89 处引用）都自动跟随，控件本身不需要实现任何主题接口。
    /// 置空或未知键 = 撤销作用域，子树回到全局主题。
    /// </summary>
    public static class UI4ThemeScope
    {
        /// <summary>作用域主题键（<c>light</c> / <c>dark</c> / <c>highcontrast</c> / 自定义注册键），大小写不敏感。</summary>
        public static readonly DependencyProperty ThemeProperty = DependencyProperty.RegisterAttached(
            "Theme", typeof(string), typeof(UI4ThemeScope),
            new FrameworkPropertyMetadata(null, OnThemePropertyChanged));

        /// <summary>设置元素的主题作用域；<see cref="string.Empty"/> 或 null 撤销作用域。</summary>
        public static void SetTheme(DependencyObject element, string value)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            element.SetValue(ThemeProperty, value);
        }

        /// <summary>读取元素自身声明的主题作用域键（不含祖先的作用域）。</summary>
        public static string GetTheme(DependencyObject element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return (string)element.GetValue(ThemeProperty);
        }

        private sealed class ScopeEntry
        {
            public readonly WeakReference<FrameworkElement> Element;
            public readonly string Key;
            public readonly ResourceDictionary Dictionary;

            public ScopeEntry(FrameworkElement element, string key, ResourceDictionary dictionary)
            {
                Element = new WeakReference<FrameworkElement>(element);
                Key = key;
                Dictionary = dictionary;
            }
        }

        private static readonly List<ScopeEntry> _scopes = new List<ScopeEntry>();

        private static void OnThemePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FrameworkElement element = d as FrameworkElement;
            if (element == null) return;

            // 空串 = 撤销作用域；未知键同样按撤销处理：XAML 里写错键名不应让宿主崩溃。
            string key = (e.NewValue as string ?? string.Empty).Trim();
            if (key.Length == 0 || !UI4Theme.IsRegistered(key)) key = null;

            ScopeEntry existing = Find(element);
            if (existing != null)
            {
                element.Resources.MergedDictionaries.Remove(existing.Dictionary);
                _scopes.Remove(existing);
            }

            if (key == null)
            {
                return;
            }

            // 与全局共用同一份共享字典实例：SetAccent 原地改一次，全局与本作用域同时跟随
            ResourceDictionary dictionary = UI4Theme.SharedResourcesFor(key);
            element.Resources.MergedDictionaries.Add(dictionary);
            _scopes.Add(new ScopeEntry(element, key, dictionary));

            // 作用域根若是整窗，标题栏（非客户区，DWM 绘制）也要跟着换；
            // 子树内的控件无需任何刷新——颜色全部走资源引用
            Window scopeWindow = element as Window;
            if (scopeWindow != null) UI4WindowTitleBar.Apply(scopeWindow);
        }

        /// <summary>
        /// 把某个键下所有作用域仍挂着的旧令牌字典就地换成新实例。
        /// <see cref="UI4Theme.Register"/> 覆盖一个已用过的键时会新建字典、作废旧实例，
        /// 若不同步这里，那些作用域会一直显示旧主题（撤销作用域时还会去摘一个已经不在树里的字典）。
        /// </summary>
        internal static void ReplaceSharedDictionary(string key, ResourceDictionary replacement)
        {
            for (int i = 0; i < _scopes.Count; i++)
            {
                ScopeEntry entry = _scopes[i];
                FrameworkElement element;
                if (!entry.Element.TryGetTarget(out element)) continue;
                if (!string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
                if (ReferenceEquals(entry.Dictionary, replacement)) continue;

                int index = element.Resources.MergedDictionaries.IndexOf(entry.Dictionary);
                if (index < 0) continue;
                element.Resources.MergedDictionaries[index] = replacement;
                _scopes[i] = new ScopeEntry(element, entry.Key, replacement);

                // 作用域根是整窗时，标题栏（DWM 绘制，够不着资源引用）跟着重染
                Window scopeWindow = element as Window;
                if (scopeWindow != null) UI4WindowTitleBar.Apply(scopeWindow);
            }
        }

        /// <summary>
        /// 若有存活的作用域正用着这个键，就请 <see cref="UI4Theme"/> 重建该键的字典并就地重指向。
        /// <see cref="UI4Theme.Register"/> 覆盖一个<b>不是当前生效键</b>的定义时走这里——
        /// 那种情况下不会有 apply 流程来触发重建，作用域就会一直挂着被作废的旧字典。
        /// </summary>
        internal static void RebindIfScoped(string key)
        {
            for (int i = 0; i < _scopes.Count; i++)
            {
                FrameworkElement element;
                if (!_scopes[i].Element.TryGetTarget(out element)) continue;
                if (!string.Equals(_scopes[i].Key, key, StringComparison.OrdinalIgnoreCase)) continue;
                UI4Theme.SharedResourcesFor(key);
                return;
            }
        }

        private static ScopeEntry Find(FrameworkElement element)
        {
            for (int i = 0; i < _scopes.Count; i++)
            {
                FrameworkElement target;
                if (_scopes[i].Element.TryGetTarget(out target) && ReferenceEquals(target, element))
                    return _scopes[i];
            }
            return null;
        }

        /// <summary>
        /// 从元素向上找最近的作用域键（<see cref="UI4Theme"/> 解析控件有效主题时调用）。
        /// 先走逻辑/模板父链（可穿过 Popup 与控件模板），再退回视觉父链。
        /// </summary>
        internal static string ResolveKey(DependencyObject element)
        {
            for (DependencyObject current = element; current != null; current = NextAncestor(current))
            {
                string key = current.GetValue(ThemeProperty) as string;
                if (!string.IsNullOrEmpty(key) && UI4Theme.IsRegistered(key)) return key;
            }
            return null;
        }

        private static DependencyObject NextAncestor(DependencyObject d)
        {
            FrameworkElement fe = d as FrameworkElement;
            if (fe != null && fe.Parent != null) return fe.Parent;
            FrameworkContentElement fce = d as FrameworkContentElement;
            if (fce != null && fce.Parent != null) return fce.Parent;
            return VisualTreeHelper.GetParent(d);
        }

    }
}
