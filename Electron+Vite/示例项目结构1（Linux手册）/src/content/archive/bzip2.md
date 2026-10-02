# bzip2 / bunzip2 — 更高压缩率的单文件压缩

> bzip2 用 Burrows-Wheeler 变换换取比 gzip 更小的体积，代价是明显更慢；同样只处理单个文件，不打包目录。

## 语法

```bash
bzip2 [选项] [文件...]
bunzip2 [选项] 文件.bz2   # 等于 bzip2 -d
bzcat 文件.bz2            # 等于 bzip2 -dc：只看内容，不落文件
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-1` … `-9` | 块大小 100 KB…900 KB，压缩级别默认 `-9`；数字越大压缩率越高、内存越多 |
| `-d`, `--decompress` | 解压；`bunzip2` 即其别名 |
| `-k`, `--keep` | 解压或压缩后保留输入的 `.bz2`/原文件 |
| `-c`, `--stdout` | 输出到标准输入/输出流，源文件不动 |
| `-t`, `--test` | 只测试压缩数据完整性 |
| `-s`, `--small` | 降低内存占用（解压大块文件时明显），牺牲速度 |
| `-f`, `--force` | 强制覆盖已存在的输出文件，并允许压缩多个硬链接指向的文件 |
| `-v`, `--verbose` | 显示每个文件的压缩率等信息 |

## 示例

压缩分发用的 tar 归档，`-k` 保留原 tar 以便对比体积：

```bash
tar -cf app.tar -C /srv app
bzip2 -9k app.tar          # 生成 app.tar.bz2，app.tar 仍在
```

只看归档内容、不把 `.bz2` 解到磁盘：

```bash
bzcat app.tar.bz2 | tar -tf -
bzip2 -dc app.tar.bz2 | tar -xf - -C /opt/app
```

内存较小的机器上，解压用 `-s`，或降低级别只影响压缩阶段：

```bash
bzip2 -s -d hugefile.tar.bz2
```

交付前逐个校验完整性，失败时退出码非 0：

```bash
bzip2 -t /release/*/*.bz2 && echo "all ok"
```

批量压缩文本文件并保留原文件，配合 tar 时更常用 `tar -cjf`：

```bash
find /srv/data -type f -name '*.csv' -exec bzip2 -9k {} +
tar -cjf data-2026-10-21.tar.bz2 -C /srv data
```

## 注意事项

- 一个 `.bz2` 只装一个文件；整个目录要先 `tar -cjf`。多个 `.bz2` 直接拼接仍可被正确解压，但没有任何额外好处。
- 解压时 `-1`…`-9` 不起作用：块大小在压缩阶段已写入文件，解压所需内存由源文件决定，内存不足请加 `-s`。
- 压缩成功后默认删除源文件，且不会覆盖已存在的 `.bz2`（除非 `-f`）。
- 与 gzip 相比通常小 10%~15%，但耗时数倍；追求更小体积或更快的速度都应考虑 xz 或 zstd。

## 相关命令

- `tar` — `tar -cjf`/`-xjf`/`-tjf` 用 bzip2 处理目录归档
- `gzip` — 速度快、压缩率低一档的选择
- `xz` — 同级别下压缩率通常更好，解压内存也更低
- `bzcat` / `bzmore` / `bzdiff` — 查看、分页、比较 `.bz2` 内容
