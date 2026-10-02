# read — 从标准输入读取

> `read` 每次取一行（或指定长度）存入变量，是脚本交互与逐行处理文本的入口。

## 语法

```bash
read [-ers] [-a array] [-d delim] [-n nchars] [-p prompt] [-t timeout] [-u fd] [name ...]
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `name ...` | 变量列表，按 `IFS` 依次切分；不写变量名时整行进 `REPLY`，`-a ARR` 存成数组 |
| `-p "提示"` | 打印提示；只有 stdin 是终端时才与输入处在同一行 |
| `-r` | 不把反斜杠当转义或续行处理，读任何文本都该加 |
| `-s` | 静默读取、不回显输入（密码提示） |
| `-n N` / `-N N` | 读满 N 字符即返回 / 严格读满 N 字符；终端上 `-n 1` 不必等回车 |
| `-d CHAR` | 用 CHAR 取代换行作为结束符，`-d ''` 表示读至 NUL |
| `-t SEC` | 超时立刻返回非 0，SEC 允许小数 |
| `-u FD` / `-e` | 从指定描述符读取 / 用 readline 编辑本行（方向键、`Ctrl+A/E`） |

## 示例

带默认值的提问，以及不回显的密码读取：

```bash
read -r -p "目标目录 [/tmp]: " dir
echo "使用 ${dir:-/tmp}"
read -r -s -p "Password: " pass; echo
```

`IFS` 决定切分方式，多余的字段整体落入最后一个变量：

```bash
IFS=: read -r user pw uid gid rest < <(getent passwd root)
echo "$user -> uid $uid"
IFS= read -r line <<< "   保留首尾空白   "
```

逐行遍历文件；末行没有换行时 `read` 返回非 0，必须补一次判空才不丢尾行：

```bash
while IFS= read -r line || [[ -n "$line" ]]; do
  printf '%s\n' "$line"
done < /etc/passwd
```

含空格或换行的文件名用 `-d ''` 配 `print0`；here-document 也能直接喂给 `read`：

```bash
while IFS= read -r -d '' f; do printf 'file: %s\n' "$f"; done < <(find . -print0)
read -r a b c <<'EOF'
1 2 3
EOF
```

## 注意事项

- 不加 `-r` 时输入里的 `\` 会被当转义吃掉；`read` 处在 `cmd | while read` 中会吞掉整条管道，改用 here-string、进程替换或 `read -u 3` 另开描述符。
- 到达 EOF 时 `read` 返回非 0 但变量仍被赋值，循环条件写成 `|| [[ -n "$line" ]]`；脚本被管道喂数据时 `-p`、`-s` 的交互效果依赖 tty。
- 脚本改动 `IFS` 后 `read` 的切分结果随之改变，用完应恢复；`-t` 超时后已读到的部分内容仍在变量里。

## 相关命令

- `mapfile` / `readarray` — 一次把整个输入读进数组
- `getopts` — 解析命令行选项
- `exec` — `exec 3< file` 开描述符供 `read -u 3` 使用
