# id — 查看账号的 UID、GID 与所属组

> 打印真实/有效用户 ID、主组与全部附加组，是排查「到底以什么身份在跑」的第一条命令。

## 语法

```bash
id [选项] [账号]
id -Gn [账号]
```

不带账号时作用于当前进程；带账号时查询该账号在 NSS（`/etc/passwd`、`/etc/group`，含 LDAP/SSSD）中的记录。`-n` 不能单独使用，必须搭配输出类选项，否则直接报错退出。

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-u`, `--user` | 只输出有效 UID |
| `-g`, `--group` | 只输出有效主组 GID |
| `-G`, `--groups` | 输出主组和全部附加组 ID，空格分隔 |
| `-n`, `--name` | 与 `-u`/`-g`/`-G`/`-Z` 配合，输出名称而非数字 |
| `-r`, `--real` | 输出真实 ID 而不是有效 ID |
| `-Z`, `--context` | 输出 SELinux 安全上下文（未启用 SELinux 的系统会报错） |

## 示例

默认输出把三类信息都带上，便于人工核对：

```bash
id
uid=1000(alice) gid=1000(alice) groups=1000(alice),27(sudo),1001(developers)
```

脚本里判断是否 root，同时确认服务账号的附加组真的到位：

```bash
[[ "$(id -u)" -eq 0 ]] || { echo "需要 root 权限" >&2; exit 1; }
id www-data
id -Gn deploy | grep -qw www-data && echo "deploy 在 www-data 组里"
```

切换身份后立即验证，避免把环境差异当成故障：

```bash
sudo -u postgres id -un
su - appuser -c 'id -Gn'
```

权限被拒时用 `namei -l` 逐级看路径每个目录的属主与权限，输出里出现 `drwx------ jenkins` 这类中间目录，说明卡在父目录缺少 `x`（进入）权限，而不是文件本身：

```bash
namei -l /var/lib/jenkins/.ssh/authorized_keys
namei -m /srv/project/logs/audit.log     # 只显示每级权限位
```

## 注意事项

- `id` 读的是账号数据库，不等于某个正在运行的进程的实际凭证；核对进程请用 `grep -E '^(Uid|Gid|Groups)' /proc/<pid>/status`。
- 通过 `usermod -aG` 或改 `/etc/group` 新增的组成员关系，只对新登录会话生效；当前 shell 里 `id -nG` 仍看不到，需重新登录、`su - 账号` 或 `newgrp`。
- `id 不存在的账号` 返回非零并输出 `no such user`，可以当作账号存在性检查，但更清晰的写法是 `getent passwd 账号`。
- setuid 程序里真实 ID 与有效 ID 不同，需要两者对比时用 `id -ru` 与 `id -u`。

## 相关命令

- `whoami` — 只看当前有效用户名
- `groups` — 只列组名，`groups 账号` 可查他人
- `getent passwd` / `getent group` — 直接查 NSS 条目
- `namei` — 逐级展示路径的属主、权限与链接指向
- `ps -o pid,user,group` — 看进程实际使用的凭证
