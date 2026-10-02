# chgrp — 变更文件所属组

> 只修改文件或目录的所属组，常与 setgid 目录、`newgrp`/`sg` 一起用来做团队共享权限。

## 语法

```bash
chgrp [选项] <组> <路径>...
chgrp [选项] --reference=模板文件 <路径>...
```

`<组>` 可以是 `/etc/group` 里的组名，也可以是 GID 数字；数字容易被当作名称解析失败时，先确认 `getent group <gid>`。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-R`, `--recursive` | 递归改目录树中每个条目 |
| `-h`, `--no-dereference` | 改符号链接本身而非目标 |
| `-c`, `--changes` | 只在确实变化时输出一行 |
| `-v`, `--verbose` | 每个文件都输出结果 |
| `-f`, `--silent` | 隐藏错误信息 |
| `--reference=RFILE` | 复制 `RFILE` 的所属组 |
| `--from=当前组` | 只改当前组匹配的条目 |
| `-H` / `-L` / `-P` | 递归时对符号链接：跟随命令行 / 全部跟随 / 都不跟随（默认） |

## 示例

私钥给 Web 服务读取，用组权限而不是 `0644`：

```bash
sudo chgrp ssl-cert /etc/ssl/private/wildcard.key
sudo chmod 0640 /etc/ssl/private/wildcard.key
```

团队共享目录三步走：改组、放开组写、置 setgid，使目录里新建的文件自动继承该组。

```bash
sudo chgrp -R developers /srv/project
sudo chmod -R g+rwX /srv/project
sudo chmod g+s /srv/project        # drwxrwsr-x = 2775
```

普通用户也能把自己的文件改到自己所在的组，不需要 root：

```bash
id -nG                             # 显示当前会话生效的组
chgrp deploy /home/alice/build.tgz # 仅当 alice 是 deploy 成员
```

`usermod -aG` 之后当前 shell 不会立刻拿到新组，`sg` 或 `newgrp` 可当场生效：

```bash
sg docker -c "docker ps"   # 只在这一条命令里带上 docker 组
newgrp developers          # 进入一个以 developers 为主组的新 shell
id -gn                     # 确认主组已变
```

## 注意事项

- 非 root 改组有两道门槛：对文件有写权限，且目标组必须是自己的成员组；否则报 `invalid group`。
- setgid 目录只决定新文件的「组」，写权限仍由 `umask` 决定；`umask 022` 下新建文件是 `0644`，队友无法覆盖，建议共享会话里用 `umask 002`。
- `chgrp -R` 默认不跟随目录树中的符号链接，指向别处部署目录的 `current` 链接不会被改，必要时显式加 `-L`（注意可能改到共享目标）；批量改 cron 或 systemd 服务运行目录前先 `ls -l` 记录原状。
- `newgrp` 是否需要组口令取决于 `/etc/gshadow`：成员免口令，非成员需口令；字段为 `!` 表示未设口令，此时非成员直接被拒。

## 相关命令

- `newgrp` / `sg` — 切换或临时使用某个组
- `groupmems` / `getent group` — 查看组成员列表
- `chmod` — 设置组权限位（`g+rwx`、`0640`）
- `chown` — 同时改属主与所属组
- `id` — 核对 uid/gid 与附加组是否真的生效
