# cat — 输出文件内容、合并与写入

> 把文件原样写到标准输出；可一次拼接多个文件，也能用 heredoc 现场写出小文件。

## 语法

```bash
cat [选项] [文件...]
cat > 目标文件 <<'EOF'
要写入的内容
EOF
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-n`, `--number` | 给所有输出行编号，空行也占号 |
| `-b`, `--number-nonblank` | 只给非空行编号，空行不编号 |
| `-s`, `--squeeze-blank` | 连续两个以上空行压成一个 |
| `-v`, `--show-nonprinting` | 不可打印字节用 `^` 和 `M-` 记法显示 |
| `-E`, `--show-ends` | 每行末尾显示 `$`，用于发现 CRLF |
| `-T`, `--show-tabs` | 制表符显示为 `^I` |
| `-A`, `--show-all` | 等价于 `-vET`，排查行尾与编码最省事 |

不写文件参数时 `cat` 读标准输入，文件参数写 `-` 效果相同，按 `Ctrl-D` 结束输入。

## 示例

拼接多个片段生成一份文档，`>` 会先把目标清空：

```bash
cat header.md body.md footer.md > README.md
```

加行号后只看末尾几行，`-n` 的编号跨文件连续累加：

```bash
cat -n /var/log/nginx/access.log | tail -n 5
```

heredoc 写配置文件；定界符加单引号时 `$HOST` 原样保留，去掉引号才会被 shell 展开：

```bash
cat > deploy.env <<'EOF'
HOST=10.0.0.12
PORT=8080
EOF
```

不带引号的定界符配合命令替换，把当前时间追加到日志末尾；行尾与制表符用 `-A` 检查：

```bash
cat >> run.log <<EOF
$(date '+%F %T') heartbeat ok
EOF
cat -A conf/app.ini
```

## 注意事项

- `cat f > f`、`cat *.log > all.log` 这类目标文件自身也在输入里的写法会把输出截断成空文件；就地合并请借助 `sponge`（moreutils）或先写临时文件。
- heredoc 的结束定界符必须单独一行且顶格；只有写成 `<<-` 才允许行首缩进，而且只吃 Tab 不吃空格。
- 对二进制或超大文件直接 `cat` 到终端会刷屏、甚至改写终端状态，改用 `less` 或 `head`。
- `cat` 不会补齐换行：最后一个输入行不带换行符时，下一段内容会被黏在同一行。

## 相关命令

- `tac` — 按行倒序输出，`cat` 的反面
- `head` — 只看文件开头若干行或字节
- `tee` — 同时写入文件和标准输出
