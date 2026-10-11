// 零网络判据 —— 贴进 Electron 主进程（不是渲染层，sandbox 下读不到 session）。
// 用法：
//   1. import 里加上 session：import { app, session, ... } from 'electron';
//   2. 在 app.whenReady() 内、创建窗口之前调用 installNetworkGuard()；
//   3. 每次自证收尾调用 reportNetwork() 再 app.quit()。
//
// 三条设计各防一种假绿：
//   · 同一条钩子也数 file:// —— 钩子没挂上时 remote 同样是 0，本地计数 >0 才证明它在收事件。
//   · 远程请求 cancel:true —— 只观测不阻断的话，CDN 真被请求到页面也照样显示，判据就成了事后诸葛。
//   · --netprobe 反向对照 —— 主动打一次库里那条 CDN 地址，看钩子接不接得住。
//
// 只在 file:// 交付路径上装：开发模式走 http://localhost，会被这道钩子拦死。
//   if (!process.env.VITE_DEV_SERVER_URL) installNetworkGuard();

import { session } from 'electron';

const remoteRequests: string[] = [];
const localFiles = new Map<string, number>();

export function installNetworkGuard(): void {
  session.defaultSession.webRequest.onBeforeRequest({ urls: ['<all_urls>'] }, (details, callback) => {
    const scheme = details.url.slice(0, details.url.indexOf(':')).toLowerCase();
    if (scheme === 'file' || scheme === 'data' || scheme === 'blob') {
      if (scheme === 'file') {
        const name = details.url.split('/').pop()!.split('?')[0];
        localFiles.set(name, (localFiles.get(name) || 0) + 1);
      }
      callback({});
      return;
    }
    remoteRequests.push(details.url);
    callback({ cancel: true });
  });
}

export function reportNetwork(): void {
  // 排序，让同一份构建的两次运行能逐行对比。
  const listed = [...localFiles.entries()].sort().map(([n, c]) => n + (c > 1 ? 'x' + c : ''));
  console.log(
    `network remote=${remoteRequests.length} local-files=${localFiles.size} [${listed.join(', ')}]` +
      (remoteRequests.length ? ' ' + JSON.stringify(remoteRequests.slice(0, 5)) : '')
  );
}

// 判据：正常运行要看到 `network remote=0 local-files=N`；
//       带 --netprobe 要看到 `netprobe control-fetch=REJECTED ...` 且 remote=1。
// 传 N 给 --netprobe 用的话，把这段放进自证流程里执行（render 侧才有 fetch）：
export const NETPROBE_JS =
  `fetch('https://cdn.jsdelivr.net/npm/emoji-datasource-apple@16.0.0/img/apple/64/1f600.png', {cache:'no-store'})` +
  ` .then(r => 'RESOLVED status=' + r.status)` +
  ` .catch(e => 'REJECTED ' + String(e && e.message).slice(0, 60))`;
// console.log(`netprobe control-fetch=${String(await win.webContents.executeJavaScript(NETPROBE_JS))}`);
