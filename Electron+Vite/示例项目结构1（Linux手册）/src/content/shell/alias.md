# alias — 命令别名

> `alias` 给一条命令起短名，只在命令行读取时做文本替换，不改变参数传递方式。

## 语法

```bash
alias [name[=value] ...]
alias -p
unalias [-a] [name ...]
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `alias` / `alias -p` | 列出全部已定义别名（`alias name='value'` 的可复用格式） |
| `alias ll='ls -alF'` | 定义；值要整体加引号，否则空格后的部分被当成另一个别名名 |
| `unalias ll` / `unalias -a` | 删除单个别名 / 清空全部（含发行版自带的 `rm -i`） |
| `\ll` / `command ll` | 绕过别名展开，调用真实命令或函数 |
| `type -a ls` | 查看 `ls` 到底是别名、函数还是文件 |
| `shopt -s expand_aliases` | 在脚本中开启别名展开（默认仅交互式 shell） |

## 示例

按「前缀替换」理解别名：命令后的参数总是追加在别名值之后，无法插到中间。

```bash
alias tailf='tail -f'
tailf /var/log/syslog        # 实际执行 tail -f /var/log/syslog
```

值里带引号或变量时用单引号，避免定义时就发生展开；值以一个空格结尾时，其后第一个词会继续参与别名展开：

```bash
alias please='sudo $(fc -ln -1)'   # 用 sudo 重跑上一条命令
alias grep='grep --color=auto'
alias proxy='https_proxy=http://127.0.0.1:7890 '
```

参数需要出现在命令中间，或需要判断 `$?`、循环、局部变量时，改用函数：

```bash
mkcd() { mkdir -p -- "$1" && cd -- "$1"; }
alias ..='cd ..'
alias ...='cd ../..'
type -a mkcd
```

持久化写入 `~/.bashrc`，注意放在非交互提前 return 的守卫之后：

```bash
cat >> ~/.bashrc <<'EOF'
alias g='git'
alias gs='git status -sb'
EOF
source ~/.bashrc && alias gs
```

## 注意事项

- 别名不是环境变量，不会被子 shell 或 `bash -c` 继承；要让远程/子进程复用得写函数并 `export -f`，或直接放进脚本。
- 脚本里即使 `shopt -s expand_aliases`，展开时机仍是读取该行时，同一复合语句内刚定义的别名要用到下一行才生效。
- 发行版常预置 `alias rm='rm -i'`、`alias cp='cp -i'`，脚本不受影响（因为不展开），但交互式手工删除的行为会变，必要时 `command rm`。
- `unalias -a` 后交互 shell 会失去防误删保护，重新加载 `~/.bashrc` 即恢复。

## 相关命令

- `unalias` — 删除别名定义
- `type` — 分辨别名 / 函数 / 内建 / 可执行文件
- `command` — 跳过别名与函数直接调用内建或外部命令
- `declare -f` — 打印函数定义，与 `alias -p` 对应
- `shopt` — `expand_aliases` 等开关
