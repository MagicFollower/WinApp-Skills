# source / `.` — 在当前 shell 执行脚本

> `source` 让脚本里的变量、函数、别名与目录切换直接作用于当前 shell，而不是留下一个子进程。

## 语法

```bash
source filename [arg ...]
. filename [arg ...]
. ./env.sh              # 相对路径必须显式写 ./，否则去 PATH 里找
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `source f` | 在当前 shell 依次读取并执行 `f` 的每一条命令 |
| `. f` | 与 `source` 完全等价，POSIX 写法，sh/dash 也能用 |
| `bash f` | 另起子 shell 执行，父 shell 环境不受影响 |
| `./f` | 依赖 shebang 与可执行位，同样是新进程 |
| `chmod +x f` | 加可执行位：`chmod +x` 后仍需 `./f` 而非 `f`（`.` 不在 PATH） |
| `$1 … $9` / `$@` / `$#` | source 时接收传入参数，用法与位置参数一致 |
| `$?` | source 结束后是最后一条命令的退出状态 |
| `$$` / `$BASHPID` | source 时 `$$` 仍是当前 shell 的 PID；子 shell 中 `$BASHPID` 才会变 |
| `${BASH_SOURCE[0]}` | 脚本自身路径，用于区分「被 source」与「被直接执行」 |

## 示例

同一份脚本，source 与执行对当前目录的影响完全不同：

```bash
printf 'cd /tmp\nVAR=hello\n' > probe.sh
. ./probe.sh && echo "$VAR $PWD"        # hello /tmp
bash ./probe.sh && echo "[$VAR] $PWD"   # [] 原目录
```

加载环境定义文件，参数按位置参数取用：

```bash
load() { . "$1" "${@:2}"; }
printf 'PORT=%s\n' '${1:-8080}' > port.sh
. ./port.sh 9090; echo "$PORT $?"
```

脚本同时支持「被 source」和「被运行」，靠 BASH_SOURCE 判断：

```bash
main() { echo "run with $*"; }
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"          # 直接执行才跑
fi
```

给文件加可执行位并确认解释器：

```bash
head -1 deploy.sh
chmod +x deploy.sh && ./deploy.sh prod
```

## 注意事项

- 脚本里的 `exit` 在 source 时会退出当前 shell（交互终端里就是关掉窗口），需要中断请用 `return`；被 source 的脚本不应 `exit`。
- `.` 会按 `PATH` 查找给定的裸文件名，写 `. ./name.sh` 才是明确的相对路径，找不到时报 `no such file or directory`。
- 被 source 的脚本继承当前 shell 的 `IFS`、函数与选项；若脚本自己 `set -euo pipefail`，这些开关会留在交互 shell 里继续生效，失败命令可能导致会话退出。
- shebang 只在通过内核 exec 直接运行时生效，source 会忽略它并用当前 shell 解释，bash 语法混进 dash 时行为立即改变。

## 相关命令

- `bash` / `sh` — 显式指定解释器新建子 shell 执行
- `exec` — 用新进程替换当前 shell，进程内不返回
- `chmod` — 设置可执行位
- `declare -f` — 查看 source 之后带进来的函数
- `type` — 确认命令来源（文件 / 函数 / 别名）
