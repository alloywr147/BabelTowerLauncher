import { useEffect, useState } from 'react';
export const nativeHost = Boolean(window.chrome?.webview);
const initial = {
  page: 'home', busy: false, dark: true, launcherVersion: '1.7',
  configured: false, packageAvailable: false, modVersion: '尚未安装',
  local: false, translation: false, localKnown: false, installKnown: false,
  installReady: false, logs: [], gameRoot: '', installRoot: '', currentFolder: '',
  progress: 0, draftRevision: 0, reportText: '', result: ''
};
export function useLauncher(onNotice) {
  const [state, setState] = useState(() => ({ ...initial, dark: window.__babelDark !== false }));
  useEffect(() => {
    if (!nativeHost) return;
    const receive = event => {
      const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
      if (data?.type === 'state') setState(data);
    };
    window.chrome.webview.addEventListener('message', receive);
    window.chrome.webview.postMessage({ command: 'state' });
    return () => window.chrome.webview.removeEventListener('message', receive);
  }, []);
  const send = (command, payload = {}) => {
    if (nativeHost) { window.chrome.webview.postMessage({ command, ...payload }); return; }
    const routes = ['home', 'mod', 'diagnostics', 'settings', 'software'];
    if (routes.includes(command)) setState(previous => ({ ...previous, page: command }));
    else if (command === 'theme') setState(previous => ({ ...previous, dark: !previous.dark }));
    else if (command === 'clearResult') setState(previous => ({ ...previous, result: '' }));
    else onNotice('此浏览器视图未连接桌面程序；请运行巴别塔启动器。');
  };
  return [state, send];
}
