# split — 切分大文件与合并还原

> GNU coreutils 的 split 按字节、按行或按块数把大文件切成有序小片，用 `cat` 按同名前缀顺序拼回原样。

## 语法

```bash
split [选项] 输入文件 [输出前缀]
split -b 200M big.iso part-          # 每片 200 MB
split -C 200m dump.sql sql-          # 每片不超 200 MB 且不截断行
cat part-* > big.iso                 # 还原
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-b`, `--bytes=SIZE` | 每片大小；`K`/`M`/`G` 是 1024 的幂，带 `B` 的后缀（`kB`/`MB`）是 1000 的幂 |
| `-l`, `--lines=N` | 每片固定行数 |
| `-C`, `--line-bytes=SIZE` | 每片最多 SIZE 字节，但不会把一行拆到两片之间 |
| `-n`, `--number-of-chunks=CHUNKS` | 按片数切分，如 `-n 20`；`-n l/20` 在行边界切，`-n r/20` 逐行轮流分配 |
| `-d`, `--numeric-suffixes` | 用数字后缀 `00`、`01`… 取代默认的 `aa`、`ab`…；可带起始值（较新 coreutils） |
| `-a`, `--suffix-length=N` | 后缀长度，默认 2；两位字母最多 676 片 |
| `--additional-suffix=SUF` | 在后缀后再追加固定后缀，如 `--additional-suffix=.bin` |

## 示例

按固定字节切分镜像，`-d` 让片名是数字，`-a 3` 保证片数足够且排序稳定：

```bash
split -b 2G -d -a 3 ubuntu.iso ubuntu.iso.
ls -1 ubuntu.iso.*
```

合并还原并核对内容一致；显式写出片名或固定 locale，避免 glob 顺序受环境影响：

```bash
LC_ALL=C cat ubuntu.iso.000 ubuntu.iso.001 ubuntu.iso.002 > restored.iso
md5sum ubuntu.iso restored.iso
```

文本导出按大小切但不截断行；单个超长行会独占一片：

```bash
split -C 10m -d --additional-suffix=.sql pgdump.sql dump-
cat dump-*.sql > pgdump.sql
```

直接切分 tar 流，不必先生出完整的中间大文件，还原时同样用管道：

```bash
tar cf - -C /srv app | split -b 1G -d - app.tar.
cat app.tar.* | tar xf - -C /restore
```

按行数切分大清单，方便并行导入；`$FILE` 是当前片名，可用 `--filter` 边切边处理：

```bash
split -l 100000 -d -a 4 users.csv users-
split -b 500M -d --filter='gzip > $FILE.gz' big.log log-
```

## 注意事项

- 还原顺序由片名决定：默认字母后缀按 `aa、ab…az、ba` 增长，`ls`/glob 的排序受 locale 影响，脚本里用 `-d` 或显式列出片名。
- split 只能切文件；目录要先 `tar`。`-b` 在任意字节处断开，除二进制/单流格式外，不要指望切开的压缩包或数据库文件片段能单独使用。
- 后缀长度限制片数：默认 2 位最多 676 片，超出后 split 直接报错，需要 `-a` 加长或用 `-d -a 4`。
- `-C`/`-l` 无法让每片大小完全均匀，超大单行会造成明显偏大的片；要求均匀就用 `-b` 或 `-n l/总数`。

## 相关命令

- `cat` — 按顺序拼接分片，是 split 唯一必需的还原手段
- `tar` — 目录先归档再切分，`tar cf - … | split` 可全程流式
- `7z` — `-v` 自带分卷，`7z x 第一卷` 会自动续卷
- `md5sum` — 为每片留下校验值，传输后逐片核对
