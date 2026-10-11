using System;
using System.Collections.Generic;
using System.Globalization;

namespace StartUI4Controls
{
    /// <summary>
    /// 多语言键枚举，用于标识多语言字符串。
    /// </summary>
    public enum UI4LanguageKey
    {
        OK,
        Cancel,
        Notice,
        ColorPicker,
        Undo,
        Redo,
        Cut,
        Copy,
        Paste,
        Delete,
        SelectAll
    }

    /// <summary>
    /// 多语言支持类，提供组件库内部使用的多语言字符串。
    /// </summary>
    /// <remarks>
    /// <para>通过 <see cref="SetLanguage(string)"/> 设置当前语言，
    /// 使用 <see cref="Get(UI4LanguageKey)"/> 获取翻译字符串。</para>
    /// <para>支持的语言包括：中文简体、中文繁体、英语、日语、韩语、法语、德语、俄语。</para>
    /// </remarks>
    public static class UI4MultiLanguage
    {
        private static Dictionary<UI4LanguageKey, string> _current;

        public static Dictionary<UI4LanguageKey, string> Current
        {
            get
            {
                if (_current == null)
                    _current = GetStrings(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                return _current;
            }
        }

        public static string Get(UI4LanguageKey key)
        {
            return Current.TryGetValue(key, out string value) ? value : key.ToString();
        }

        public static void Refresh()
        {
            _current = null;
        }

        public static Dictionary<UI4LanguageKey, string> GetStrings(string lang)
        {
            switch (lang.ToLower())
            {
                case "zh":
                    return GetChineseStrings();
                case "ja":
                    return GetJapaneseStrings();
                case "ko":
                    return GetKoreanStrings();
                case "de":
                    return GetGermanStrings();
                case "fr":
                    return GetFrenchStrings();
                case "es":
                    return GetSpanishStrings();
                case "ru":
                    return GetRussianStrings();
                default:
                    return GetEnglishStrings();
            }
        }

        private static Dictionary<UI4LanguageKey, string> GetEnglishStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "OK" },
                { UI4LanguageKey.Cancel, "Cancel" },
                { UI4LanguageKey.Notice, "Notice" },
                { UI4LanguageKey.ColorPicker, "Color Picker" },
                { UI4LanguageKey.Undo, "Undo" },
                { UI4LanguageKey.Redo, "Redo" },
                { UI4LanguageKey.Cut, "Cut" },
                { UI4LanguageKey.Copy, "Copy" },
                { UI4LanguageKey.Paste, "Paste" },
                { UI4LanguageKey.Delete, "Delete" },
                { UI4LanguageKey.SelectAll, "Select All" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetChineseStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "确定" },
                { UI4LanguageKey.Cancel, "取消" },
                { UI4LanguageKey.Notice, "提示" },
                { UI4LanguageKey.ColorPicker, "颜色选择器" },
                { UI4LanguageKey.Undo, "撤销" },
                { UI4LanguageKey.Redo, "重做" },
                { UI4LanguageKey.Cut, "剪切" },
                { UI4LanguageKey.Copy, "复制" },
                { UI4LanguageKey.Paste, "粘贴" },
                { UI4LanguageKey.Delete, "删除" },
                { UI4LanguageKey.SelectAll, "全选" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetJapaneseStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "OK" },
                { UI4LanguageKey.Cancel, "キャンセル" },
                { UI4LanguageKey.Notice, "通知" },
                { UI4LanguageKey.ColorPicker, "カラーピッカー" },
                { UI4LanguageKey.Undo, "元に戻す" },
                { UI4LanguageKey.Redo, "やり直し" },
                { UI4LanguageKey.Cut, "切り取り" },
                { UI4LanguageKey.Copy, "コピー" },
                { UI4LanguageKey.Paste, "貼り付け" },
                { UI4LanguageKey.Delete, "削除" },
                { UI4LanguageKey.SelectAll, "すべて選択" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetKoreanStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "확인" },
                { UI4LanguageKey.Cancel, "취소" },
                { UI4LanguageKey.Notice, "알림" },
                { UI4LanguageKey.ColorPicker, "색상 선택기" },
                { UI4LanguageKey.Undo, "실행 취소" },
                { UI4LanguageKey.Redo, "다시 실행" },
                { UI4LanguageKey.Cut, "잘라내기" },
                { UI4LanguageKey.Copy, "복사" },
                { UI4LanguageKey.Paste, "붙여넣기" },
                { UI4LanguageKey.Delete, "삭제" },
                { UI4LanguageKey.SelectAll, "모두 선택" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetGermanStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "OK" },
                { UI4LanguageKey.Cancel, "Abbrechen" },
                { UI4LanguageKey.Notice, "Hinweis" },
                { UI4LanguageKey.ColorPicker, "Farbauswahl" },
                { UI4LanguageKey.Undo, "Rückgängig" },
                { UI4LanguageKey.Redo, "Wiederholen" },
                { UI4LanguageKey.Cut, "Ausschneiden" },
                { UI4LanguageKey.Copy, "Kopieren" },
                { UI4LanguageKey.Paste, "Einfügen" },
                { UI4LanguageKey.Delete, "Löschen" },
                { UI4LanguageKey.SelectAll, "Alle auswählen" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetFrenchStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "OK" },
                { UI4LanguageKey.Cancel, "Annuler" },
                { UI4LanguageKey.Notice, "Avis" },
                { UI4LanguageKey.ColorPicker, "Sélecteur de couleur" },
                { UI4LanguageKey.Undo, "Annuler" },
                { UI4LanguageKey.Redo, "Rétablir" },
                { UI4LanguageKey.Cut, "Couper" },
                { UI4LanguageKey.Copy, "Copier" },
                { UI4LanguageKey.Paste, "Coller" },
                { UI4LanguageKey.Delete, "Supprimer" },
                { UI4LanguageKey.SelectAll, "Sélectionner tout" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetSpanishStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "Aceptar" },
                { UI4LanguageKey.Cancel, "Cancelar" },
                { UI4LanguageKey.Notice, "Aviso" },
                { UI4LanguageKey.ColorPicker, "Selector de color" },
                { UI4LanguageKey.Undo, "Deshacer" },
                { UI4LanguageKey.Redo, "Rehacer" },
                { UI4LanguageKey.Cut, "Cortar" },
                { UI4LanguageKey.Copy, "Copiar" },
                { UI4LanguageKey.Paste, "Pegar" },
                { UI4LanguageKey.Delete, "Eliminar" },
                { UI4LanguageKey.SelectAll, "Seleccionar todo" }
            };
        }

        private static Dictionary<UI4LanguageKey, string> GetRussianStrings()
        {
            return new Dictionary<UI4LanguageKey, string>
            {
                { UI4LanguageKey.OK, "OK" },
                { UI4LanguageKey.Cancel, "Отмена" },
                { UI4LanguageKey.Notice, "Уведомление" },
                { UI4LanguageKey.ColorPicker, "Выбор цвета" },
                { UI4LanguageKey.Undo, "Отменить" },
                { UI4LanguageKey.Redo, "Повторить" },
                { UI4LanguageKey.Cut, "Вырезать" },
                { UI4LanguageKey.Copy, "Копировать" },
                { UI4LanguageKey.Paste, "Вставить" },
                { UI4LanguageKey.Delete, "Удалить" },
                { UI4LanguageKey.SelectAll, "Выделить всё" }
            };
        }
    }
}