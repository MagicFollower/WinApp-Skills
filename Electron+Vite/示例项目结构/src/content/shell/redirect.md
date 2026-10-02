# 重定向与管道

> 把 stdin/stdout/stderr 接到文件、设备或其他命令上，是 shell 组合小程序的基本胶合方式。

## 语法

```bash
cmd < in.txt              cmd > out.txt          cmd >> out.txt
cmd 2> err.txt            cmd > all.txt 2>&1     cmd &> all.txt
cmd1 | cmd2               cmd <<< "str"          cmd <<EOF ... EOF
cmd < <(gen)              cmd > >(sink)          exec 3<> rw.txt
set -C                    # noclobber：> 不覆盖已有文件，需写 >| 强制覆盖
```

## 常用选项

| 名称 | 说明 |
| --- | --- |
| `>` 与 noclobber | 重定向 stdout 并截断；`set -C` 开启后 `>` 拒绝覆盖已有文件，需上面那种强制写法 |
| `>>` | 追加写入，不存在则创建 |
| `2>` | 只重定向 stderr（fd 2）；数字与 `>` 之间不留空格，否则 `2` 变成参数 |
| `2>&1` / `1>&2` | 让 stderr 跟随 stdout 当前的目标 / 让 stdout 走 stderr（顺序敏感） |
| `&>` / `&>>` | bash 扩展，一次捕获两路输出；dash 不支持 |
| `<` / `<<<` / `<<` / `<<-` | 文件作 stdin / here-string / here-document，`<<-` 只去掉行首 Tab |
| `n<&-` / `n>&-` | 关闭某个 fd，如 `exec 3<&-` |
| `<(cmd)` / `>(cmd)` | 进程替换，给出 `/dev/fd/N` 形式的文件名 |

## 示例

顺序决定结果：`> f 2>&1` 让 stderr 跟随 stdout，`2>&1 > f` 时 stderr 仍打在终端；管道只传 stdout，想连 stderr 一起交给下游必须显式合并：

```bash
make > build.log 2>&1
make 2>&1 > build.log          # 错误信息仍然打在终端上
ping -c 3 host.invalid 2>&1 | grep -i 'unknown host'
tar czf - /etc 2> tar.err | wc -l
```

`tee` 边写文件边显示，配合进程替换可分别落盘两路输出：

```bash
df -h | tee disk.txt
make install > >(tee install.out) 2> >(tee install.err >&2)
```

here-document 用引号包定界符即可原样输出，不展开 `$` 与反引号；`<<-` 便于脚本缩进：

```bash
cat > /tmp/x.sh <<-'EOF'
	echo "HOME=$HOME 不会被展开"
EOF
cat /tmp/x.sh
```

管道里的循环在子 shell 中运行，变量带不出来；进程替换或 here-string 可保持当前 shell：

```bash
while IFS= read -r f; do ((n++)); done < <(find . -maxdepth 1 -type f)
echo "$n files"
```

## 注意事项

- 重定向在命令执行前完成，且作用于当前命令；`cd > f` 这类内建没有输出，`f` 只会被创建成空文件。
- `&>`、`<<<`、`<(...)`、`>|` 都是 bash 扩展，POSIX sh/dash 脚本里请改回 `> f 2>&1` 与传统临时文件。
- 进程替换依赖 `/dev/fd`，精简容器或 chroot 里可能不可用；`>(...)` 的写入还可能晚于命令结束，需要 `wait` 才拿到完整文件。
- 重定向由 shell 自己完成，目标路径打不开时报错来自 shell 本身（`bash: f: Permission denied`）而不是命令输出，此时 fd 2 通常仍指向终端。

## 相关命令

- `tee` — 分流写入并同时显示
- `exec` — 为整个 shell 重定向或开闭 fd
- `xargs` — 把 stdin 转成参数而非管道输入
- `mapfile` — 一次把多行读进数组，替代 while read 循环
- `stdbuf` — 调整下游命令的缓冲模式，解决管道输出延迟
