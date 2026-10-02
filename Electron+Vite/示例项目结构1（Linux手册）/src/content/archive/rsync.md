# rsync — 增量同步与传输

> rsync 只传输内容有差异的部分，可在本地、跨主机（走 ssh）或从 rsync 守护进程同步，`--delete` 让目标与源保持一致。

## 语法

```bash
rsync [选项] 源路径 目标路径
rsync -avh /data/src/ /backup/src/
rsync -avhe ssh /data/src/ user@10.0.0.5:/backup/src/
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-a`, `--archive` | 归档模式，等于 `-rlptgoD`：递归、保留软链接/权限/时间/属组/属主/设备文件 |
| `-v` / `-h` | 显示传输的文件 / 以 K、M、G 显示数字 |
| `-z`, `--compress` | 传输时压缩，适合带宽小的链路；对已压缩数据无益 |
| `-n`, `--dry-run` | 只打印将要执行的动作，不改任何文件 |
| `--progress` / `--stats` | 显示每个文件的进度（整体进度用 `--info=progress2`，rsync 3.1+）/ 结束时输出文件数、字节数、加速比汇总 |
| `--delete` | 删除目标里有、源里没有的文件；`--delete-excluded` 连被排除的也删；`--max-delete=NUM` 限定删除数量上限，超过即中止 |
| `--exclude=PATTERN` / `--include=PATTERN` | 排除/保留规则按书写顺序匹配，`--include` 要写在对应 `--exclude` 之前 |
| `-e`, `--rsh=COMMAND` | 指定通道，如 `-e "ssh -p 2222"`；`--rsync-path="sudo rsync"` 让远端以 root 运行 |
| `-c`, `--checksum` / `--partial` | 用校验和代替「大小+时间」判断变化 / 保留中断的半成品文件以便续传 |
| `-H` / `-A` / `-X` / `-S` | 保留硬链接 / ACL / 扩展属性 / 稀疏文件 |
| `--link-dest=DIR` | 与参照目录做硬链接，用来生成几乎不占空间的快照 |
| `--bwlimit=RATE` / `-u` | 限速（如 `--bwlimit=20M`）/ 跳过目标上更新的文件 |

## 示例

尾部斜杠是最大差异点：`src/` 同步目录里的内容，`src` 会把 `src` 这层目录本身建到目标下。

```bash
rsync -avh /data/src/ /backup/src/       # 内容是 /backup/src/*
rsync -avh /data/src  /backup/           # 结果是 /backup/src/*
```

先演练再执行，确认 `--delete` 的影响范围：

```bash
rsync -avn --delete --stats /data/src/ /backup/src/
rsync -avh --delete --max-delete=200 /data/src/ /backup/src/
```

排除运行时数据，同时保留必须同步的那份日志（顺序敏感的 include/exclude）：

```bash
rsync -avh /data/src/ /backup/src/ \
  --include='important.log' --exclude='*.log' \
  --exclude='.git/' --exclude='node_modules/'
```

走 ssh 同步到远端（远端同样要装 rsync），大文件用整体进度并限速；每日快照则以昨天目录为 `--link-dest` 参照，未变的文件只多一个硬链接：

```bash
rsync -avh -e "ssh -p 2222 -i ~/.key/id_ed" src/ deploy@10.0.0.5:/srv/app/
rsync -avh --info=progress2 --bwlimit=50M vm.qcow2 backup@10.0.0.9:/mnt/nas/
rsync -avh --delete --link-dest=/snap/latest/ /data/ /snap/$(date +%F)/
ln -sfn /snap/$(date +%F) /snap/latest
```

## 注意事项

- `--delete` 只作用于被同步的目录，但源路径写错或目录为空时目标会被清空：务必先 `-n` 演练，生产任务再加 `--max-delete`。
- `--exclude` 匹配到的目标文件默认受保护、不会被 `--delete` 删除；要用 `--delete-excluded` 或 `--filter` 的 `protect`/`sender` 修饰符表达更细的语义。
- `-a` 含 `-o`/`-g`，非 root 执行会报 `chown` 相关警告但文件仍传成功；不需要属主信息时加 `--no-o --no-g`。`-c` 逐块算校验和，比默认的大小+时间判断慢得多，只在怀疑目标数据已被改动时使用。
- 退出码 23 表示有文件传输失败，24 表示源文件在传输过程中消失，脚本里应区分处理而不是当成成功。

## 相关命令

- `tar` + `ssh` — 一次性整目录搬运，没有增量与删除能力
- `scp` / `sftp` — 只做整文件覆盖，不比较差异
- `split` — 目标介质有单文件大小上限时先切分再同步
- `md5sum` — 同步后做端到端抽查校验
