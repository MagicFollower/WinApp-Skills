# du — 统计文件与目录占用空间

> `-sh` 给单个目录的可读总量，`--max-depth=N`（`-d N`）控制展开到第几层。

## 语法
```bash
du [OPTION]... [FILE]...
du [OPTION]... --files0-from=F
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-s`, `--summarize` | 只显示总计，不逐个列出子目录 |
| `-h`, `--human-readable` | 以 K/M/G 显示（1024 进制）；`--si` 用 1000 进制 |
| `--max-depth=N`, `-d N` | 只展开到第 N 层子目录 |
| `-a`, `--all` | 连单个文件一起统计，而不只是目录 |
| `-c`, `--total` | 末尾追加一行总计 |
| `-x`, `--one-file-system` | 不跨越挂载点，避免把别的分区算进来 |
| `--apparent-size` | 显示表观字节数，而不是实际分配的磁盘块 |
| `-b`, `--bytes` | 等于 `--apparent-size --block-size=1` |
| `-k`, `-m`, `--block-size=SIZE` | 统一换算输出单位（默认单位是 1024 字节块） |
| `--exclude=PATTERN` | 排除匹配的路径，可重复给出 |
| `-l`, `--count-links` | 硬链接每次都计入，默认只算第一次出现 |
| `-0`, `--null` | 输出以 NUL 结尾，配合 `sort -z` |

## 示例
看某个目录整体占多少：

```bash
du -sh /var
```

逐层展开一级子目录并按大小倒序排列：

```bash
du -h --max-depth=1 /var | sort -hr | head -15
```

连文件一起统计，找出最占空间的十项：

```bash
du -ah ~/Downloads | sort -rh | head -10
```

对比实际占用与表观大小（稀疏文件差异最明显）：

```bash
dd if=/dev/zero of=sparse.img bs=1 count=0 seek=1G
du -h sparse.img
du -h --apparent-size sparse.img
```

只统计根文件系统自身，不被 `/proc`、`/mnt` 等挂载点带偏：

```bash
du -xh --max-depth=1 /
```

## 注意事项
- du 数的是实际分配的磁盘块，会向上取整到块大小，几字节的小文件也至少算 1 块；`ls -l` 的字节数与 du 结果天生不等。
- du 与 df 对不上通常有两类原因：已删除但仍被进程持有句柄的文件（用 `lsof +L1` 查），以及日志、snapshot、透明压缩等文件系统自身开销。
- 非 root 统计系统目录时，读不到的子目录会被跳过并打印 `Permission denied`，结果偏小；需要准确数字时用 `sudo du`。
- `--max-depth` 是 GNU 扩展，BusyBox 等精简实现只认 `-d`；跨环境脚本建议写 `-d 1`。
- `sort -h` / `sort -hr` 需要 GNU coreutils，且 du 与 sort 的单位进制要一致（`-h` 与 `--si` 别混用）。

## 相关命令
- `df` — 查看整个文件系统的用量与剩余空间
- `find -size` — 按大小筛选单个文件
- `lsof +L1` — 找出已删除但仍未释放空间的文件
- `ncdu` — 交互式浏览目录占用（需另行安装）
