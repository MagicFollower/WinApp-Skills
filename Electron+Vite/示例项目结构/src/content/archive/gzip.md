# gzip / gunzip / zcat — 单文件压缩与查看

> gzip 一次只压缩一个文件（不打包目录），`-1`~`-9` 控制压缩级别，`gunzip` 与 `zcat` 是同一程序的解压、查看别名。

## 语法

```bash
gzip [选项] [文件...]
gunzip [选项] 文件.gz   # 等于 gzip -d
zcat 文件.gz            # 等于 gzip -dc：输出内容到标准输出，磁盘上不动
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-1` … `-9` | 压缩级别，默认 `-6`；`-1`/`--fast` 最快，`-9`/`--best` 最小最慢 |
| `-d`, `--decompress` | 解压；`gunzip` 等价于 `gzip -d` |
| `-k`, `--keep` | 保留源文件；默认成功后删除源文件 |
| `-c`, `--stdout` | 结果写到标准输出，源文件保持不变 |
| `-t`, `--test` | 只校验压缩数据完整性，不产生输出 |
| `-l`, `--list` | 显示压缩后大小、原始大小、压缩率、压缩时记录的原文件名 |
| `-f`, `--force` | 强制覆盖已存在的输出文件，并允许压缩有多个硬链接的文件 |
| `-v`, `--verbose` | 显示压缩率等信息 |
| 无递归选项 | GNU gzip 跳过目录，不给 `-r`；想批量处理用 `find -exec`，想整目录压缩用 `tar -czf` |

## 示例

压缩日志并保留原文件，`-9` 用于要长期归档的文件：

```bash
gzip -9k access.log          # 生成 access.log.gz，access.log 仍在
```

压缩流不落中间文件，直接写进 tar 归档；反向用 `gunzip -c` 喂给 tar：

```bash
tar -cf - /etc | gzip -9 > /backup/etc.tar.gz
gunzip -c /backup/etc.tar.gz | tar -xf - -C /restore
```

不解压就查看和搜索内容，`zgrep` 对多个 `.gz` 逐个匹配：

```bash
zcat app.log.2.gz | tail -100
zgrep -n 'Out of memory' /var/log/app/app.log.*.gz
```

批量解压并保留压缩包，先测完整性再动手：

```bash
gzip -t /backup/*.gz && gunzip -kv /backup/*.gz
```

按修改时间递归压缩历史日志（GNU find 的 `-exec ... +`）：

```bash
find /var/log/app -type f -name '*.log' -mtime +7 -exec gzip -9 {} +
```

## 注意事项

- 一个 `.gz` 只对应一个文件：目录必须先 `tar`，gzip 会直接跳过目录；符号链接也不跟随，除非从标准输入读。
- 成功后源文件被删除；同名 `.gz` 已存在时会交互式询问，脚本里需要 `-f`。
- 对已经压缩过的数据（`.zip`、jpg、mp4、`xz` 包）再 gzip 基本不减小，还可能变大几个字节。
- 数据损坏时 gzip 会报 `unexpected end of file`；交付前用 `gzip -t` 逐个校验。

## 相关命令

- `tar` — 目录打包；`tar -czf` 内部就是调用 gzip
- `zstd` — 同级压缩率下速度快得多，且自带 `-r` 递归
- `bzip2` / `xz` — 压缩率更高但更慢的单文件压缩器
- `zdiff` / `zcmp` / `zmore` / `znew` — gzip 套件里对压缩包做比较、分页、格式转换
