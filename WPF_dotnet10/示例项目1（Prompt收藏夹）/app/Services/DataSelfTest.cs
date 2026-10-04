using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 数据链路的自检段：frontmatter 保真 + 仓储的真实文件读写。
    /// 全部落在一个临时目录里，跑完删除；不碰用户的 Prompt 根目录。
    /// 这一段的由来：设置编解码那 29 组断言一条也不覆盖磁盘链路，"selftest 退 0"曾经
    /// 只等于"设置没退化"。
    /// </summary>
    internal static class DataSelfTest
    {
        public static void Run(SelfTestResult r)
        {
            r.Section("data");

            CheckFrontmatter(r);
            CheckRepository(r);
            CheckModuleNotifications(r);
        }

        // ── 模型通知 ───────────────────────────────────────────────

        /// <summary>
        /// 模块名与计数都会被就地改写（改名、刷新计数），所以必须播报变化。
        /// 这条断言对着的是那类"改了不生效"的缺陷：属性没有 PropertyChanged 时，
        /// 任何一次整表重建都会让它看起来是好的，只有真正走增量刷新时才暴露。
        /// </summary>
        private static void CheckModuleNotifications(SelfTestResult r)
        {
            var fired = new List<string>();
            var module = new Models.PromptModule { Name = "写作", FullPath = @"C:\Prompts\写作", EntryCount = 3 };
            ((System.ComponentModel.INotifyPropertyChanged)module).PropertyChanged +=
                (s, e) => fired.Add(e.PropertyName);

            module.Name = "编程";
            module.EntryCount = 5;
            module.EntryCount = 5;   // 同值不应该再报一次，否则每次刷新都全表重绘

            r.Check(fired.Count == 2 && fired[0] == "Name" && fired[1] == "EntryCount",
                "PromptModule 的变更通知不对，实际收到 [" + string.Join(",", fired.ToArray()) + "]");
        }

        // ── frontmatter ────────────────────────────────────────────

        private static void CheckFrontmatter(SelfTestResult r)
        {
            string body = "你是无损转录器，不是总结器。\r\n\r\n硬性规则：\r\n1. 不得改写。\r\n";
            var created = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Local);
            var updated = new DateTime(2025, 1, 10, 15, 30, 0, DateTimeKind.Local);
            var used = new DateTime(2025, 1, 12, 9, 5, 0, DateTimeKind.Local);

            var data = new FrontmatterData
            {
                Title = "无损转录器",
                Module = "写作",
                Favorite = true,
                UseCount = 42,
                CreatedAt = created,
                UpdatedAt = updated,
                LastUsedAt = used
            };

            FrontmatterData reparsed;
            string rebodied;
            FrontmatterParser.Parse(FrontmatterParser.Serialize(data, body), out reparsed, out rebodied);

            r.Check(reparsed.Title == "无损转录器" && reparsed.Module == "写作"
                    && reparsed.Favorite && reparsed.UseCount == 42
                    && reparsed.CreatedAt == created && reparsed.UpdatedAt == updated
                    && reparsed.LastUsedAt == used,
                "已知字段往返丢失：" + Describe(reparsed));
            r.Check(rebodied == body, "正文往返被改动（换行或首尾空白被吃掉）");

            // 无 frontmatter：整段是正文，字段走默认，不抛异常
            FrontmatterData bare;
            string bareBody;
            FrontmatterParser.Parse("只有一段正文", out bare, out bareBody);
            r.Check(bareBody == "只有一段正文" && bare != null && bare.UseCount == 0
                    && !bare.Favorite && string.IsNullOrEmpty(bare.Title),
                "无 frontmatter 的文件没能按正文整段读入");

            // 正文里的 --- 不作分隔符（分隔符只认紧挨文件开头的那一对）
            string withRule = FrontmatterParser.Serialize(data,
                "上文\r\n---\r\n下文\r\n");
            FrontmatterData split;
            string splitBody;
            FrontmatterParser.Parse(withRule, out split, out splitBody);
            r.Check(splitBody.Contains("---") && split.Title == "无损转录器",
                "正文中的 --- 被误当成 frontmatter 分隔符");

            // 未知字段是外部工具（Obsidian / 用户编辑器）加的，备份=复制文件夹的承诺依赖它们不被吃掉
            string external = "---\r\ntitle: 无损转录器\r\nmodule: 写作\r\nfavorite: true\r\n"
                + "useCount: 42\r\ncreatedAt: " + created.ToString("o") + "\r\n"
                + "updatedAt: " + updated.ToString("o") + "\r\nlastUsedAt: " + used.ToString("o") + "\r\n"
                + "tags: 转录, 长文\r\npublisher: obsidian\r\n---\r\n正文\r\n";
            FrontmatterData kept;
            string keptBody;
            FrontmatterParser.Parse(external, out kept, out keptBody);

            string reserialized = FrontmatterParser.Serialize(kept, keptBody);
            FrontmatterData again;
            string againBody;
            FrontmatterParser.Parse(reserialized, out again, out againBody);
            r.Check(reserialized.Contains("tags: 转录, 长文") && reserialized.Contains("publisher: obsidian"),
                "未知 frontmatter 字段在写回时被静默删除（外部工具的元数据会丢）");
            r.Check(again.UseCount == 42 && againBody == keptBody,
                "含未知键的往返后已知字段或正文发生变化");

            // 值里带冒号（时间、URL、Windows 盘符）不能被冒号切分切坏
            string colonValue = "---\r\ntitle: a\r\nsource: https://example.com/x\r\n---\r\n正文\r\n";
            FrontmatterData colonData;
            string colonBody;
            FrontmatterParser.Parse(colonValue, out colonData, out colonBody);
            string colonBack = FrontmatterParser.Serialize(colonData, colonBody);
            r.Check(colonBack.Contains("source: https://example.com/x"),
                "值里含冒号的未知字段没能原样写回");
        }

        // ── 仓储真实 IO ─────────────────────────────────────────────

        private static void CheckRepository(SelfTestResult r)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "PromptFavorites-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                Directory.CreateDirectory(root);
                var repo = new FileSystemRepository(root);

                string modulePath = repo.CreateModule("写作");
                r.Check(repo.ModuleExists("写作") && Directory.Exists(modulePath),
                    "CreateModule 没能建出模块目录");

                var data = new FrontmatterData
                {
                    Title = "无损转录器",
                    Module = "写作",
                    Favorite = false,
                    UseCount = 3,
                    CreatedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Local),
                    UpdatedAt = new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Local),
                    LastUsedAt = null
                };

                string entryPath = repo.CreateEntry(modulePath, "无损转录器", data);
                r.Check(repo.EntryExists(modulePath, "无损转录器") && File.Exists(entryPath),
                    "CreateEntry 没落盘");

                // 权威=磁盘：文件名与目录名兜底 Title/Module，frontmatter 只是派生副本
                FrontmatterData orphan;
                string orphanBody;
                File.WriteAllText(Path.Combine(modulePath, "外部拷入.md"), "只有正文，没有 frontmatter");
                repo.ReadEntry(Path.Combine(modulePath, "外部拷入.md"), out orphan, out orphanBody);
                r.Check(orphan.Title == "外部拷入" && orphan.Module == "写作",
                    "无 frontmatter 的文件没按文件名/目录名兜底（读到 " + orphan.Title + "/" + orphan.Module + "）");

                // 写盘原子性：写完不留半截文件、不在模块目录里散落临时件
                WriteAndInspect(r, repo, entryPath, data,
                    string.Concat(Enumerable.Repeat("很长的正文", 400)) + "\r\n尾巴",
                    "短正文");

                // 收藏不更新 updatedAt（设计 §16 的口径）
                var beforeFav = File.ReadAllText(entryPath);
                repo.UpdateFavorite(entryPath, true);
                FrontmatterData afterFav;
                string afterFavBody;
                repo.ReadEntry(entryPath, out afterFav, out afterFavBody);
                r.Check(afterFav.Favorite && afterFav.UpdatedAt == data.UpdatedAt,
                    "切换收藏把 updatedAt 推进了（设计明确禁止）");

                // 复制只推进 useCount/lastUsedAt，同样不动 updatedAt
                var stamp = new DateTime(2025, 3, 3, 8, 0, 0, DateTimeKind.Local);
                repo.UpdateUseCount(entryPath, afterFav.UseCount + 1, stamp);
                FrontmatterData afterCopy;
                string afterCopyBody;
                repo.ReadEntry(entryPath, out afterCopy, out afterCopyBody);
                r.Check(afterCopy.UseCount == data.UseCount + 1 && afterCopy.LastUsedAt == stamp
                        && afterCopy.UpdatedAt == data.UpdatedAt,
                    "复制后的计数/时间戳口径不对");

                // 改名与跨模块移动：目标已存在时绝不能静默覆盖
                string otherModule = repo.CreateModule("编程");
                var moved = new FrontmatterData
                {
                    Title = "代码审查",
                    Module = "编程",
                    Favorite = false,
                    UseCount = 0,
                    CreatedAt = data.CreatedAt,
                    UpdatedAt = data.CreatedAt,
                    LastUsedAt = null
                };
                string targetPath = repo.CreateEntry(otherModule, "代码审查", moved);
                string clash = Path.Combine(otherModule, "代码审查.md");
                r.Check(File.Exists(clash) && targetPath == clash,
                    "准备冲突用例失败：目标文件不在预期位置");
                string clashTextBefore = File.ReadAllText(clash);

                string sourceCopy = Path.Combine(modulePath, "要被移动的.md");
                File.Copy(targetPath, sourceCopy);
                bool threw = false;
                try { repo.RenameEntry(sourceCopy, clash); }
                catch (IOException) { threw = true; }
                catch (UnauthorizedAccessException) { threw = true; }
                r.Check(threw || File.Exists(clash),
                    "改名撞上同名目标时既不报错又覆盖了目标文件（数据丢失）");
                r.Check(File.Exists(clash) && File.ReadAllText(clash) == clashTextBefore,
                    "冲突改名把已存在的目标文件内容改坏了");

                // 正常跨模块移动
                repo.MoveEntry(sourceCopy, otherModule);
                r.Check(!File.Exists(sourceCopy), "MoveEntry 之后源文件仍在（等于复制了一份）");

                // 外部编辑器改过同一文件后，本进程的一次保存不该把外部新增的字段抹掉——
                // 这条依赖"未知键保留"，与上面 frontmatter 那段同因。
                string externalPath = Path.Combine(modulePath, "外部改过.md");
                repo.WriteEntry(externalPath, data, "正文一");
                File.AppendAllText(externalPath, string.Empty);
                var externalOnDisk = File.ReadAllText(externalPath)
                    .Replace("useCount: 3", "useCount: 3\r\neditorNote: 外部加的");
                File.WriteAllText(externalPath, externalOnDisk, new UTF8Encoding(false));
                FrontmatterData readExternal;
                string readExternalBody;
                repo.ReadEntry(externalPath, out readExternal, out readExternalBody);
                repo.WriteEntry(externalPath, readExternal, readExternalBody);
                r.Check(File.ReadAllText(externalPath).Contains("editorNote: 外部加的"),
                    "外部新增的 frontmatter 字段被本进程的一次保存抹掉了");

                r.Check(!Directory.EnumerateFiles(modulePath, "*.tmp").Any()
                        && !Directory.EnumerateFiles(otherModule, "*.tmp").Any(),
                    "写盘后目录里残留临时文件（原子写的临时件应当被改名或被删掉）");
            }
            catch (Exception ex)
            {
                r.Check(false, "仓储自检段异常中断：" + ex.GetType().Name + " " + ex.Message);
            }
            finally
            {
                TryDelete(root);
                r.Check(!Directory.Exists(root), "自检临时目录没能清干净：" + root);
            }
        }

        private static void WriteAndInspect(SelfTestResult r, FileSystemRepository repo,
            string entryPath, FrontmatterData data, string longBody, string shortBody)
        {
            repo.WriteEntry(entryPath, data, longBody);
            FrontmatterData d1;
            string b1;
            repo.ReadEntry(entryPath, out d1, out b1);
            r.Check(b1 == longBody, "长正文写入后读回不一致");

            repo.WriteEntry(entryPath, data, shortBody);
            FrontmatterData d2;
            string b2;
            repo.ReadEntry(entryPath, out d2, out b2);
            r.Check(b2 == shortBody, "覆写更短的正文后残留了旧尾巴（截断写没做对）");

            string text = File.ReadAllText(entryPath);
            r.Check(text.StartsWith("---") && text.IndexOf("---", 3) > 0,
                "覆写后的文件不是合法 frontmatter 开头");
        }

        private static string Describe(FrontmatterData d)
        {
            return "title=" + d.Title + ", module=" + d.Module + ", fav=" + d.Favorite
                + ", use=" + d.UseCount + ", created=" + d.CreatedAt.ToString("o")
                + ", updated=" + d.UpdatedAt.ToString("o") + ", lastUsed="
                + (d.LastUsedAt.HasValue ? d.LastUsedAt.Value.ToString("o") : "null");
        }

        private static void TryDelete(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
                    catch { }
                }
                Directory.Delete(dir, true);
            }
            catch
            {
            }
        }
    }
}
