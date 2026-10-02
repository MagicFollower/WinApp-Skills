export type CommandEntry = {
  slug: string
  name: string
  summary: string
}

export type ThemeIcon =
  | 'folder'
  | 'text'
  | 'gear'
  | 'globe'
  | 'archive'
  | 'key'
  | 'disk'
  | 'terminal'

export type Theme = {
  id: string
  label: string
  blurb: string
  icon: ThemeIcon
  commands: CommandEntry[]
}

export const THEMES: Theme[] = [
  {
    id: 'files',
    label: '文件与目录',
    blurb: '浏览、创建、复制、删除与查找',
    icon: 'folder',
    commands: [
      { slug: 'ls', name: 'ls', summary: '列出目录内容，长格式与排序选项' },
      { slug: 'cd', name: 'cd', summary: '切换当前工作目录' },
      { slug: 'pwd', name: 'pwd', summary: '打印当前目录的绝对路径' },
      { slug: 'cp', name: 'cp', summary: '复制文件与目录，保留属性' },
      { slug: 'mv', name: 'mv', summary: '移动与重命名' },
      { slug: 'rm', name: 'rm', summary: '删除文件与目录，递归与强制' },
      { slug: 'mkdir', name: 'mkdir', summary: '创建目录，一次建多级' },
      { slug: 'touch', name: 'touch', summary: '新建空文件或更新时间戳' },
      { slug: 'find', name: 'find', summary: '按名称、时间、大小查找并处理' },
      { slug: 'du', name: 'du', summary: '统计文件与目录占用空间' }
    ]
  },
  {
    id: 'text',
    label: '文本处理',
    blurb: '查看、检索、改写与统计文本',
    icon: 'text',
    commands: [
      { slug: 'cat', name: 'cat', summary: '输出文件内容，合并与写入' },
      { slug: 'less', name: 'less', summary: '分页查看，向前向后翻页' },
      { slug: 'head', name: 'head', summary: '查看文件开头若干行或字节' },
      { slug: 'tail', name: 'tail', summary: '查看结尾并跟踪日志追加' },
      { slug: 'grep', name: 'grep', summary: '按模式检索，正则与上下文' },
      { slug: 'sed', name: 'sed', summary: '流编辑器，替换删除与打印' },
      { slug: 'awk', name: 'awk', summary: '按列处理文本，条件与统计' },
      { slug: 'sort', name: 'sort', summary: '排序，按键、数值与去重' },
      { slug: 'uniq', name: 'uniq', summary: '折叠相邻重复行并计数' },
      { slug: 'wc', name: 'wc', summary: '统计行数、字数、字节数' }
    ]
  },
  {
    id: 'process',
    label: '进程与服务',
    blurb: '查看、终止进程与管理系统服务',
    icon: 'gear',
    commands: [
      { slug: 'ps', name: 'ps', summary: '快照进程，自选输出列' },
      { slug: 'top', name: 'top', summary: '实时查看 CPU、内存与进程' },
      { slug: 'kill', name: 'kill', summary: '向进程发送信号，含强制终止' },
      { slug: 'pkill', name: 'pkill', summary: '按名字或模式批量终止进程' },
      { slug: 'pgrep', name: 'pgrep', summary: '按名字查找进程号' },
      { slug: 'systemctl', name: 'systemctl', summary: '服务的启停、状态与开机自启' },
      { slug: 'journalctl', name: 'journalctl', summary: '查看 systemd 日志并按条件过滤' },
      { slug: 'nohup', name: 'nohup', summary: '让命令脱离终端在后台运行' },
      { slug: 'jobs', name: 'jobs / fg / bg', summary: '作业控制，挂起与恢复' },
      { slug: 'nice', name: 'nice / renice', summary: '调整进程的 CPU 调度优先级' }
    ]
  },
  {
    id: 'network',
    label: '网络',
    blurb: '接口、连通性、端口与传输',
    icon: 'globe',
    commands: [
      { slug: 'ip', name: 'ip', summary: '查看地址、路由与网卡状态' },
      { slug: 'ping', name: 'ping', summary: '测试连通性与往返时延' },
      { slug: 'ss', name: 'ss', summary: '查看套接字与端口占用' },
      { slug: 'curl', name: 'curl', summary: '发起请求，带header与重试' },
      { slug: 'wget', name: 'wget', summary: '下载文件，递归与断点续传' },
      { slug: 'ssh', name: 'ssh', summary: '远程登录，密钥与端口转发' },
      { slug: 'scp', name: 'scp', summary: '跨主机复制文件' },
      { slug: 'dig', name: 'dig', summary: '查询 DNS 记录与解析链' },
      { slug: 'nc', name: 'nc', summary: '读写端口，做探测与简易服务' },
      { slug: 'traceroute', name: 'traceroute', summary: '跟踪到达主机的逐跳路径' }
    ]
  },
  {
    id: 'archive',
    label: '压缩与归档',
    blurb: '打包、压缩、解压与增量同步',
    icon: 'archive',
    commands: [
      { slug: 'tar', name: 'tar', summary: '打包与解包，配合 gzip/xz' },
      { slug: 'gzip', name: 'gzip / gunzip', summary: '单文件压缩与解压' },
      { slug: 'bzip2', name: 'bzip2 / bunzip2', summary: '更高压缩率的单文件压缩' },
      { slug: 'xz', name: 'xz / unxz', summary: '高压缩比，耗时更长' },
      { slug: 'zip', name: 'zip', summary: '打包压缩成 zip，跨平台' },
      { slug: 'unzip', name: 'unzip', summary: '解压 zip，列出与校验' },
      { slug: '7z', name: '7z', summary: '7-Zip 归档，支持多种格式' },
      { slug: 'zstd', name: 'zstd', summary: '速度快、压缩率均衡的新格式' },
      { slug: 'rsync', name: 'rsync', summary: '增量同步与远程传输' },
      { slug: 'split', name: 'split', summary: '按大小或行数切分大文件' }
    ]
  },
  {
    id: 'users',
    label: '权限与用户',
    blurb: '读写执行权限、属主与账号',
    icon: 'key',
    commands: [
      { slug: 'chmod', name: 'chmod', summary: '修改权限，符号与八进制' },
      { slug: 'chown', name: 'chown', summary: '修改属主与属组' },
      { slug: 'chgrp', name: 'chgrp', summary: '修改文件所属组' },
      { slug: 'sudo', name: 'sudo', summary: '以其他身份执行命令' },
      { slug: 'su', name: 'su', summary: '切换用户并进入其环境' },
      { slug: 'id', name: 'id', summary: '查看 uid、gid 与附加组' },
      { slug: 'whoami', name: 'whoami', summary: '打印当前生效的用户名' },
      { slug: 'passwd', name: 'passwd', summary: '修改口令与口令有效期' },
      { slug: 'useradd', name: 'useradd / userdel', summary: '创建与删除账号' },
      { slug: 'usermod', name: 'usermod', summary: '改登录名、组与外壳' }
    ]
  },
  {
    id: 'disk',
    label: '磁盘与性能',
    blurb: '挂载、空间、内存与IO 观测',
    icon: 'disk',
    commands: [
      { slug: 'df', name: 'df', summary: '查看文件系统剩余空间' },
      { slug: 'free', name: 'free', summary: '查看内存与 swap 使用' },
      { slug: 'lsblk', name: 'lsblk', summary: '列出块设备与分区层级' },
      { slug: 'mount', name: 'mount / umount', summary: '挂载与卸载文件系统' },
      { slug: 'fdisk', name: 'fdisk / parted', summary: '查看与划分分区表' },
      { slug: 'mkfs', name: 'mkfs', summary: '创建文件系统并调参数' },
      { slug: 'lsof', name: 'lsof', summary: '查看被占用的文件与端口' },
      { slug: 'iostat', name: 'iostat', summary: '磁盘 IO 吞吐与等待时间' },
      { slug: 'vmstat', name: 'vmstat', summary: 'CPU、内存与换页整体趋势' },
      { slug: 'uptime', name: 'uptime', summary: '运行时长与平均负载' }
    ]
  },
  {
    id: 'shell',
    label: 'Shell 与快捷键',
    blurb: '行编辑、历史、展开与会话管理',
    icon: 'terminal',
    commands: [
      { slug: 'line-editing', name: 'Ctrl 行编辑键', summary: 'Ctrl+A/E/U/K/W 快速改命令行' },
      { slug: 'history', name: 'history', summary: '查看与检索历史命令' },
      { slug: 'alias', name: 'alias', summary: '定义命令别名并持久化' },
      { slug: 'export', name: 'export', summary: '设置环境变量与生效范围' },
      { slug: 'redirect', name: '重定向与管道', summary: '> >> | 2>&1 与 heredoc' },
      { slug: 'globbing', name: '通配符展开', summary: '* ? [] {} 与 globstar' },
      { slug: 'source', name: 'source / .', summary: '在当前 shell 执行脚本' },
      { slug: 'shell-options', name: 'set 选项', summary: 'set -euo pipefail 等开关' },
      { slug: 'read', name: 'read', summary: '从标准输入读取变量' },
      { slug: 'tmux', name: 'tmux', summary: '会话分离与窗口复用' }
    ]
  }
]

const rawDocs = import.meta.glob('../content/*/*.md', {
  query: '?raw',
  import: 'default',
  eager: true
}) as Record<string, string>

export const BUILTIN_DOCS: Record<string, string> = {}
for (const [file, text] of Object.entries(rawDocs)) {
  const marker = '/content/'
  const at = file.lastIndexOf(marker)
  if (at < 0) continue
  const id = file.slice(at + marker.length).replace(/\.md$/, '')
  BUILTIN_DOCS[id] = text
}

export const COMMAND_INDEX: Record<string, { theme: Theme; command: CommandEntry }> = {}
for (const theme of THEMES) {
  for (const command of theme.commands) {
    COMMAND_INDEX[`${theme.id}/${command.slug}`] = { theme, command }
  }
}

export function docId(themeId: string, slug: string): string {
  return `${themeId}/${slug}`
}

export function firstCommand(theme: Theme): CommandEntry {
  return theme.commands[0]
}
