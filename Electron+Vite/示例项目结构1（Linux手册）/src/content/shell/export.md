# export — 环境变量

> `export` 把 shell 变量标记为「导出」，只有导出的变量才会出现在子进程的环境表中。

## 语法

```bash
export [-f] [-n] [name[=value] ...]
VAR=value; export VAR      # POSIX 写法，sh/dash 同样可用
VAR=value command          # 只给 command 这一个进程设置变量
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `-f` | 导出函数而不是变量（bash 扩展，`export -f name`） |
| `-n` | 取消导出标记（`export -n VAR`），变量与值都还在 |
| `export` | 不带参数时列出全部已导出的变量与函数 |
| `declare -x` | 效果同 `export`，也可用于查看某变量的导出状态 |
| `unset VAR` | 连变量带导出属性一起删除 |
| `env` | 打印当前环境，或以指定环境执行命令；`env -i` 给出空环境 |
| `printenv NAME` | 查单个变量；未设置时返回非 0，比 `echo $NAME` 更易判断 |
| `readonly VAR=x` | 置为只读，之后不可改写也不可靠 `unset` 删除 |

## 示例

只赋值不 export 的变量是 shell 局部变量，子 shell 里读到空：

```bash
FOO=1
export BAR=2
bash -c 'echo "FOO=[$FOO] BAR=[$BAR]"'
```

临时前缀只影响一条命令，命令结束即消失，适合切换语言与代理：

```bash
LC_ALL=C sort merged.txt
http_proxy=http://127.0.0.1:8888 curl -sI http://example.com
echo "[$http_proxy]"
```

追加 `PATH` 的稳妥写法，`${PATH:+:$PATH}` 避免在原有值为空时留下多余冒号：

```bash
export PATH="$HOME/.local/bin${PATH:+:$PATH}"
env | grep '^PATH='
```

导出函数给子 bash（cron、systemd、非 bash 解释器都不认这种方式），末行用空环境验证脚本是否依赖隐含变量：

```bash
retry() { "$@" || { sleep 2; "$@"; }; }
export -f retry
bash -c 'retry pg_isready'
env -i PATH="$PATH" HOME="$HOME" bash --norc -c 'echo "LANG=[$LANG]"'
```

## 注意事项

- 导出只作用于之后 fork 出来的进程；修改 `export` 无法回改已在运行的程序，也不能影响父 shell。
- 值中含空格、`*` 或 `~` 时要在赋值处加引号；`export $FOO=1` 会先展开 `$FOO` 再当变量名，通常得到语法错误。
- `PATH` 以冒号结尾或开头等价于把当前目录列入搜索路径，属安全风险；命令查找按 `PATH` 从左到右，目录放前面即拥有优先权。
- `sudo` 默认启用 `env_reset`，普通变量不透传，只有 `env_keep` 列表内的（以及 `--set-env`/`VAR=x sudo cmd` 形式）能保留。

## 相关命令

- `env` — 查看/构造环境后执行命令
- `printenv` — 读取单个环境变量
- `set` — 列出当前 shell 的全部变量与函数（含未导出的）
- `unset` / `readonly` — 删除变量、设为只读
- `declare` — 指定属性声明变量（`-x`、`-a`、`-A`、`-i`）
