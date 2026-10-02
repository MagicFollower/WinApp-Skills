# history — 命令历史的查看与复用

> `history` 列出当前会话内存中的命令，配合 `!` 展开与 `Ctrl+R` 可以完全不重打长命令。

## 语法

```bash
history [n]
history [-c] [-d offset] [n]
history [-anrw] [filename]
history [-p arg ...] | [-s arg ...]
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `history` / `history 20` | 按编号列出内存中的历史 / 只列最后 20 条 |
| `-c` | 清空当前会话的历史列表，不动磁盘文件 |
| `-d n` | 删除第 n 条，负数从末尾计数 |
| `-w` | 把内存列表整体写入 `HISTFILE`（覆盖） |
| `-a` / `-n` / `-r` | 追加本次新增行 / 读入文件新行 / 用文件内容替换内存列表 |
| `-p` / `-s` | 只对参数做历史展开并打印 / 把参数作为一条命令塞进列表末尾 |
| `!!` / `!$` / `!n` | 上一条命令 / 上一条的最后一个参数 / 第 n 条命令 |
| `!?str?` / `^old^new` | 按子串检索最近一条 / 快速替换上一条中的片段 |
| `HISTSIZE` / `HISTFILESIZE` | 内存中、文件中各保留多少条 |
| `HISTCONTROL` / `HISTTIMEFORMAT` | `ignorespace`、`ignoredups`、`ignoreboth`、`erasedups` / 非空则输出时间戳，如 `"%F %T  "` |

## 示例

重复上一条命令、复用上一个参数（提权与回滚最常用）：

```bash
apt-get install nginx
sudo !!
cat /etc/nginx/nginx.conf
vi !$
```

从历史里按编号或子串精确取一条，`history -p` 可先看展开结果再决定执行：

```bash
history | grep --color=auto docker
!42
!?compose?
history -p "!?compose?"
```

跨终端共享历史（追加写 + 立即读回，避免后开的 shell 覆盖前一个），以及清理误输入的密码行后落盘：

```bash
shopt -s histappend
export PROMPT_COMMAND='history -a; history -n'
history -d -3
history -w
history -c          # 只清内存，不动文件
```

## 注意事项

- 每个交互 bash 各持一份内存历史，默认在退出时整体覆盖 `HISTFILE`，多终端并开会互相冲掉，需 `histappend` + `history -a`。
- `!` 展开发生在双引号内，单引号内不展开；要输出字面 `!` 用 `\!` 或临时 `set +H` 关闭展开，脚本里通常应关闭。
- `HISTCONTROL=ignorespace` 要求命令以一个空格开头，`erasedups` 会重排列表使编号不再稳定。
- `history -c` 之后旧命令仍留在文件里，除非再执行 `history -w`；已运行进程的环境变量与凭据不会被 history 抹掉。

## 相关命令

- `fc` — 打开编辑器批量修改并重放一段历史
- `bind` — 改 `Ctrl+R` 等检索键的绑定
- `set` — `set +H` 关闭历史展开，`set -o history` 开关记录
- `shopt` — `histappend`、`cmdhist`、`dotglob` 等 shell 行为
