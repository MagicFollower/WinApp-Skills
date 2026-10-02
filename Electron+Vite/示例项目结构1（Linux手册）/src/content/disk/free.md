# free — 内存与 swap 使用情况

> 一条快照给出 RAM 与 swap 的总量/已用/可用，关键是读懂 `available` 而不是 `free`。

## 语法

```bash
free [选项]
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-h`, `--human-readable` | 自动选单位（Ki/Mi/Gi） |
| `-m` / `-g` | 固定按 MiB / GiB 显示，方便脚本做阈值比较 |
| `-s 2` | 每 2 秒刷新一次，持续输出 |
| `-c 5` | 配合 `-s`，采样 5 次后退出 |
| `-t`, `--total` | 末尾追加 Mem+Swap 的总计行 |
| `-w`, `--wide` | 加宽输出，把 `buff/cache` 拆成 `buffers` 与 `cached` 两列 |

## 示例

日常巡检看这一条就够了：

```bash
free -h
```

```text
              total        used        free      shared  buff/cache   available
Mem:           15Gi       6.2Gi       1.1Gi       512Mi       8.3Gi       8.4Gi
Swap:         4.0Gi       1.0Gi       3.0Gi
```

`free` 只有 1.1Gi 看似危急，但 `available` 有 8.4Gi，说明 cache 可回收、内存实际充足。

老环境（procps 3.2.x）没有 `available` 列，看 `-/+ buffers/cache` 一行的 used；procps-ng 里可再加 `-w` 把 `buff/cache` 展开成 `buffers` 与 `cached`：

```bash
free -m
```

```text
              total        used        free      shared  buffers     cached
Mem:          15884        6348        1126         512        1024       7372
-/+ buffers/cache:        1856       14028
Swap:          4095        1024        3071
```

按秒采样观察是否持续增长（内存泄漏或缓存被反复丢弃）：

```bash
free -h -s 2 -c 10
```

确认总用量与 swap 合计：

```bash
free -h -t
```

## 注意事项

- `available` 是内核估算「不触发 swap 就能给新进程用」的内存，包含可回收的 page cache 与可回收 slab；判断是否缺内存只看它，别看 `free`。
- `used = total - free - buffers - cache`，`buff/cache` 大是正常现象，不是「被吃掉」；只有 `si`/`so` 持续非零才是真缺内存（用 `vmstat 1` 佐证）。
- `shared` 统计的是 tmpfs 与共享内存段，`/dev/shm` 里堆大文件会直接计入并挤占可用内存。
- 容器里读 `/proc/meminfo` 得到的是宿主机的内存，除非有 lxcfs 之类的隔离层；`cgroup` 限制要看 `/sys/fs/cgroup/memory.max`。

## 相关命令

- `vmstat 1 5` — 看 si/so 是否持续换页
- `top` / `htop` — 按进程定位内存占用
- `df -h /dev/shm` — 查看 tmpfs 占用
- `cat /proc/meminfo` — 逐字段查看内核内存计数
