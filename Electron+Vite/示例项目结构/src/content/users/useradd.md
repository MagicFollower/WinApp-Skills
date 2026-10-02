# useradd / userdel — 创建与删除账号

> `useradd` 写入 `/etc/passwd`、`/etc/shadow`、`/etc/group` 建立账号，`userdel` 反向删除；家目录与遗留文件需要额外处理。

## 语法

```bash
useradd [选项] 账号
userdel [选项] 账号
useradd -D                     # 显示全局默认值；带选项时写回 /etc/default/useradd
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-m` / `-M` | 创建家目录并复制 `/etc/skel`（权限由 `HOME_MODE`/`UMASK` 决定）/ 强制不建，覆盖 `login.defs` 的 `CREATE_HOME` |
| `-d DIR`, `-k SKEL` | 指定家目录路径（需配合 `-m` 才创建）/ 换骨架目录 |
| `-s SHELL` | 登录 shell 绝对路径，服务账号常用 `/usr/sbin/nologin` |
| `-g GROUP` | 主组名或 GID，组必须已存在 |
| `-G G1,G2` | 附加组列表，逗号分隔不留空格，组都必须存在 |
| `-c COMMENT`, `-e DATE`, `-f DAYS` | GECOS 备注 / 账号失效日期 `YYYY-MM-DD` / 口令过期后的宽限天数 |
| `-r` | `useradd`：系统账号，UID 取 `SYS_UID_MIN..SYS_UID_MAX`，不做口令老化；`userdel`：连同家目录与邮件池一起删 |
| `-u UID`, `-o` | 指定 UID / 允许与已有 UID 重复 |
| `-U` / `-N` | 创建 / 不创建与账号同名的私有组，行为受 `USERGROUPS_ENAB` 影响 |
| `-K KEY=VALUE` | 临时覆盖 `login.defs`，如 `-K UID_MIN=5000` |

## 示例

给应用建一个不可交互登录的系统账号，家目录随后单独创建：

```bash
sudo groupadd -r appgroup
sudo useradd -r -g appgroup -d /var/lib/appuser -s /usr/sbin/nologin \
  -c "MyApp worker" appuser
sudo install -d -o appuser -g appgroup -m 0750 /var/lib/appuser
```

真人账号：建家目录、给两个附加组、要求首次登录改口令。

```bash
sudo useradd -m -s /bin/bash -G developers,docker -c "Alice Ops" alice
sudo passwd -e alice
sudo useradd -D   # 核对默认骨架、shell、UMASK 等
```

沿用已有主组、指定 UID 与家目录（容器或复用旧数据盘时常见）：

```bash
sudo useradd -u 1500 -g developers -m -d /home/jdoe -k /etc/skel-project jdoe
```

删除前先确认没有遗留进程和文件，再决定是否让 `userdel` 连带清理：

```bash
ps -u bob -o pid,user,cmd
find / -xdev -user bob -print 2>/dev/null | head
sudo userdel -r bob
sudo userdel -f -r carol   # 账号仍在线时强制删除
```

## 注意事项

- `useradd` 不会设置口令，新建账号的口令字段是 `!!`，无法用口令登录；要么 `passwd 账号`，要么保持锁定并用 `sudo -u 账号` 进入。
- Debian/Ubuntu 的 `adduser` 是交互式 Perl 封装（`adduser --disabled-password --gecos "" alice`），RHEL/Fedora 的 `adduser` 只是指向 `useradd` 的链接，跨发行版脚本应按 `useradd` 写。
- `userdel -r` 只删家目录和邮件池：`/var/spool/cron/crontabs`、`/tmp`、共享目录里该 UID 的文件都要手工清点，删完用 `find / -nouser` 复查孤儿 UID；同名私有组只有在没有其他成员时才会一起删掉。
- 普通账号 UID 起点由 `login.defs` 的 `UID_MIN` 决定（现代发行版多为 1000），系统账号在其之下；账号删除后 UID 可能被新账号复用，残留文件会被「继承」。

## 相关命令

- `usermod` — 改组、改 shell、改登录名与过期设置
- `passwd` — 设口令、强制过期、锁定
- `groupadd` / `groupdel` / `groupmems` — 组侧操作
- `id` / `getent passwd` — 校验账号与组成员关系
- `skel` 目录（`/etc/skel`）— 新家目录的默认文件来源
