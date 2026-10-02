# usermod — 修改已有账号的组、shell 与登录名

> 调整账号属性：附加组、主组、登录 shell、家目录、口令锁定与过期；改组时 `-aG` 与 `-G` 差别极大。

## 语法

```bash
usermod [选项] 账号
usermod -aG 组名 账号
usermod -l 新登录名 旧登录名
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-aG G1,G2` | **追加**附加组，`-a` 只能与 `-G` 一起使用 |
| `-G G1,G2` | 用给定列表**替换**全部附加组，漏写 `-a` 会把账号踢出其它组 |
| `-g GROUP` | 改主组，名称或 GID，组必须已存在；不会重写已有文件的所属组 |
| `-l NEW` | 改登录名；家目录路径与同名私有组不会自动跟着改 |
| `-d DIR` + `-m` | 改家目录路径；`-m` 把现有家目录内容移动到新的 `<DIR>` |
| `-s SHELL`, `-c`, `-e`, `-f` | 登录 shell 绝对路径 / GECOS 备注 / 失效日期 `YYYY-MM-DD` / 口令过期后宽限天数 |
| `-L` / `-U` | 锁定口令（`/etc/shadow` 字段前加 `!`）/ 解锁 |
| `-u UID`（可配 `-o`） | 改 UID；家目录内的属主自动更新，目录之外的文件要手动处理 |

## 示例

追加用 `-aG`；漏了 `-a` 的 `-G` 是「替换」，成员关系只剩列表里那几个组：

```bash
sudo usermod -aG docker alice
sudo -u alice id -nG      # 数据库里已是新集合
id -nG                    # 当前这个 shell 仍看不到 docker
sudo usermod -G developers alice              # 反面例子：sudo 组被抹掉
sudo usermod -aG sudo,docker,developers alice # 手工补回
```

改名标准流程：账号名、组名、家目录分三步（前提是账号未登录、无运行进程）。

```bash
sudo usermod -l svc-backup backup
sudo groupmod -n svc-backup backup
sudo usermod -d /home/svc-backup -m svc-backup
```

离职停用，一条命令同时锁口令、换 shell、置失效日期：

```bash
sudo usermod -L -s /usr/sbin/nologin -e 1970-01-01 bob
sudo pkill -u bob
```

改主组不会重写已有文件的所属组，需要补一刀；改 UID 同理要处理家目录之外的文件：

```bash
sudo usermod -g developers alice
sudo chgrp -R --from=dev-team developers /home/alice
sudo usermod -u 1600 carol
sudo find / -xdev -uid 1500 -exec chown 1600 {} +
```

## 注意事项

- 改登录名或 UID 时若账号仍有进程/会话，`usermod` 会拒绝并报账号正被使用；先 `loginctl terminate-user 账号` 或 `pkill -u 账号`。
- 组成员关系在登录时由 `initgroups` 计算，`usermod -aG` 之后必须重新登录（或新开 `su - 账号`、`newgrp`）才生效，已在跑的 systemd 服务与 tmux 会话不会自动获得新组。
- `-L` 只封口令认证：SSH 公钥、`sudo` 的 `NOPASSWD` 策略、cron 任务照常工作；要真正停用还得清 `~/.ssh/authorized_keys` 并改 shell，而 `-s` 写错路径会直接把人关在门外，先确认文件存在（Debian 为 `/usr/sbin/nologin`，老 RHEL 为 `/sbin/nologin`）。若口令字段本就没有可用口令，`-U` 也解不开，需 `passwd` 重设。
- `-l` 改名后，`/etc/sudoers.d/` 条目、`/var/spool/cron/crontabs/` 文件名、目录 ACL（`getfacl` 里的 `user:旧名:`）和日志解析规则都要同步，否则留下按旧名放行的僵尸授权。

## 相关命令

- `useradd` / `userdel` — 建号与删号
- `groupmod` / `groupadd` — 组侧改名与新建
- `passwd` / `chage` — 口令与有效期细节
- `id` / `groups` — 校验改动是否已在会话中生效
- `loginctl` — 结束该账号的登录会话
