# set 与 shopt — shell 选项

> `set -euo pipefail` 是脚本的安全带，`shopt` 管的是另一类更细的行为开关。

## 语法

```bash
set -euo pipefail       # 一次写三个；- 开启、+ 关闭
set -o nounset -o xtrace
set -o                  # 列出全部 POSIX 选项的当前状态
shopt -s globstar; shopt -u globstar
restore=$(set +o)       # 可 eval 回放的开关快照
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `-e` / `-o errexit` | 命令返回非 0 就退出脚本 |
| `-u` / `-o nounset` | 引用未定义变量时报错并退出 |
| `-o pipefail` | 管道返回码取最右一个非 0，不再只看末命令 |
| `-x` / `-o xtrace` | 把展开后的命令打到 stderr，配 `PS4` 定位行号 |
| `-v` / `-n` | 读取时原样打印脚本行 / 只解析语法不执行 |
| `-f` / `-m` / `-C` | 关闭通配展开 / 监视模式（`lastpipe` 需先关它）/ 禁止 `>` 覆盖已有文件 |
| `shopt -s expand_aliases` | 在脚本里启用别名展开 |
| `shopt -s globstar / nullglob / failglob / dotglob / nocaseglob` | 通配相关开关 |
| `shopt -s lastpipe` | 未开监视模式时，管道最后一段留在当前 shell 执行 |

## 示例

脚本头部三件套，`pipefail` 才能捕获管道中间环节的失败：

```bash
#!/usr/bin/env bash
set -euo pipefail
grep ERROR /var/log/syslog | wc -l
```

`-e` 的例外：条件位、`&&`/`||` 列表的非末项、`!` 取反、管道非末段，失败都不终止脚本：

```bash
set -e
if grep -q nosuchuser /etc/passwd; then :; fi
false || echo "仍然执行"
false | true; echo "上面不触发 errexit，退出码 $?"
```

`-u` 下先给默认值；调试时只开一段 `set -x`，结束立刻关掉：

```bash
set -u
echo "${1:-默认端口}"
PS4='+$LINENO: '
set -x; cp /etc/hosts /tmp/hosts.bak; set +x
```

函数被当作条件调用时，其内部的 `-e` 整体失效，这是最常见的误判来源：

```bash
set -e
pick() { grep -q nosuch /etc/passwd; echo "grep 失败但函数继续跑完"; }
if pick; then echo "函数返回 0"; fi
```

## 注意事项

- `set -e` 不覆盖后台任务，也不看 `local x=$(false)` 这种「声明即赋值」的失败；需要捕获时用 `|| exit 1` 或 `trap ... ERR` 加 `-o errtrace`。
- `pipefail`、`lastpipe`、`nounset` 都是 bash 特性，`#!/bin/sh` 的 dash 下 `set -o pipefail` 直接 illegal option；bash 4.4 之前空数组的 `"${arr[@]}"` 在 `-u` 下还会算未定义。
- 改过的开关一直影响后续代码，函数内临时改 `IFS` 或开 `set -x` 要在返回前恢复，或先用 `restore=$(set +o)` 再 `eval "$restore"` 回滚。

## 相关命令

- `shopt` — 查看与切换 shell 扩展开关
- `trap` — 捕获 ERR/EXIT 做统一收尾
- `help set` / `bash -n` — 完整选项表与语法检查
