# find — 按名称、时间、大小查找并处理

> 路径参数写在命令名后、表达式之前；`-name`/`-type`/`-mtime`/`-size` 筛选，`-exec`/`-delete` 处理。

## 语法
```bash
find [-H] [-L] [-P] [-Olevel] [path...] [expression]
```

## 常用选项
| 表达式 | 说明 |
| --- | --- |
| `-name GLOB` / `-iname GLOB` | 按文件名（不含路径）匹配，通配符必须加引号 |
| `-path GLOB` | 按整条路径匹配 |
| `-type c` | `f` 普通文件、`d` 目录、`l` 符号链接、`s` socket |
| `-mtime n` / `-mmin n` | 按修改时间筛选，单位分别为天与分钟，支持 `+`/`-` |
| `-newermt '2026-05-01'` | 按绝对时间点比较修改时间 |
| `-size n` | 按大小筛选，后缀 `c` 为精确字节，`k`/`M`/`G` 分别是 KiB/MiB/GiB |
| `-maxdepth N` / `-mindepth N` | 限制搜索深度，应放在其他表达式之前 |
| `-not`/`!`, `-a`, `-o` | 逻辑运算，`-o` 优先级低于 `-a` |
| `-exec CMD {} ;` | 对每个匹配结果各执行一次命令 |
| `-exec CMD {} +` | 把多个结果作为末尾参数一次性传给命令 |
| `-delete` | 删除匹配项，隐含 `-depth` |
| `-print0` | 以 NUL 分隔输出，配合 `xargs -0` 处理特殊文件名 |
| `-prune` | 剪掉匹配到的目录分支，不再下降 |

## 示例
按名称与类型查找日志，再加时间条件（`-mtime +7` 实际是超过 8 天）：

```bash
find /var/log -type f -name '*.log'
find /var/log -type f -name '*.log' -mtime +7 -print
```

在单个文件系统内定位超过 500 MiB 的文件：

```bash
find / -xdev -type f -size +500M 2>/dev/null
```

批量搜索内容，用 `{}` 加 `+` 减少进程创建：

```bash
find ./src -type f -name '*.c' -exec grep -l 'TODO' {} +
```

跳过 `.git` 目录，只列出 markdown：

```bash
find . -path ./.git -prune -o -type f -name '*.md' -print
```

先确认匹配结果，再删除；或用 `-print0` 配合 `xargs -0`：

```bash
find ./build -type f -name '*.o' -print
find ./build -type f -name '*.o' -delete
find /srv/data -type f -name '*.tmp' -print0 | xargs -0 rm -f --
```

## 注意事项
- 通配符必须加引号，写成 `-name *.log` 会先被 shell 展开，可能一个文件都不匹配或直接报错。
- `-mtime +7` 按整天向下取整，实际含义是“距今超过 8 天”；需要精确门槛用 `-mmin` 或 `-newermt`。
- 一旦表达式里出现 `-exec`、`-delete` 等动作，find 就不再默认打印结果，想看路径要显式加 `-print`。
- `-delete` 隐含 `-depth` 且与 `-prune` 冲突；删除前务必用同样条件先跑一遍只打印的版本。
- 非 root 遍历 `/`、`/root` 等目录会有大量 `Permission denied`，`2>/dev/null` 只是掩盖错误，结果并不完整。

## 相关命令
- `locate` / `plocate` — 查索引数据库，比 find 快但可能过期
- `grep -r` — 递归搜索文件内容
- `xargs` — 把 find 的输出批量交给命令执行
- `stat` — 查看单个文件的完整时间戳与大小
