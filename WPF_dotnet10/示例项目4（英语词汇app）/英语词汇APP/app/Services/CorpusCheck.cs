using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using VocabDesk.Models;

namespace VocabDesk.Services
{
    /// <summary>
    /// 真实语料上的检索正确性与延迟核对。
    ///
    /// 为什么单开一个挡而不是塞进 --selftest：--selftest 必须在没有词书数据的机器上也能全绿
    /// （它是发布门禁），而这里每条断言都要求 json/ 在场。退出码 = 失败断言数。
    ///
    /// 正确性一律拿"朴素全扫"当真值比对，且真值实现独立于索引实现——两边共用同一段代码的话，
    /// 索引建错了也照样对得上，那种断言是空的。
    /// </summary>
    public static class CorpusCheck
    {
        static readonly List<string> Log = new List<string>();
        static int fails;

        public static int Run()
        {
            fails = 0;
            Corpus c;
            try { c = Corpus.Build(Corpus.ResolveDataRoot(), null, null); }
            catch (Exception ex)
            {
                Log.Add("FAIL  无法加载语料：" + ex.Message);
                Write();
                return 1;
            }

            Header(c);
            CheckRecall(c);
            Latency(c);
            Write();
            return fails;
        }

        static void Header(Corpus c)
        {
            Log.Add("corpus  " + c.DataRoot);
            Log.Add("rows    " + c.VariantCount + " 条变体   words " + c.WordCount + " 个词头");
            Log.Add("books   " + string.Join(" / ", c.Books.Select(b => b.Label + "=" + b.WordCount)));
            Log.Add("stages  " + string.Join("  ", c.StageMs.Select(kv => kv.Key + "=" + kv.Value.ToString("F1") + "ms")));
            Log.Add("total   " + c.StageMs.Values.Sum().ToString("F1") + " ms");
            Log.Add("memory  workingSet=" + (Environment.WorkingSet / 1048576) + " MB  managed=" + (GC.GetTotalMemory(false) / 1048576) + " MB");
            Log.Add("");
            Log.Add("recall  index-vs-naive-truth (set equality)");
        }

        static void CheckRecall(Corpus c)
        {
            // 词头三形：前缀走排序数组，后缀走反转数组，包含走全扫
            Eq(c, "prefix co", c.Prefix("co"), c.TruthPrefix("co"));
            Eq(c, "prefix ab", c.Prefix("ab"), c.TruthPrefix("ab"));
            Eq(c, "suffix tion", c.Suffix("tion"), c.TruthSuffix("tion"));
            Eq(c, "suffix ment", c.Suffix("ment"), c.TruthSuffix("ment"));
            // 大小写混合查询：前缀二分用的是忽略大小写比较，真值用显式 StartsWith，两边口径要碰得上
            Eq(c, "prefix AB (mixed case)", c.Prefix("AB"), c.TruthPrefix("AB"));

            // 短语倒排
            Eq(c, "term payment", c.Term("payment"), c.TruthTerm("payment"));
            Eq(c, "term make", c.Term("make"), c.TruthTerm("make"));

            // 释义反查：2 字走 bigram，≥3 字必须走朴素兜底——bigram 索引天生答不了三字子串，
            // 少了那条兜底这里就会红（探针实测 3 字查询返回 0 而真值是 2）
            Eq(c, "gloss 能力(2gram)", c.Gloss("能力"), c.TruthGloss("能力"));
            Eq(c, "gloss 支付(2gram)", c.Gloss("支付"), c.TruthGloss("支付"));
            Eq(c, "gloss 的能力(3字走兜底)", c.Gloss("的能力"), c.TruthGloss("的能力"));
            Eq(c, "gloss 生产能力(4字走兜底)", c.Gloss("生产能力"), c.TruthGloss("生产能力"));

            // 拼写纠错
            Eq(c, "fuzzy abilty", c.Fuzzy("abilty", 1), c.TruthFuzzy("abilty", 1));
            // 空断言不算断言：两侧都是 0 的比对永远绿，所以这条要挑一个真能命中拼写纠错的串
            Eq(c, "fuzzy enviroment", c.Fuzzy("enviroment", 1), c.TruthFuzzy("enviroment", 1));
            NonEmpty(c.Fuzzy("enviroment", 1).Length, "fuzzy enviroment 应有命中");

            // 视图：书标签过滤必须与"该词在这本书里出现过"一致
            for (int b = 0; b < c.Books.Count; b++)
            {
                int book = b;
                var view = c.Views[book + 1].Select(w => w.WordId).OrderBy(x => x).ToArray();
                int expect = c.WordCount;
                var truth = new List<int>();
                for (int i = 0; i < expect; i++) if (ContainsBook(c, i, book)) truth.Add(i);
                Eq(c, "view " + c.Books[book].Label, view, truth.Distinct().OrderBy(x => x).ToArray());
            }

            // 结果顺序必须是字母序，不是内部 id 序（这份语料恰好按字母排，顺序对了可能只是巧合）
            var res = c.Search(SearchMode.Contains, "ab", 0);
            bool alpha = true;
            for (int i = 1; i < res.Length; i++)
                if (string.Compare(res[i - 1].Word, res[i].Word, StringComparison.OrdinalIgnoreCase) > 0) { alpha = false; break; }
            Yes(alpha, "结果按字母序", "[" + res.Length + " 项]");
        }

        static bool ContainsBook(Corpus c, int wordId, int book)
        {
            // 真值口径：从原始解析行线性扫出这个词头名下出现过哪些书号，
            // 与被测路径（加载时压成 ulong 位图、建视图时按位取）是两种推导。
            return c.TruthBooksOf(wordId).Contains(book);
        }

        static void Latency(Corpus c)
        {
            Log.Add("");
            Log.Add("latency per query shape (30 reps, ms)      p50      max     rows   gate500");
            Bench(c, "prefix ab", () => c.Search(SearchMode.Prefix, "ab", 0));
            Bench(c, "prefix a (widest)", () => c.Search(SearchMode.Prefix, "a", 0));
            Bench(c, "suffix tion", () => c.Search(SearchMode.Suffix, "tion", 0));
            Bench(c, "term payment", () => c.Search(SearchMode.Phrase, "payment", 0));
            Bench(c, "gloss 能力", () => c.Search(SearchMode.Gloss, "能力", 0));
            Bench(c, "gloss 的能力", () => c.Search(SearchMode.Gloss, "的能力", 0));
            Bench(c, "fuzzy abilty", () => c.Search(SearchMode.Fuzzy, "abilty", 0));
            Bench(c, "contains lect", () => c.Search(SearchMode.Contains, "lect", 0));
            Bench(c, "empty query", () => c.Search(SearchMode.Prefix, "qqqx", 0));

            // 卡片懒建：点开一张才算内容，预建全部 14,625 张实测要 130 ms + 48 MB
            var cards = c.Views[0];
            var sw = Stopwatch.StartNew();
            var one = new Stopwatch();
            double worst = 0, total = 0;
            for (int k = 0; k < cards.Length; k++)
            {
                one.Restart();
                var d = cards[k].Detail;
                double ms = one.Elapsed.TotalMilliseconds;
                if (ms > worst) worst = ms;
                if (k % 500 == 0) GC.Collect();
            }
            total = sw.Elapsed.TotalMilliseconds;
            Log.Add(string.Format("  build ALL {0} card details on demand: wall={1:F0} ms  worst single={2:F2} ms",
                cards.Length, total, worst));
            Yes(worst < 500, "单张卡片构建", "最坏 " + worst.ToString("F2") + " ms");
        }

        static void Bench(Corpus c, string label, Func<WordCard[]> f)
        {
            f();
            var s = new double[30];
            int rows = 0;
            var sw = new Stopwatch();
            for (int i = 0; i < 30; i++) { sw.Restart(); rows = f().Length; s[i] = sw.Elapsed.TotalMilliseconds; }
            Array.Sort(s);
            double p50 = s[15], max = s[29];
            Log.Add(string.Format("  {0,-28}{1,8:F3}{2,8:F3}{3,8}   {4}", label, p50, max, rows, max <= 500 ? "PASS" : "FAIL"));
            if (max > 500) fails++;
        }

        static void Eq(Corpus c, string label, int[] got, int[] truth)
        {
            var g = new HashSet<int>(got ?? Array.Empty<int>());
            var t = new HashSet<int>(truth ?? Array.Empty<int>());
            int onlyIdx = g.Count(x => !t.Contains(x));
            int onlyTruth = t.Count(x => !g.Contains(x));
            bool ok = onlyIdx == 0 && onlyTruth == 0;
            Log.Add(string.Format("  {0,-30}{1}  index={2,6} truth={3,6}  onlyIdx={4} onlyTruth={5}",
                label, ok ? "OK  " : "FAIL", g.Count, t.Count, onlyIdx, onlyTruth));
            if (!ok) fails++;
        }

        static void NonEmpty(int count, string label)
        {
            Yes(count > 0, label, "命中 " + count);
        }

        static void Yes(bool ok, string label, string detail)
        {
            Log.Add("  " + (ok ? "OK  " : "FAIL") + " " + label + "  " + detail);
            if (!ok) fails++;
        }

        static void Write()
        {
            Log.Add("");
            Log.Add(fails == 0 ? "RESULT all checks passed" : "RESULT failed=" + fails);
            string text = string.Join(Environment.NewLine, Log);
            Console.WriteLine(text);
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "corpuscheck.txt"), text, new UTF8Encoding(false));
            }
            catch (Exception ex) { Console.WriteLine("写文件失败：" + ex.Message); }
        }
    }
}
