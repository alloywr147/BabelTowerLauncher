import { useEffect, useRef, useState } from 'react';
import { ArrowRight, Check, Sun, Moon, X, ArrowUpRight, GearSix, DownloadSimple, CaretDown } from '@phosphor-icons/react';
import { ContourScene as ArchiveScene } from './ContourScene';
import { paintTheme } from './vendor/rhine/src/theme-ui';
import { SurfaceTransition } from './vendor/rhine/src/ui-transitions';
import './prototype.css';
import './launcher.css';
import { useLauncher, nativeHost } from './useLauncher';
import { StatusGrid, SecondaryPage } from './LauncherPages';
import { FramePacer } from './frame-pacer';
import { followPointer } from './launcher-motion';
import { ReducedMotion, RollingNumber, useReducedMotion, useRhinePage, useTerminalDialog } from './RhineWidgets';
import './rhine-refinement.css';

const navigation = ['启动', 'Mod 更新', '诊断'];

export function StandardApp() {
  const host = useRef(null);
  const scene = useRef(null);
  const live = useRef({ dark: true, motion: true });
  const [dark, setDark] = useState(window.__babelDark !== false);
  const motion = true;
  const reduced = useReducedMotion(motion);

  const [ready, setReady] = useState(false);
  const [error, setError] = useState('');
  const [toast, setToast] = useState('');
  const [dialog, setDialog] = useState('');
  const [state, send] = useLauncher(setToast);
  const pane = useRhinePage(state.page, reduced);
  const pageState = { ...state, page: pane.page };
  const terminal = useTerminalDialog(dialog === 'result' ? dialog + state.result : dialog, reduced, () => {
    // Clear native result only after the close animation. Clearing it earlier changes
    // the dialog identity and cancels its own close transition.
    if (dialog === 'result') send('clearResult');
    setDialog('');
  });
  const [draft, setDraft] = useState({ gameRoot: '', installRoot: '', currentFolder: '' });
  useEffect(() => { setDark(state.dark); }, [state.dark]);
  useEffect(() => { setDraft({ gameRoot: state.gameRoot, installRoot: state.installRoot, currentFolder: state.currentFolder }); }, [state.gameRoot, state.installRoot, state.currentFolder]);
  useEffect(() => { if (state.detection) setDraft({ gameRoot: state.detection.game, installRoot: state.detection.installRoot, currentFolder: state.detection.currentFolder }); }, [state.draftRevision]);
  useEffect(() => { if (state.result) setDialog('result'); }, [state.result]);
  useEffect(() => { if (state.launchConfirmation) setDialog('launch-confirm'); }, [state.launchConfirmation]);
  const [logOpen, setLogOpen] = useState(false);
  const logRoot = useRef(null);
  const logTransition = useRef(null);
  useEffect(() => { setLogOpen(false); }, [pane.page]);
  useEffect(() => {
    if (!logOpen) return;
    const close = event => {
      if (event.type === 'keydown' ? event.key === 'Escape' : !event.target.closest('.log')) setLogOpen(false);
    };
    document.addEventListener('keydown', close);
    document.addEventListener('pointerdown', close);
    return () => { document.removeEventListener('keydown', close); document.removeEventListener('pointerdown', close); };
  }, [logOpen]);

  useEffect(() => {
    if (!logRoot.current) return;
    const transition = new SurfaceTransition(logRoot.current, logRoot.current, 420, 220, 24, 14);
    logTransition.current = transition;
    return () => { transition.dispose(); logTransition.current = null; };
  }, [pane.page]);
  useEffect(() => {
    if (logOpen) logTransition.current?.show(reduced);
    else logTransition.current?.hide(reduced);
  }, [logOpen, reduced, pane.page]);

  useEffect(() => {
    live.current.dark = dark;
    scene.current?.setTheme(dark);
    if (!scene.current) paintTheme(dark ? 1 : 0);
    document.documentElement.dataset.previewTheme = dark ? 'dark' : 'light';
  }, [dark]);
  useEffect(() => {
    live.current.motion = motion;
    scene.current?.setReduced(!motion);
    host.current?.setAttribute('data-motion', motion ? 'running' : 'paused');
  }, [motion]);
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(''), 3600);
    return () => clearTimeout(timer);
  }, [toast]);

  useEffect(() => {
    let disposed = false;
    let loaded = false;
    let current;
    let animation = 0;
    const pacer = new FramePacer();
    let rendered = 0;
    let measuredAt = 0;
    let hoverCount = 0;
    let pointer = null;
    let followed = { x: innerWidth / 2, y: innerHeight / 2 };
    let previousFrame = 0;
    let settleUntil = 0;
    let observer;
    const onPointer = event => {
      if (!live.current.motion) return;
      if (event.pointerType === 'mouse') pointer = { x: event.clientX, y: event.clientY };
    };
    const onExit = event => {
      if (!event.relatedTarget) { pointer = null; current?.clearLauncherPointer(); }
    };
    const resize = () => { if (loaded) current.resize(); };
    const frame = ms => {
      if (disposed) return;
      animation = requestAnimationFrame(frame);
      if (!pacer.take(ms, 60, document.hidden)) return;
      const elapsed = previousFrame ? (ms - previousFrame) / 1000 : 1 / 60;
      previousFrame = ms;
      if (live.current.motion && pointer) {
        followed.x = followPointer(followed.x, pointer.x, elapsed);
        followed.y = followPointer(followed.y, pointer.y, elapsed);
        current.setLauncherPointer(followed.x, followed.y, 'mouse');
      }
      // Keep the actual upstream theme wave running even with background motion paused.
      const changingTheme = Math.abs(current.themeAmount - Number(live.current.dark)) > .001;
      if (live.current.motion || changingTheme || ms < settleUntil) {
        current.update(ms / 1000);
        rendered++;
      }
      if (host.current && ms - measuredAt >= 1000) {
        host.current.dataset.renderFps = (rendered * 1000 / Math.max(1, ms - measuredAt)).toFixed(1);
        rendered = 0; measuredAt = ms;
      }
      paintTheme(current.themeAmount);
      if (host.current) {
        const telemetry = current.launcherTelemetry;
        host.current.dataset.lift = String(telemetry.lift.toFixed(4));
        host.current.dataset.drawnFrames = String(telemetry.frames);
      }
    };
    queueMicrotask(async () => {
      if (disposed || !host.current) return;
      try {
        current = new ArchiveScene(host.current);
        scene.current = current;
        current.setReduced(!live.current.motion);
        current.setTheme(live.current.dark, true);
        current.onHover = index => {
          if (index === null) return;
          hoverCount++;
          host.current.dataset.hoverCount = String(hoverCount);
          host.current.dataset.hoveredArchive = String(index);
          // Hover lift follows the damped cursor; do not stack selection waves on mouse moves.
        };
        await current.load();
        loaded = true;
        if (disposed) { current.dispose(); return; }
        current.resize();
        // A paused scene still needs a first frame; opening choreography otherwise stays offscreen.
        current.update(performance.now() / 1000);
        settleUntil = performance.now() + 1800;
        host.current.dataset.sceneReady = 'true';
        setReady(true);
        observer = new ResizeObserver(resize);
        observer.observe(host.current);
        document.addEventListener('pointermove', onPointer);
        document.addEventListener('pointerout', onExit);
        animation = requestAnimationFrame(frame);
      } catch (failure) {
        if (!disposed) {
          setError('三维背景加载失败，可重新打开启动器重试');
          console.error(failure);
        }
      }
    });
    return () => {
      disposed = true;
      cancelAnimationFrame(animation);
      observer?.disconnect();
      document.removeEventListener('pointermove', onPointer);
      document.removeEventListener('pointerout', onExit);
      scene.current = null;
      if (loaded) current?.dispose();
    };
  }, []);


  const closeDialog = (dismiss = true) => {
    if (!terminal.close()) return;
    if (dismiss !== false && dialog === 'launch-confirm') send('cancelLaunch');
  };
  useEffect(() => {
    if (!dialog) return;
    const key = event => {
      if (event.key === 'Escape') { closeDialog(); return; }
      if (event.key === 'Tab') {
        const buttons = [...document.querySelectorAll('.preview-dialog button:not(:disabled)')];
        if (!buttons.length) return;
        const first = buttons[0], last = buttons.at(-1);
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
      }
    };
    const previouslyFocused = document.activeElement;
    document.querySelector('.dialog-close')?.focus();
    document.addEventListener('keydown', key);
    return () => { document.removeEventListener('keydown', key); previouslyFocused?.focus?.(); };
  }, [dialog]);
  const onReport = () => { setDialog('report'); send('report'); };
  const home = pane.page === 'home';
  const selected = ['home', 'mod', 'diagnostics'].indexOf(pane.page);
  return (
    <ReducedMotion.Provider value={reduced}><main className={'launcher ' + (home ? '' : 'is-detail')} data-ui-motion={reduced ? 'reduced' : 'running'}>
      <div ref={host} className="array-background" aria-label="实验室等高线背景" />
      <div className="scene-atmosphere archive-atmosphere" aria-hidden="true" />
      <div className="launcher-chrome">
        <header className="topbar">
          <div className="brand"><strong>巴别塔启动器</strong><span>BABEL TOWER <i>/</i> LAUNCHER</span></div>
          <div className="top-actions"><button className="utility-link web-settings" disabled={state.busy} onClick={() => send('webSettings')}><GearSix size={17}/>Mod 设置</button>
            <button className="utility-link" disabled={state.busy} onClick={() => send('software')}><DownloadSimple size={17} />软件更新</button>
            <button className="utility-link" disabled={state.busy} onClick={() => send('settings')}><GearSix size={17} />设置</button>
            <button className="theme-switch" disabled={state.busy} onClick={() => send('theme')}>{dark ? <Sun size={17} /> : <Moon size={17} />}{dark ? '浅色模式' : '深色模式'}</button>
          </div>
        </header>
        <div className="page-surface" ref={pane.root} aria-busy={pane.switching} inert={pane.switching || undefined}>{home ? <section className="launch-panel archive-callout" aria-label="启动游戏与连接状态">
          <div className="detail-kicker"><span>GAME / 01</span><span className="ready-label">{state.local && state.translation && state.installReady && <Check size={13} />}{state.busy ? '正在处理' : state.gameRunning ? '游戏运行中' : state.local && state.translation && state.installReady ? '准备就绪' : '等待检查'}</span></div>
          <h1>DEADLOCK</h1><div className="detail-title-cn">死锁<span>游戏启动</span></div>
          <div className="callout-rule"><i /></div>
          <StatusGrid state={state}/><div className={"channel-strip " + (state.channelReady ? "is-ready" : "")} role="status"><span>低延迟通道</span><strong>{state.channelText || "尚未检查"}</strong>{state.channelSupported && <button className="certificate-setup" disabled={state.busy} onClick={() => setDialog('certificate')}>{state.certificateConfiguring ? '正在配置…' : '配置证书'}</button>}</div>
          <button className="launch-button read-file" disabled={pageState.busy} onClick={() => send('launch')}><span className="launch-copy">{state.busy ? '正在检查' : !state.configured ? '设置路径' : state.gameRunning ? '检查运行状态' : '启动游戏'}<small>{state.gameRunning ? 'CHECK CONNECTION' : 'START DEADLOCK'}</small></span><ArrowRight size={33} weight="light" /></button>
          <div className="secondary-actions"><button disabled={state.busy} onClick={() => setDialog('repair')}>一键修复<ArrowUpRight size={16} /></button><button disabled={state.busy} onClick={onReport}>导出报告<ArrowUpRight size={16} /></button></div>
          <section className="log" aria-label="运行记录"><button className="log-toggle" aria-expanded={logOpen} aria-controls="log-content" onClick={() => setLogOpen(!logOpen)}>运行记录<CaretDown size={13} className={logOpen ? 'expanded' : ''} /></button><div id="log-content" className="log-content" ref={logRoot} hidden><div className="log-header"><span>运行记录</span><button className="log-close" aria-label="关闭运行记录" onClick={() => setLogOpen(false)}><X size={16}/></button></div><div className="log-lines">{state.logs.length ? state.logs.slice(-8).map((line,index) => <p key={index}>{line}</p>) : <p>暂无运行记录</p>}</div></div></section>
        </section> : <SecondaryPage state={pageState} send={send} draft={draft} setDraft={setDraft} onRepair={() => setDialog('repair')} onCertificate={() => setDialog('certificate')} onReport={onReport} onNotice={setToast}/>}</div>
        <div className="page-counter" aria-hidden="true"><span>{home ? 'LAUNCH / MENU' : 'BABEL / SYSTEM'}</span><div><b><RollingNumber value={(selected < 0 ? 0 : selected) + 1} pad={2}/></b><small>/ 03</small></div></div>
        <nav className="menu-navigation" aria-label="启动器导航">{navigation.map((label, index) => <button disabled={state.busy} key={label} className={index === selected ? 'selected' : ''} aria-current={index === selected ? 'page' : undefined} onClick={() => send(['home', 'mod', 'diagnostics'][index])}><small>{String(index + 1).padStart(2, '0')}</small>{label}<span className="nav-line" /></button>)}</nav>
        <footer className="footer"><span>{state.launcherVersion}<i>/</i>{nativeHost ? 'BABEL TOWER LAUNCHER' : '浏览器视图 · 桌面服务未连接'}</span><div className="background-tools"><span className="scene-message" role="status">{error || (!ready ? '正在加载背景' : '')}</span></div></footer>
      </div>
      {toast && <div className="toast" role="status">{toast}</div>}
      {dialog && <div className="dialog-shade" ref={terminal.root} onClick={closeDialog}><section ref={terminal.panel} className={'preview-dialog terminal-dialog ' + (dialog === 'report' ? 'report-dialog' : '')} role="dialog" aria-modal="true" aria-labelledby="terminal-dialog-title" onClick={event => event.stopPropagation()}>
        <div className="terminal-top"><span>{dialog === 'report' ? 'DIAGNOSTICS / REPORT' : dialog === 'repair' ? 'MOD / REPAIR' : dialog === 'launch-confirm' ? 'DEADLOCK / LAUNCH' : 'BABEL / RESULT'}</span><button className="dialog-close" aria-label="关闭弹窗" onClick={closeDialog}><span>ESC</span><X size={18} /></button></div>
        <h2 id="terminal-dialog-title">{dialog === 'report' ? '诊断报告' : dialog === 'repair' ? '一键修复' : dialog === 'certificate' ? '配置低延迟通道证书' : dialog === 'launch-confirm' ? '检查已通过' : '操作结果'}</h2>
        {dialog === 'certificate' ? <>
          <p>将运行当前所选 Mod 的官方脚本，在本机生成并信任 <strong>BabelTower-localhost</strong> 证书，用于低延迟通道。</p>
          <p>Windows 可能弹出“安全警告”。核对证书名称后，请选择“是”；取消确认将无法启用通道。</p>
          <p className="demo-note">完成后请完全退出 Steam（包括托盘）并重新打开，再点击“重启桥”。不会自动关闭游戏或 Steam；每台电脑各自生成证书。</p>
          <button className="dialog-confirm certificate-confirm" disabled={state.busy || terminal.closing} onClick={() => { closeDialog(false); send('configureCertificate'); }}>开始配置<ArrowRight size={20}/></button>
        </> : dialog === 'report' ? <><pre className="report-preview">{state.reportText || '正在生成报告…'}</pre><button className="dialog-confirm" disabled={state.busy || !state.reportText} onClick={() => send('saveReport')}>保存报告<ArrowRight size={20}/></button></> : dialog === 'repair' ? <>
          <p>{state.gameRunning ? '游戏运行中只重启所选桥。涉及 Mod 文件或加载配置的修复，需要先退出游戏。' : '检查并补回当前版本的 Mod 文件，修复加载路径与桥配置，再检查连接。'}</p>
          <p className="demo-note">修复前保留备份，保留其他 Mod 和翻译服务配置。完整包缺失时，请从更新页下载官方完整包。</p>
          <button className="dialog-confirm" disabled={state.busy || terminal.closing} onClick={() => { closeDialog(false); send('repair'); }}>开始修复<ArrowRight size={20}/></button>
        </> : dialog === 'launch-confirm' ? <><ul className="check-confirmation"><li><Check/>本地桥服务正常</li><li><Check/>翻译接口正常</li><li><Check/>Mod 文件与加载配置正常</li><li className={state.channelReady ? "" : "channel-warning"}>低延迟通道：{state.channelText || "尚未检查"}</li></ul><button className="dialog-confirm" disabled={terminal.closing} onClick={() => { closeDialog(false); send('launchConfirmed'); }}>启动游戏<ArrowRight size={20}/></button></> : <><p className="result-copy">{state.result}</p><button className="dialog-confirm" onClick={closeDialog}>返回<ArrowRight size={20}/></button></>}
      </section></div>}
    </main></ReducedMotion.Provider>
  );
}

