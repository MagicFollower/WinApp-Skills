# tar — 打包与解包

> GNU tar 把多个文件合并成一个归档，再用 `-z`/`-j`/`-J`/`--zstd` 调用外部压缩程序，得到 `.tar.gz` 这类单文件压缩包。

## 语法

```bash
tar [操作选项] [修饰选项] -f 归档名 [路径...]
tar -czf app.tar.gz app/          # 创建 + gzip 压缩
tar -tzf app.tar.gz               # 只列出内容
tar -xzf app.tar.gz -C /opt/app/  # 解包到指定目录
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-c`, `--create` | 创建归档，同名归档会被覆盖 |
| `-x`, `--extract` | 解包 |
| `-t`, `--list` | 列出成员名，不向磁盘写文件 |
| `-f`, `--file=ARCHIVE` | 指定归档；`-f -` 表示标准输入/输出 |
| `-v`, `--verbose` | 显示处理的文件名；配 `-t` 时额外输出权限、大小、时间 |
| `-z`, `--gzip` | 调用 gzip，后缀 `.tar.gz`（或 `.tgz`） |
| `-j`, `--bzip2` | 调用 bzip2，后缀 `.tar.bz2` |
| `-J`, `--xz` | 调用 xz，后缀 `.tar.xz`；必须大写，小写已被 bzip2 占用 |
| `--zstd` | 调用 zstd，后缀 `.tar.zst`，需 GNU tar 1.31 及以上 |
| `-C`, `--directory=DIR` | 创建时先切到 DIR 以存相对路径；解包时把成员写到 DIR 下 |
| `--exclude=PATTERN` | 排除匹配路径，可重复；建议写在路径参数之前 |
| `--strip-components=N` | 解包时忽略成员名前 N 层目录 |
| `-k`, `--keep-old-files` | 目标位置已有同名文件时报错，而不是覆盖 |

## 示例

打包目录本身与打包目录内容写法不同：`-C` 后接父目录、再接子目录名，得到 `app/...`；`-C` 直接指向该目录再打包 `.`，得到 `./...`。

```bash
tar -czf app-$(date +%F).tar.gz -C /srv app
tar -czf app-content.tar.gz -C /srv/app .
```

不解包先确认里面有什么、有没有异常成员：

```bash
tar -tvf app-2026-10-21.tar.gz | head -20
tar -tzf app-2026-10-21.tar.gz | grep '\.conf$'
```

排除版本库、依赖和日志；通配符要加引号，避免被 shell 提前展开：

```bash
tar --exclude='.git' --exclude='node_modules' --exclude='*.log' \
    -czf src.tar.gz -C /srv/project .
```

上游源码包通常带一层与版本号同名的目录，`--strip-components=1` 可直接铺平；只想取一个成员时把成员名写在后面：

```bash
tar -xzf app-1.24.0.tar.gz --strip-components=1 -C /usr/local/src/app
tar -xzf app.tar.gz etc/app/config.yaml -C /tmp/inspect
```

## 注意事项

- 压缩选项要和归档配套：解 `.tar.xz` 用 `-xJf`，写成 `-xzf` 会报 `not in gzip format`。GNU tar 解包时能自动识别压缩格式，但显式写出更可靠；创建时不会自动识别，必须给对压缩选项。
- 解包默认静默覆盖同名文件，怕覆盖就加 `-k`，或加 `-w` 逐个确认。
- 归档成员若是绝对路径，`tar -xf` 默认会去掉开头的 `/` 并提示 `Removing leading /' from member names`；只有加 `-P` 才按绝对路径写出，风险很高，先 `-tvf` 检查。
- 压缩归档与源文件同时存在会额外占用磁盘；空间不足时用 `-f -` 配管道，或先 `split` 分卷再传输。

## 相关命令

- `gzip` / `bzip2` / `xz` / `zstd` — 单文件压缩器，tar 的压缩选项实际是把归档通过它们管道压缩
- `zip` / `unzip` — 每个成员独立压缩，可单独取出某个文件
- `split` — 把生成的单个大归档切成固定大小的分卷
- `rsync` — 目录间增量同步，比反复传整包 tar 省带宽
