using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using MemoTask.Helpers;
using MemoTask.Models;
using MemoTask.Services;
using StartUI4Controls;

namespace MemoTask.ViewModels
{
    /// <summary>强调色预设的小方块。Key 是 #RRGGBB，也是点下去要写进设置的值；色块本身不跟主题走。</summary>
    internal sealed class AccentSwatch
    {
        public AccentSwatch(string hex)
        {
            Key = hex;
            Color color;
            if (!Theme.TryParseHex(hex, out color)) color = Theme.Accent;
            Color = color;
        }

        public string Key { get; private set; }

        public Color Color { get; private set; }
    }

    /// <summary>
    /// 设置页：应用信息 + 外观 + 行为 + 数据。
    /// 每一项改动都立刻写 settings.json，不做「保存设置」按钮——单机小工具的预期是改完就生效。
    /// </summary>
    internal sealed class SettingsViewModel : ViewModelBase
    {
        private readonly DataStore _store;
        private readonly SettingsStore _settings;
        private readonly Action _flushEverything;
        private readonly Action _reloadEverything;

        private string _themeKey;
        private string _startPageKey;
        private string _autoSaveKey;
        private string _defaultPriorityKey;
        private string _renderModeKey;
        private string _statusText = "";
        private Brush _accentBrush = Theme.AccentBrush;
        private string _accentHex = "";
        private bool _isCustomAccent;

        public SettingsViewModel(DataStore store, SettingsStore settings, Action flushEverything, Action reloadEverything)
        {
            _store = store;
            _settings = settings;
            _flushEverything = flushEverything;
            _reloadEverything = reloadEverything;

            ThemeKey = settings.Current.ThemeMode;
            StartPageKey = settings.Current.StartPage;
            AutoSaveKey = settings.Current.AutoSaveMs.ToString();
            DefaultPriorityKey = settings.Current.DefaultPriority;
            _renderModeKey = string.IsNullOrEmpty(settings.Current.RenderMode) ? "auto" : settings.Current.RenderMode;

            PickAccentCommand = new RelayCommand(PickAccent);
            ResetAccentCommand = new RelayCommand(ResetAccent);
            SetAccentCommand = new RelayCommand<string>(ApplyAccentHex);
            OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
            ExportBackupCommand = new RelayCommand(ExportBackup);
            ClearDataCommand = new RelayCommand(ClearData);
            RefreshInfoCommand = new RelayCommand(RefreshInfo);

            // 主题切换后库会广播；这里不绑静态属性（WPF 绑静态 CLR 属性找的是同名 <Prop>Changed 事件，
            // 库发的是 StaticPropertyChanged，绑上去会停在初值），改成订阅后手动重发属性通知。
            UI4Theme.ThemeChanged += delegate
            {
                RaisePropertyChanged("ThemeLabel");
                RaisePropertyChanged("ResolvedModeLabel");
                UpdateAccentDisplay();
            };
        }

        public IList<OptionItem> ThemeOptions { get; } = new List<OptionItem>
        {
            new OptionItem("system", "跟随系统"),
            new OptionItem("light", "浅色"),
            new OptionItem("dark", "深色"),
            new OptionItem("highcontrast", "高对比度"),
        };

        public IList<OptionItem> StartPageOptions { get; } = new List<OptionItem>
        {
            new OptionItem("notes", "备忘录"),
            new OptionItem("todos", "待办"),
            new OptionItem("last", "上次浏览的页面"),
        };

        public IList<OptionItem> AutoSaveOptions { get; } = new List<OptionItem>
        {
            new OptionItem("0", "关闭（只认 Ctrl+S）"),
            new OptionItem("500", "停顿 0.5 秒后"),
            new OptionItem("800", "停顿 0.8 秒后"),
            new OptionItem("1500", "停顿 1.5 秒后"),
            new OptionItem("3000", "停顿 3 秒后"),
        };

        public IList<OptionItem> PriorityOptions { get; } = new List<OptionItem>
        {
            new OptionItem("Low", "低"),
            new OptionItem("Normal", "普通"),
            new OptionItem("High", "高"),
        };

        public IList<OptionItem> RenderModeOptions { get; } = new List<OptionItem>
        {
            new OptionItem("auto", "自动（硬件加速）"),
            new OptionItem("software", "软件渲染"),
        };

        /// <summary>渲染档位只在建窗口前生效，所以改完要重启；这里不假装立刻生效。</summary>
        public string RenderModeKey
        {
            get { return _renderModeKey; }
            set
            {
                if (value == null) return;
                if (!SetProperty(ref _renderModeKey, value)) return;
                _settings.Current.RenderMode = value;
                _settings.Save();
                StatusText = "渲染档位已设为「" + LabelOf(RenderModeOptions, value) + "」，重启应用后生效";
            }
        }

        public IList<AccentSwatch> AccentSwatches { get; } = BuildSwatches();

        public RelayCommand PickAccentCommand { get; private set; }
        public RelayCommand ResetAccentCommand { get; private set; }
        public RelayCommand<string> SetAccentCommand { get; private set; }
        public RelayCommand OpenDataFolderCommand { get; private set; }
        public RelayCommand ExportBackupCommand { get; private set; }
        public RelayCommand ClearDataCommand { get; private set; }
        public RelayCommand RefreshInfoCommand { get; private set; }

        public string ThemeKey
        {
            get { return _themeKey; }
            set
            {
                if (value == null) return;
                if (!SetProperty(ref _themeKey, value)) return;
                _settings.Current.ThemeMode = value;
                _settings.Save();
                ThemeService.Apply(value, _settings.Current.Accent);
                StatusText = "已切换到" + LabelOf(ThemeOptions, value);
            }
        }

        public string StartPageKey
        {
            get { return _startPageKey; }
            set
            {
                if (value == null) return;
                if (!SetProperty(ref _startPageKey, value)) return;
                _settings.Current.StartPage = value;
                _settings.Save();
            }
        }

        public string AutoSaveKey
        {
            get { return _autoSaveKey; }
            set
            {
                if (value == null) return;
                int ms;
                if (!int.TryParse(value, out ms)) return;
                if (!SetProperty(ref _autoSaveKey, value)) return;
                _settings.Current.AutoSaveMs = ms;
                _settings.Save();
            }
        }

        public string DefaultPriorityKey
        {
            get { return _defaultPriorityKey; }
            set
            {
                if (value == null) return;
                if (!SetProperty(ref _defaultPriorityKey, value)) return;
                _settings.Current.DefaultPriority = value;
                _settings.Save();
            }
        }

        public Brush AccentBrush
        {
            get { return _accentBrush; }
            private set { SetProperty(ref _accentBrush, value); }
        }

        public string AccentHex
        {
            get { return _accentHex; }
            private set { SetProperty(ref _accentHex, value ?? ""); }
        }

        public bool IsCustomAccent
        {
            get { return _isCustomAccent; }
            private set { SetProperty(ref _isCustomAccent, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value ?? ""); }
        }

        public string ThemeLabel { get { return ThemeService.ResolvedDescription; } }

        public string ResolvedModeLabel { get { return "请求模式：" + ModeName(UI4Theme.CurrentMode); } }

        public string AppName { get { return AppInfo.DisplayName + "（" + AppInfo.AppName + "）"; } }

        public string AppDescription { get { return AppInfo.Description; } }

        public string VersionText { get { return "MemoTask v" + AppInfo.Version; } }

        public string ComponentText { get { return "StartUI4Controls v" + AppInfo.ComponentVersion; } }

        public string FrameworkText { get { return AppInfo.Framework; } }

        public string OsText { get { return AppInfo.OsCaption + " · " + AppInfo.ProcessArchitecture; } }

        public string RuntimeModeText { get { return AppInfo.RuntimeMode; } }

        public string DataDirText { get { return AppPaths.DataDir; } }

        public string StartedText { get { return AppInfo.StartedLabel; } }

        public string UpTimeText { get { return "已运行 " + AppInfo.UpTime; } }

        public string StatsText
        {
            get
            {
                long bytes = _store.DataSizeBytes();
                return _store.Notes.Count + " 条备忘 · " + _store.Todos.Count + " 条待办 · 数据占用 " + SizeText(bytes);
            }
        }

        /// <summary>进入设置页时刷新会随时间变的几项。</summary>
        public void RefreshInfo()
        {
            RaisePropertyChanged("ThemeLabel");
            RaisePropertyChanged("ResolvedModeLabel");
            RaisePropertyChanged("UpTimeText");
            RaisePropertyChanged("StatsText");
            RaisePropertyChanged("VersionText");
            RaisePropertyChanged("FrameworkText");
            RaisePropertyChanged("OsText");
            RaisePropertyChanged("RuntimeModeText");
            RaisePropertyChanged("ComponentText");
            RaisePropertyChanged("DataDirText");
            UpdateAccentDisplay();
        }

        private void PickAccent()
        {
            Color? picked = UI4ColorPicker.ShowDialog(
                "选择强调色",
                UI4Theme.Current.ColorOf(UI4ThemeToken.Accent),
                System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow);
            if (picked.HasValue) CommitAccent(Theme.ToHex(picked.Value));
        }

        private void ApplyAccentHex(string hex)
        {
            Color ignored;
            if (!Theme.TryParseHex(hex, out ignored)) return;
            CommitAccent(hex);
        }

        private void ResetAccent()
        {
            CommitAccent("");
            StatusText = "强调色已恢复为当前主题自带色";
        }

        private void CommitAccent(string hex)
        {
            _settings.Current.Accent = hex ?? "";
            _settings.Save();
            ThemeService.ApplyAccent(_settings.Current.Accent);
            UpdateAccentDisplay();
            if (!string.IsNullOrEmpty(hex)) StatusText = "强调色已改为 " + hex;
        }

        private void UpdateAccentDisplay()
        {
            Color accent = ThemeService.CurrentAccent;
            var brush = new SolidColorBrush(accent);
            brush.Freeze();
            AccentBrush = brush;
            AccentHex = Theme.ToHex(accent);
            IsCustomAccent = !string.IsNullOrEmpty(_settings.Current.Accent);
        }

        private void OpenDataFolder()
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = AppPaths.DataDir, UseShellExecute = true });
                StatusText = "已在资源管理器里打开数据目录";
            }
            catch (Exception ex)
            {
                StatusText = "打不开数据目录：" + ex.Message;
            }
        }

        private void ExportBackup()
        {
            _flushEverything();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出备份",
                Filter = "MemoTask 备份（*.json）|*.json",
                FileName = "MemoTask-备份-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json",
            };
            bool? ok = dialog.ShowDialog();
            if (ok != true) return;

            var bundle = new BackupBundle
            {
                Notes = new List<NoteItem>(_store.Notes),
                Todos = new List<TodoItem>(_store.Todos),
            };
            try
            {
                System.IO.File.WriteAllText(dialog.FileName, JsonStore.ToJson(bundle), System.Text.Encoding.UTF8);
                StatusText = "已导出到 " + dialog.FileName;
            }
            catch (Exception ex)
            {
                StatusText = "导出失败：" + ex.Message;
            }
        }

        private void ClearData()
        {
            int notes = _store.Notes.Count;
            int todos = _store.Todos.Count;
            if (notes == 0 && todos == 0)
            {
                StatusText = "本来就是空的，没什么可清";
                return;
            }
            bool yes = UI4MessageBox.Show(
                "清空全部数据？" + notes + " 条备忘与 " + todos + " 条待办会从本机移除，无法撤销。建议先点「导出备份」。",
                "清空数据",
                UI4MessageBoxButtons.OKCancel,
                owner: System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow) == true;
            if (!yes) return;

            _store.Notes.Clear();
            _store.Todos.Clear();
            _store.SaveAll();
            _reloadEverything();
            RefreshInfo();
            StatusText = "数据已清空";
        }

        private static string LabelOf(IList<OptionItem> options, string key)
        {
            foreach (OptionItem item in options)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item.Label;
            }
            return key;
        }

        private static string ModeName(UI4ThemeMode mode)
        {
            switch (mode)
            {
                case UI4ThemeMode.Light: return "浅色";
                case UI4ThemeMode.Dark: return "深色";
                case UI4ThemeMode.HighContrast: return "高对比度";
                default: return "跟随系统";
            }
        }

        private static string SizeText(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.00") + " MB";
        }

        private static IList<AccentSwatch> BuildSwatches()
        {
            var list = new List<AccentSwatch>();
            foreach (Color c in Theme.AccentPresets) list.Add(new AccentSwatch(Theme.ToHex(c)));
            return list;
        }
    }
}
