# tail — 查看文件结尾并跟踪日志追加

> 默认输出末尾 10 行；`-f` 持续跟踪新写入的行，`-F` 还能扛住日志轮转。

## 语法

```bash
tail [选项] [文件...]
tail -n 100 app.log
tail -f app.log
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-n`, `--lines=NUM` | 输出末尾 NUM 行，默认 10 行；`-n +NUM` 表示从第 NUM 行开始一直输出到结尾 |
| `-c`, `--bytes=NUM` | 按字节取末尾，`-c +NUM` 同样是从第 NUM 个字节开始 |
| `-f`, `--follow` | 跟踪文件内容增长，默认等价 `--follow=descriptor` |
| `-F` | 等价 `--follow=name --retry`，文件被改名或删除后自动重新打开 |
| `--retry` | 文件暂时打不开时继续重试，常与 `--follow=name` 搭配 |
| `-s`, `--sleep-interval=N` | 配合 `-f` 设定两次检查之间的间隔秒数，默认约 1 秒 |
| `--pid=PID` | 配合 `-f`，指定的进程退出后 `tail` 也随之退出 |

## 示例

先看最近 200 行确认问题范围：

```bash
tail -n 200 /var/log/nginx/error.log
```

实时跟踪，`Ctrl-C` 退出：

```bash
tail -f /var/log/app.log
```

`-F` 应对 logrotate：文件被改名成 `app.log.1` 并新建 `app.log` 后仍能继续跟踪，服务重启后也会自动重开：

```bash
tail -F -n 50 /var/log/app.log
```

从第 50 行开始打印全文，跳过表头看数据段：

```bash
tail -n +50 report.csv
```

跟到被监控进程退出为止，适合放在启动脚本里：

```bash
tail -f --pid=$APP_PID out.log
```

## 注意事项

- `tail -f`（descriptor 方式）遇到日志轮转就失效：旧文件被改名后它还在跟旧 inode，生产环境请用 `-F`。
- `-F` 需要目录和文件都可读，且文件一开始不存在时靠 `--retry` 等待出现；权限不足会直接报错退出。
- 取末尾 NUM 行需要定位到文件结尾再往回读；`tail -n +NUM` 则是从第 NUM 行顺序读到结尾，两者语义相反，别写混。
- 跟踪中的日志接管道过滤时要用 `grep --line-buffered`，否则 grep 的缓冲会让结果成批延迟出现。

## 相关命令

- `less +F` — 分页状态下跟踪日志
- `head` — 查看文件开头
- `grep --line-buffered` — 实时过滤跟踪中的日志
