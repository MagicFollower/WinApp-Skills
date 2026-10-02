# touch — 新建空文件或更新时间戳

> 文件不存在时创建零字节文件，存在时把访问时间与修改时间刷新为当前时间。

## 语法
```bash
touch [OPTION]... FILE...
```

## 常用选项
| 选项 | 说明 |
| --- | --- |
| `-c`, `--no-create` | 文件不存在时什么都不做，也不报错 |
| `-a` | 只改访问时间（等价 `--time=access`） |
| `-m` | 只改修改时间（等价 `--time=modify`） |
| `-d, --date=STRING` | 用指定时间代替当前时间，如 `2026-05-01 09:30`、`3 days ago` |
| `-t STAMP` | 用 `[[CC]YY]MMDDhhmm[.ss]` 格式的紧凑时间戳 |
| `-r, --reference=FILE` | 参照另一个文件的时间戳 |
| `-h`, `--no-dereference` | 改符号链接自身的时间而不是目标 |

## 示例
创建空文件；若已存在则刷新时间戳：

```bash
touch notes.md
```

只动修改时间来触发增量构建：

```bash
touch -m src/main.c
make
```

不想误建文件时加 `-c`，仅对已存在的文件生效：

```bash
touch -c ./config.ini
```

设定精确时间，或参照另一个文件对齐：

```bash
touch -d '2026-05-01 09:30:00' report.pdf
touch -r report.pdf summary.pdf
```

紧凑格式 `CCYYMMDDhhmm.ss`：

```bash
touch -t 202601312359.59 archive.tar.gz
```

批量统一目录内文件的时间戳，便于可复现打包：

```bash
find ./dist -type f -exec touch -d '2026-01-01 00:00:00 UTC' {} +
```

## 注意事项
- 把时间设成任意值需要你是文件属主或是 root；其他用户即使有写权限，也只能设成当前时间，否则报 `Operation not permitted`。
- 创建新文件需要对父目录的写和执行权限；只改时间戳时不需要目录写权限。
- `touch` 默认跟随符号链接去改目标文件，要动链接自身得加 `-h`，且该能力取决于内核与文件系统支持。
- `-t` 只写两位年份时按 POSIX 规则推断世纪，跨世纪场景请写满四位；核对结果用 `stat FILE`。
- `relatime`/`noatime` 挂载下普通读取不会即时更新 atime，别把 atime 当成访问审计依据。

## 相关命令
- `stat` — 查看 atime/mtime/ctime 的精确值
- `date` — 生成 `-d` 可识别的时间字符串
- `find -newer FILE` — 按时间戳筛选文件
- `mkdir` — 创建目录
