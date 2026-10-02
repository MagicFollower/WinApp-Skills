# ssh — 远程登录与端口转发

> OpenSSH 客户端，提供密钥登录、跳板机串联以及本地、远程、动态三类端口转发。

## 语法

```bash
ssh [ OPTIONS ] [user@]hostname [ command ]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-p`, `--port` | 远端 SSH 端口，默认 22 |
| `-i`, `--identity-file` | 指定私钥文件 |
| `-J`, `--jump-host` | 经跳板机直连目标，等价于配置里的 `ProxyJump` |
| `-L [bind:]port:host:hostport` | 本地转发：本机端口映射到远端可达地址 |
| `-R [bind:]port:host:hostport` | 远端转发：在对端开放端口回连本机 |
| `-D port` | 动态 SOCKS5 代理 |
| `-N`, `-f` | 不执行远端命令 / 认证成功后转入后台 |
| `-o`, `--option` | 单条配置项，如 `-o StrictHostKeyChecking=accept-new` |
| `-v` | 打印握手与认证细节，登录失败时先看它 |
| `-A`, `-X` | 转发 ssh-agent / X11，风险较高按需使用 |

## 示例

首次连接内网机器，10 秒建连超时；生产环境用 `accept-new` 而不是 `no`：

```bash
ssh -p 2200 -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 ops@10.0.0.21
```

两跳穿透，`-i` 指定的私钥对每一跳都生效，各跳密钥不同请回到配置文件写：

```bash
ssh -i ~/.ssh/id_ed25519 -J ops@10.0.0.2 ops@10.0.0.30
```

把远端只监听 127.0.0.1 的数据库拉到本地调试，`-N` 表示只转发不开 shell：

```bash
ssh -N -L 15432:127.0.0.1:5432 ops@10.0.0.21
```

无公网 IP 的机器反向注册端口（需服务端 `GatewayPorts clientspecified`），以及浏览器走 SOCKS 出口：

```bash
ssh -f -N -R 2222:127.0.0.1:22 ops@example.com
ssh -f -N -D 1080 ops@10.0.0.2
```

公钥用 `ssh-copy-id -i ~/.ssh/id_ed25519.pub -p 2200 ops@10.0.0.21` 落盘，常用参数固化进 `~/.ssh/config`：

```text
Host bastion-10
    HostName 10.0.0.2
    User ops
    IdentityFile ~/.ssh/id_ed25519
    ServerAliveInterval 30
    ServerAliveCountMax 3
```

## 注意事项

- `StrictHostKeyChecking=no` 会静默接受伪造的 host key，仅限自动化重建场景；出现密钥变更告警先 `ssh-keygen -R [10.0.0.21]:2200` 再核对指纹。
- 权限不对 sshd 会直接忽略公钥：私钥 `600`、`~/.ssh` `700`、远端 `authorized_keys` `600`，用 `-v` 可看到 `Offering public key` 后是否被拒。
- 转发受服务端 `AllowTcpForwarding`、`PermitOpen`、`GatewayPorts` 限制；1024 以下端口需 root 才能绑定。
- `ssh_config` 里同一参数以第一次出现的值为准，把具体 Host 段写在 `Host *` 之前。

## 相关命令

- `scp` / `sftp` — 复用同一 SSH 通道传输文件
- `ssh-keygen` — 生成密钥、清理 known_hosts、换指纹
- `ss` — 确认本地转发端口确实处于 LISTEN
