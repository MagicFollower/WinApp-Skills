using System;
using System.Collections.Generic;
using VocabDesk.Services;

namespace VocabDesk.Models
{
    /// <summary>
    /// 一本书的静态信息。词书即标签：一个词头可以同时属于多本书，不合并。
    /// </summary>
    public sealed class BookInfo
    {
        public BookInfo(int index, string key, string label, string fileName)
        {
            Index = index;
            Key = key;
            Label = label;
            FileName = fileName;
        }

        /// <summary>0..6，同时用作位图位。</summary>
        public int Index { get; private set; }
        /// <summary>稳定键，导航项 Tag 与自检都认它，不认显示名。</summary>
        public string Key { get; private set; }
        /// <summary>显示名，如「CET4」。</summary>
        public string Label { get; private set; }
        /// <summary>来源文件名，排障时要知道哪一本出了问题。</summary>
        public string FileName { get; private set; }
        /// <summary>该书收录的词头数，加载完成后回填。</summary>
        public int WordCount { get; set; }
    }

    /// <summary>
    /// 一份释义来源：同一个词在同一本书里可能有不止一份，且归一后仍不同（实测 20,213 条书内重复
    /// 归一只塌掉 5.9%），所以变体一律原样保留，绝不做有损合并。
    /// </summary>
    public sealed class VariantGroup
    {
        public string BookLabel { get; set; }
        /// <summary>来源标记由首条释义的前导空格风格推断，只是标签，不参与检索。</summary>
        public string SourceLabel { get; set; }
        public IReadOnlyList<string> Translations { get; set; }
        public IReadOnlyList<string> Phrases { get; set; }
        /// <summary>词性原始串集合，脏值原样留着由界面降级显示。</summary>
        public IReadOnlyList<string> PosRaw { get; set; }

        public string Header
        {
            get { return BookLabel + " · " + SourceLabel + " · " + Phrases.Count + " 短语"; }
        }
    }

    /// <summary>
    /// 卡片：一个词头 + 它的全部变体组。变体组在用户点开时才建（实测建一组 0.001–0.014 ms，
    /// 预建全部 14,625 张要 130 ms + 48 MB，所以这里坚持懒建）。
    /// </summary>
    public sealed class WordCard
    {
        internal Corpus Corpus;
        internal int WordId;

        public string Word { get; set; }
        /// <summary>所属全部书名，空格分隔——词书就是标签。</summary>
        public string BookBadges { get; set; }
        /// <summary>列表里的常驻摘要行，折叠态只显示这一句。</summary>
        public string Summary { get; set; }
        public int VariantCount { get; set; }
        /// <summary>收录这个词头的词书本数（词书=标签，一个词头可在多本里）。</summary>
        public int BookCount { get; set; }
        /// <summary>短语总数，摘要行用。</summary>
        public int PhraseCount { get; set; }

        /// <summary>页脚那行"这个词的收录情况"，用读数而不是耗时——调试信息不进界面。</summary>
        public string InclusionText
        {
            get
            {
                return Word + " · " + BookCount + " 本词书收录 · "
                       + VariantCount + " 处释义来源 · " + PhraseCount + " 个短语";
            }
        }

        IReadOnlyList<VariantGroup> detail;
        public IReadOnlyList<VariantGroup> Detail
        {
            get
            {
                if (detail == null) detail = Corpus.BuildVariants(WordId);
                return detail;
            }
        }

        public override string ToString() { return Word; }
    }
}
