# iostat — 磁盘与 CPU 的 IO 统计

> sysstat 的 `iostat` 按设备输出读写速率、延迟与队列深度，是判断「磁盘是不是瓶颈」的主力。

## 语法

```bash
iostat [选项] [刷新间隔(秒)] [采样次数]
iostat -x 1        # 扩展指标，每秒一次，直到 Ctrl-C
```

## 常用选项

| 选项 | 说明 |
| --- | --- |
| `-x` | 扩展统计：`%util`、`r_await`、`w_await`、`aqu-sz` 等 |
| `-c` | 只输出 CPU 行，不输出设备 |
| `-d` | 只输出设备行，不输出 CPU |
| `-y` | 跳过全零（无活动）的报告行 |
| `-z` | 隐藏自启动以来无活动的设备 |
| `-k` / `-m` | 吞吐按 KB/s 或 MB/s 显示 |
| `-t` | 每份报告前打印时间戳 |
| `-p [DEV]` | 附带分区统计，`-p ALL` 列出全部分区 |
| `-o 2` | 使用新版（sysstat 12.0+）指标集输出 |

## 示例

标准用法：每秒一次，重点看第二份之后的报告（第一份是开机以来的平均值）：

```bash
iostat -x 1
```

```text
Device   r/s   w/s   rkB/s   wkB/s  r_await  w_await  aqu-sz  %util
vda     12.00 45.00   96.00  380.00     0.42     6.31     0.31  38.30
vdb    110.00  8.00 1420.00   12.00     1.20    18.50     2.40  96.00
```

`vdb` 的 `w_await` 18.5 ms、`aqu-sz` 2.4、`%util` 96%，写入已接近饱和。

只看 CPU 侧的 IO 等待占比，配合脚本采样 5 次：

```bash
iostat -c 1 5
```

```text
avg-cpu:  %user   %nice %system %iowait  %steal   %idle
           8.20    0.00    3.10   22.40    0.00   66.30
```

`%iowait` 高只说明 CPU 空闲且有未完成的 IO，它不是磁盘忙碌度，忙碌度看设备的 `%util` 与 `await`。

排除光驱、loop 等无活动设备，并用 MB/s 汇报：

```bash
iostat -x -y -z -m -t 1 3
```

## 注意事项

- **第一份报告必须忽略**：它统计的是自开机以来的均值，与实时值可能差一个数量级。
- `await` = 请求从入队到完成的平均毫秒数（含排队），`r_await`/`w_await` 分读写；机械盘随机写 10 ms 级正常，SSD 应远小于 1 ms，持续几十毫秒说明排队。
- `%util` 对单队列机械盘有意义，对 NVMe/多队列设备会被严重低估或虚高（并行发 IO 时 100% 也可能仍有余量），别拿它做唯一判据。
- 老版本 iostat 才有 `svctm` 列，sysstat 手册已明确标注其为过时推算指标，不代表真实服务时间；改用 `await`、`aqu-sz`、`rareq-sz`/`wareq-sz` 读数。

## 相关命令

- `sar -d 1` / `sar -b 1` — 从 sysstat 定时落盘的历史数据回放设备指标
- `vmstat 1 5` — 整体看 `bi`/`bo` 与 `wa`，判断 IO 是否拖慢全局
- `pidstat -d 1` — 把 IO 定位到具体进程
- `iotop` — 实时按进程排 IO 榜
