using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VocabDesk.Models;

namespace VocabDesk.Services
{
    /// <summary>检索模式。每一个模式都对应一套已实测过的索引结构，不是装饰性选项。</summary>
    public enum SearchMode
    {
        Prefix,     // 词头前缀（排序数组 + 二分）
        Contains,   // 词头包含（朴素扫，量级 1 ms）
        Suffix,     // 词头后缀（反转词 + 排序数组，构词法检索：-tion 实测 619 个词头）
        Phrase,     // 短语词项倒排（11,628 词项 / 424,305 条 postings）
        Gloss,      // 中文释义反查（2 字走 bigram，≥3 字走朴素扫）
        Fuzzy,      // 编辑距离 ≤1（拼错救回，全扫 14,625 词）
    }

    /// <summary>加载进度：阶段名 + 0..1 的总进度。回调用的是 UI 线程安全的 DispatcherOperation。</summary>
    public sealed class LoadProgress
    {
        public string Stage { get; set; }
        public double Fraction { get; set; }
        public override string ToString() { return Stage; }
    }

    /// <summary>
    /// 语料与索引的单源。形状全部来自 2026-10-05 的实测：54,356 行 → 14,625 个词头，
    /// 书内重复 37.2% 且归一只塌 5.9%（所以变体绝不合并），跨书短语高度共享而释义几乎全不同。
    ///
    /// 一次构建、之后只读：构建在后台线程跑完再把整个对象交给 UI，UI 侧不做任何增量写，
    /// 因此不需要锁，也不会出现"读到建了一半的索引"。
    /// </summary>
    public sealed class Corpus
    {
        public const int BookCount = 7;

        readonly List<BookInfo> books = new List<BookInfo>();
        public IReadOnlyList<BookInfo> Books { get { return books; } }

        string[] names = [];
        ulong[] bookBits = [];
        int[][] variantsOfWord = [];
        WordCard[] cards = [];

        int[] vBook = [];
        int[] vWord = [];
        string[][] vTrans = [];
        string[][] vType = [];
        string[][] vPhr = [];

        int[] byName = [];
        int[] rank = [];
        string[] revName = [];
        int[] byRev = [];
        Dictionary<string, int[]> phraseTerm = new Dictionary<string, int[]>();
        Dictionary<string, int[]> cjkGram = new Dictionary<string, int[]>();
        Dictionary<string, int[]> posWords = new Dictionary<string, int[]>();

        /// <summary>8 个视图：[0]=全部，[1..7]=各本书。启动时一次预建（实测 16 ms），切书只是换个数组引用。</summary>
        public WordCard[][] Views { get; private set; }

        /// <summary>每个视图的成员表，检索结果过滤靠它——O(1) 判定，不每次重建 HashSet。</summary>
        bool[][] inView = [];

        public int WordCount { get { return names.Length; } }
        public int VariantCount { get { return vBook.Length; } }
        public string DataRoot { get; private set; }

        // ── 数据根定位 ─────────────────────────────────────────────────
        //
        // 开发态 json/ 在工程根，打包成单 exe 后 json/ 得跟 exe 放一起，两处口径不同。
        // 找不到就点名报出找过哪几处，别说一句"没有数据"就完了。

        public static string ResolveDataRoot()
        {
            var tried = new List<string>();
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int hop = 0; dir != null && hop < 6; hop++, dir = dir.Parent)
            {
                string cand = Path.Combine(dir.FullName, "json");
                tried.Add(cand);
                if (HasJson(cand)) return cand;
            }
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk", "json");
            tried.Add(appData);
            if (HasJson(appData)) return appData;

            throw new DirectoryNotFoundException(
                "没找到词书数据目录（需要 json/ 下放 7 个 *-顺序.json）。找过：" + string.Join("；", tried));
        }

        static bool HasJson(string dir)
        {
            try { return Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0; }
            catch (Exception) { return false; }
        }

        // ── 构建 ──────────────────────────────────────────────────────

        public static Corpus Build(string root, Action<LoadProgress> report, Func<bool> cancel)
        {
            var c = new Corpus { DataRoot = root };
            var sw = new Stopwatch();

            void Step(string stage, double from, double to, Action body)
            {
                if (cancel != null && cancel()) throw new OperationCanceledException();
                if (report != null) report(new LoadProgress { Stage = stage, Fraction = from });
                sw.Restart();
                body();
                c.stageMs[stage] = sw.Elapsed.TotalMilliseconds;
            }

            c.stageMs = new Dictionary<string, double>();
            string[] raw = [];

            Step("读取词书文件", 0.0, 0.10, () =>
            {
                var files = JsonFiles(root);
                raw = files.Select(File.ReadAllText).ToArray();
                c.books.AddRange(DescribeBooks(files));
            });

            Step("解析条目", 0.10, 0.55, () => c.Parse(raw));
            Step("归并词头与书标签", 0.55, 0.68, () => c.BuildWords());
            Step("建前缀与后缀索引", 0.68, 0.74, () => c.BuildSorts());
            Step("建短语倒排索引", 0.74, 0.86, () => c.BuildPhraseIndex());
            Step("建释义字组索引", 0.86, 0.95, () => c.BuildGlossIndex());
            Step("准备导航视图", 0.95, 1.0, () => c.BuildViewsAndCards());

            if (report != null) report(new LoadProgress { Stage = "就绪", Fraction = 1.0 });
            return c;
        }

        Dictionary<string, double> stageMs = new Dictionary<string, double>();
        public IReadOnlyDictionary<string, double> StageMs { get { return stageMs; } }

        static string[] JsonFiles(string root)
        {
            return Directory.GetFiles(root, "*.json").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        /// <summary>文件顺序就是书的 Index（同时用作位图位），所以这两句必须只有一份实现。</summary>
        static List<BookInfo> DescribeBooks(string[] files)
        {
            var list = new List<BookInfo>(files.Length);
            for (int i = 0; i < files.Length; i++)
                list.Add(new BookInfo(i, BookKey(files[i]), BookLabel(files[i]), Path.GetFileName(files[i])));
            return list;
        }

        /// <summary>
        /// 只报名字、不读内容：启动时先用它把左栏填出来（收录数还不知道就只显书名），
        /// 后台构建完再由 Attach 就地补数字。实测整个构建 1.3 s，这期间左栏空着像界面坏了。
        /// </summary>
        public static List<BookInfo> PreviewBooks(string root)
        {
            return DescribeBooks(JsonFiles(root));
        }

        static string BookKey(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            int dash = stem.IndexOf('-');
            return dash >= 0 && dash + 1 < stem.Length ? stem.Substring(dash + 1) : stem;
        }

        static string BookLabel(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            stem = stem.Replace("顺序", "").Trim('-');
            int dash = stem.IndexOf('-');
            return dash >= 0 && dash + 1 < stem.Length ? stem.Substring(dash + 1) : stem;
        }

        void Parse(string[] raw)
        {
            var b = new List<int>();
            var w = new List<string>();
            var tr = new List<string[]>();
            var ty = new List<string[]>();
            var ph = new List<string[]>();

            for (int book = 0; book < raw.Length; book++)
            {
                using var doc = JsonDocument.Parse(raw[book]);
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var word = Str(e, "word");
                    var tl = new List<string>();
                    var yl = new List<string>();
                    if (e.TryGetProperty("translations", out var tp) && tp.ValueKind == JsonValueKind.Array)
                        foreach (var x in tp.EnumerateArray())
                        {
                            string t = Str(x, "translation");
                            tl.Add(t);
                            yl.Add(Str(x, "type"));
                        }
                    var pl = new List<string>();
                    if (e.TryGetProperty("phrases", out var pp) && pp.ValueKind == JsonValueKind.Array)
                        foreach (var x in pp.EnumerateArray())
                            if (x.TryGetProperty("phrase", out var q) && q.ValueKind == JsonValueKind.String)
                                pl.Add(q.GetString());

                    b.Add(book); w.Add(word);
                    tr.Add(tl.ToArray()); ty.Add(yl.ToArray()); ph.Add(pl.ToArray());
                }
            }
            vBook = b.ToArray(); vWord = null;
            transIn = tr.ToArray(); typeIn = ty.ToArray(); phrIn = ph.ToArray();
            wordIn = w.ToArray();
        }

        string[] wordIn = [];
        string[][] transIn = [];
        string[][] typeIn = [];
        string[][] phrIn = [];

        static string Str(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        }

        void BuildWords()
        {
            int n = wordIn.Length;
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nameList = new List<string>(n / 4);
            var bitList = new List<ulong>(n / 4);
            var groupCount = new List<int>(n / 4);
            var wordOfVariant = new int[n];

            for (int i = 0; i < n; i++)
            {
                string key = (wordIn[i] ?? string.Empty).Trim();
                if (!map.TryGetValue(key, out int id))
                {
                    id = nameList.Count;
                    map[key] = id;
                    nameList.Add(key);
                    bitList.Add(0);
                    groupCount.Add(0);
                }
                wordOfVariant[i] = id;
                bitList[id] |= 1UL << vBook[i];
                groupCount[id]++;
            }

            names = nameList.ToArray();
            bookBits = bitList.ToArray();
            vWord = wordOfVariant;

            // 每个词头名下的变体序号，供懒建卡片用
            variantsOfWord = new int[names.Length][];
            var fill = new int[names.Length];
            for (int i = 0; i < names.Length; i++) variantsOfWord[i] = new int[groupCount[i]];
            for (int i = 0; i < n; i++) variantsOfWord[vWord[i]][fill[vWord[i]]++] = i;

            for (int b = 0; b < books.Count; b++)
            {
                int hit = 0;
                for (int i = 0; i < names.Length; i++) if ((bookBits[i] >> b & 1) != 0) hit++;
                books[b].WordCount = hit;
            }
        }

        void BuildSorts()
        {
            byName = Range(names.Length);
            Array.Sort(byName, (a, b) => string.Compare(names[a], names[b], StringComparison.OrdinalIgnoreCase));
            // rank[wordId] = 字母序里的位次。结果一律按它排，否则列表顺序变成"语料里第几次出现"，
            // 而这份语料恰好也按字母排——顺序对了是巧合，换一本乱序词书就会立刻露馅。
            rank = new int[names.Length];
            for (int k = 0; k < byName.Length; k++) rank[byName[k]] = k;
            revName = names.Select(n => new string((n ?? string.Empty).ToLowerInvariant().Reverse().ToArray())).ToArray();
            byRev = Range(names.Length);
            Array.Sort(byRev, (a, b) => string.Compare(revName[a], revName[b], StringComparison.Ordinal));
        }

        void BuildPhraseIndex()
        {
            phraseTerm = Inverted(i =>
            {
                var set = new Adder();
                foreach (var p in phrIn[i]) foreach (var t in Tokens(p)) if (t.Length > 1) set.Keys.Add(t);
                return set;
            });
        }

        void BuildGlossIndex()
        {
            cjkGram = Inverted(i =>
            {
                var set = new Adder();
                foreach (var t in transIn[i])
                {
                    var cs = Cjk(t);
                    for (int g = 0; g + 1 < cs.Count; g++) set.Keys.Add(cs[g] + "" + cs[g + 1]);
                    if (cs.Count == 1) set.Keys.Add(cs[0] + "\0");
                }
                return set;
            });
        }

        Dictionary<string, int[]> Inverted(Func<int, Adder> feed)
        {
            var d = new Dictionary<string, List<int>>();
            for (int i = 0; i < vWord.Length; i++)
            {
                var bag = feed(i);
                foreach (var k in bag.Keys)
                {
                    if (!d.TryGetValue(k, out var l)) d[k] = l = new List<int>();
                    l.Add(vWord[i]);
                }
            }
            var outp = new Dictionary<string, int[]>(d.Count);
            foreach (var kv in d)
            {
                var a = kv.Value.ToArray();
                Array.Sort(a);
                outp[kv.Key] = DedupSorted(a);
            }
            return outp;
        }

        void BuildViewsAndCards()
        {
            posWords = new Dictionary<string, int[]>();
            var tmp = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < vWord.Length; i++)
                foreach (var tok in PosTokens(string.Join(" ", typeIn[i])))
                {
                    if (!tmp.TryGetValue(tok, out var s)) tmp[tok] = s = new HashSet<int>();
                    s.Add(vWord[i]);
                }
            foreach (var kv in tmp) { var a = kv.Value.ToArray(); Array.Sort(a); posWords[kv.Key] = a; }

            cards = new WordCard[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                cards[i] = new WordCard
                {
                    Corpus = this,
                    WordId = i,
                    Word = names[i],
                    BookBadges = Badges(bookBits[i]),
                    BookCount = System.Numerics.BitOperations.PopCount(bookBits[i]),
                    VariantCount = variantsOfWord[i].Length,
                    PhraseCount = variantsOfWord[i].Sum(v => phrIn[v].Length),
                    Summary = FirstGloss(i),
                };
            }

            Views = new WordCard[books.Count + 1][];
            inView = new bool[books.Count + 1][];
            for (int v = 0; v < Views.Length; v++) inView[v] = new bool[names.Length];

            Views[0] = byName.Select(i => cards[i]).ToArray();
            for (int i = 0; i < names.Length; i++) inView[0][i] = true;

            for (int b = 0; b < books.Count; b++)
            {
                int book = b;
                Views[b + 1] = byName.Where(i => (bookBits[i] >> book & 1) != 0).Select(i => cards[i]).ToArray();
                foreach (var card in Views[b + 1]) inView[b + 1][card.WordId] = true;
            }
        }

        string FirstGloss(int wordId)
        {
            foreach (var v in variantsOfWord[wordId])
            {
                foreach (var t in transIn[v])
                {
                    string s = (t ?? string.Empty).Trim();
                    if (s.Length > 0) return s.Length > 46 ? s.Substring(0, 46) + "…" : s;
                }
            }
            return "";
        }

        string Badges(ulong bits)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < books.Count; i++)
                if ((bits >> i & 1) != 0) { if (sb.Length > 0) sb.Append(' '); sb.Append(books[i].Label); }
            return sb.ToString();
        }

        // ── 卡片懒建 ───────────────────────────────────────────────────

        internal IReadOnlyList<VariantGroup> BuildVariants(int wordId)
        {
            var ids = variantsOfWord[wordId];
            var list = new List<VariantGroup>(ids.Length);
            foreach (int v in ids)
            {
                string first = transIn[v].Length > 0 ? transIn[v][0] : "";
                bool spaced = first.Length > 0 && (first[0] == ' ' || first[0] == '　');
                list.Add(new VariantGroup
                {
                    BookLabel = books[vBook[v]].Label,
                    SourceLabel = spaced ? "补源" : "主源",
                    Translations = transIn[v].Select(t => (t ?? string.Empty).Trim()).Where(t => t.Length > 0).ToArray(),
                    PosRaw = typeIn[v].Where(t => !string.IsNullOrWhiteSpace(t)).ToArray(),
                    Phrases = phrIn[v].Where(p => !string.IsNullOrWhiteSpace(p)).ToArray(),
                });
            }
            return list;
        }

        // ── 检索 ──────────────────────────────────────────────────────

        public WordCard[] Search(SearchMode mode, string query, int bookView)
        {
            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0) return Views[bookView];
            string q = query.Trim();
            int[] ids = mode switch
            {
                SearchMode.Prefix => Prefix(q),
                SearchMode.Contains => NaiveWord(s => s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0),
                SearchMode.Suffix => Suffix(q),
                SearchMode.Phrase => Term(q),
                SearchMode.Gloss => Gloss(q),
                SearchMode.Fuzzy => Fuzzy(q, 1),
                _ => Array.Empty<int>(),
            };
            return Project(ids, bookView);
        }

        /// <summary>结果按当前视图过滤并保持字母序；实测最宽的一次（全部视图 14,625 项）赋值到屏 8.6 ms。</summary>
        WordCard[] Project(int[] ids, int bookView)
        {
            var member = inView[bookView];
            var keep = new List<int>(ids.Length);
            int last = -1;
            foreach (int id in OrderById(ids))
            {
                if (id == last) continue;
                last = id;
                if (member[id]) keep.Add(id);
            }
            keep.Sort((a, b) => rank[a] - rank[b]);
            var r = new WordCard[keep.Count];
            for (int i = 0; i < keep.Count; i++) r[i] = cards[keep[i]];
            return r;
        }

        static int[] OrderById(int[] ids)
        {
            var a = (int[])ids.Clone();
            Array.Sort(a);
            return a;
        }

        public int[] Prefix(string p)
        {
            int lo = Lower(byName, p, false), hi = Lower(byName, p + "{", false);
            var r = new int[hi - lo];
            for (int i = lo; i < hi; i++) r[i - lo] = byName[i];
            return r;
        }

        public int[] Suffix(string s)
        {
            string key = new string(s.ToLowerInvariant().Reverse().ToArray());
            int lo = Lower(byRev, key, true), hi = Lower(byRev, key + "{", true);
            var r = new int[hi - lo];
            for (int i = lo; i < hi; i++) r[i - lo] = byRev[i];
            return r;
        }

        public int[] Term(string t) => phraseTerm.TryGetValue(t.ToLowerInvariant(), out var a) ? a : Array.Empty<int>();

        /// <summary>
        /// 中文释义反查。bigram 索引天生答不了 ≥3 字子串（探针实测：2 字 46 命中、3 字返回 0 而真值是 2），
        /// 所以长度不是 2 的查询退回朴素扫——69,751 条释义串，同量级的短语串全扫实测 12.8 ms，
        /// 距 500 ms 闸门有一个数量级的余量。
        /// </summary>
        public int[] Gloss(string q)
        {
            var cs = Cjk(q);
            if (cs.Count == 2) return cjkGram.TryGetValue(q, out var a) ? a : Array.Empty<int>();
            if (cs.Count == 1) return cjkGram.TryGetValue(cs[0] + "\0", out var one) ? one : ScanGloss(q);
            return ScanGloss(q);
        }

        int[] ScanGloss(string q)
        {
            var hit = new List<int>();
            for (int v = 0; v < vWord.Length; v++)
                foreach (var t in transIn[v])
                    if (t != null && t.IndexOf(q, StringComparison.Ordinal) >= 0) { hit.Add(vWord[v]); break; }
            hit.Sort();
            return DedupSorted(hit.ToArray());
        }

        public int[] Fuzzy(string q, int maxDist)
        {
            string lq = q.ToLowerInvariant();
            var hit = new List<int>();
            for (int i = 0; i < names.Length; i++)
            {
                var w = names[i];
                if (Math.Abs((w ?? string.Empty).Length - lq.Length) > maxDist) continue;
                if (Lev(lq, w.ToLowerInvariant(), maxDist) <= maxDist) hit.Add(i);
            }
            hit.Sort();
            return hit.ToArray();
        }

        int[] NaiveWord(Func<string, bool> pred)
        {
            var hit = new List<int>();
            for (int i = 0; i < names.Length; i++) if (pred(names[i] ?? string.Empty)) hit.Add(i);
            return hit.ToArray();
        }

        int Lower(int[] order, string key, bool rev)
        {
            int lo = 0, hi = order.Length;
            while (lo < hi)
            {
                int m = (lo + hi) >>> 1;
                string s = rev ? revName[order[m]] : names[order[m]];
                if (string.Compare(s, key, rev ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) < 0) lo = m + 1;
                else hi = m;
            }
            return lo;
        }

        // ── 自检用的朴素真值（独立实现，不复用上面的索引路径）──────────

        public int[] TruthPrefix(string p) => NaiveWord(s => s.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        public int[] TruthSuffix(string s) => NaiveWord(t => t.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        public int[] TruthContains(string s) => NaiveWord(t => t.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        public int[] TruthGloss(string s) => NaiveWord2(v => transIn[v].Any(t => t != null && t.IndexOf(s, StringComparison.Ordinal) >= 0));

        public int[] TruthTerm(string t)
        {
            return NaiveWord2(v => phrIn[v].Any(p => Tokens(p ?? string.Empty).Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        public int[] TruthFuzzy(string q, int maxDist)
        {
            string lq = q.ToLowerInvariant();
            var hit = new List<int>();
            for (int i = 0; i < names.Length; i++) if (FullLev(lq, (names[i] ?? string.Empty).ToLowerInvariant()) <= maxDist) hit.Add(i);
            hit.Sort();
            return hit.ToArray();
        }

        int[] NaiveWord2(Func<int, bool> variantPred)
        {
            var hit = new HashSet<int>();
            for (int v = 0; v < vWord.Length; v++) if (variantPred(v)) hit.Add(vWord[v]);
            var a = hit.ToArray();
            Array.Sort(a);
            return a;
        }

        public int PosCount(string p) => posWords.TryGetValue(p, out var a) ? a.Length : 0;
        public int TermCount(string t) => phraseTerm.TryGetValue(t, out var a) ? a.Length : 0;
        public int GramCount(string g) => cjkGram.TryGetValue(g, out var a) ? a.Length : 0;
        public int BookWords(int book) => book >= 0 && book < books.Count ? books[book].WordCount : 0;
        public string WordAt(int id) => names[id];
        /// <summary>
        /// 独立真值：不查位图，直接线性扫原始解析行问"这个词头出现在哪几本书里"。
        /// 视图成员表用的是位压缩，这条用来证明压缩没出错。
        /// </summary>
        public int[] TruthBooksOf(int wordId)
        {
            var set = new SortedSet<int>();
            for (int v = 0; v < vWord.Length; v++) if (vWord[v] == wordId) set.Add(vBook[v]);
            return set.ToArray();
        }
        public string[] VariantsOfGloss(int wordId) => variantsOfWord[wordId].SelectMany(v => transIn[v]).ToArray();
        public string[] VariantsOfPhrase(int wordId) => variantsOfWord[wordId].SelectMany(v => phrIn[v]).ToArray();

        // ── 文本工具 ──────────────────────────────────────────────────

        static IEnumerable<string> Tokens(string s)
        {
            int start = -1;
            for (int i = 0; i <= s.Length; i++)
            {
                bool letter = i < s.Length && char.IsLetter(s[i]);
                if (letter) { if (start < 0) start = i; }
                else if (start >= 0) { yield return s.Substring(start, i - start).ToLowerInvariant(); start = -1; }
            }
        }

        static List<char> Cjk(string s)
        {
            var l = new List<char>();
            foreach (var ch in s ?? string.Empty) if (ch >= 0x4E00 && ch <= 0x9FFF) l.Add(ch);
            return l;
        }

        /// <summary>词性串脏得没边（n&amp;v / v / n / [复]n / (缩作OK)a&amp;ad / Mar / n&amp;vt&amp;n），
        /// 这里只按字母段切成 1..4 长度的候选，切不出来的原样进 PosRaw 由界面降级显示，不硬猜。</summary>
        static IEnumerable<string> PosTokens(string type)
        {
            var sb = new StringBuilder();
            foreach (var ch in type ?? string.Empty)
            {
                if (char.IsLetter(ch)) sb.Append(char.ToLowerInvariant(ch));
                else { if (sb.Length >= 1 && sb.Length <= 4) yield return sb.ToString(); sb.Clear(); }
            }
            if (sb.Length >= 1 && sb.Length <= 4) yield return sb.ToString();
        }

        static int[] DedupSorted(int[] a)
        {
            if (a.Length < 2) return a;
            var l = new List<int>(a.Length) { a[0] };
            for (int i = 1; i < a.Length; i++) if (a[i] != l[l.Count - 1]) l.Add(a[i]);
            return l.ToArray();
        }

        static int[] Range(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            return a;
        }

        static int Lev(string a, string b, int cut)
        {
            int n = b.Length;
            var prev = new int[n + 1];
            var cur = new int[n + 1];
            for (int j = 0; j <= n; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                int best = cur[0];
                for (int j = 1; j <= n; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                    if (cur[j] < best) best = cur[j];
                }
                if (best > cut) return cut + 1;
                var t = prev; prev = cur; cur = t;
            }
            return prev[n];
        }

        static int FullLev(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        sealed class Adder
        {
            public readonly HashSet<string> Keys = new HashSet<string>();
        }
    }
}
