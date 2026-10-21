import { useState } from 'react';
import { ArrowRight, ArrowUpRight, Check, ArrowClockwise, FolderOpen } from '@phosphor-icons/react';
import { StatusValue } from './RhineWidgets';
export function StatusGrid({ state }) {
  const statuses = [
    ['本地桥服务', state.local ? '已连接' : state.localKnown ? '离线' : '尚未检查', state.local],
    ['翻译接口', state.translation ? '正常' : '待检查', state.translation],
    ['安装文件', state.installReady ? '完整' : state.installKnown ? '需修复' : '尚未检查', state.installReady]
  ];
  return <dl className="metadata">{statuses.map(([title, value, ok], index) => <div key={title} data-status={ok ? 'good' : 'pending'}><dt><span className="channel-index" aria-hidden="true">0{index + 1}</span>{title}</dt><dd className={ok ? 'status-good' : ''}>{ok && <Check size={13} />}<StatusValue value={value}/></dd></div>)}<div><dt><span className="channel-index" aria-hidden="true">04</span>当前 Mod</dt><dd className="mod-version"><StatusValue value={state.modVersion}/></dd></div></dl>;
}
export function TextAction({ children, onClick, disabled, icon: Icon = ArrowUpRight }) {
  return <button className="text-action" onClick={onClick} disabled={disabled}>{children}<Icon size={17} /></button>;
}
function UpdatePage({ state, send }) {
  const [checked, setChecked] = useState(false);
  const [operation, setOperation] = useState('check');
  const software = state.page === 'software';
  const checking = operation === 'check';
  const cancellable = state.busy && state.canCancel;
  const label = state.busy
    ? cancellable ? checking ? '取消检查' : '取消下载' : '正在处理…'
    : state.updateAvailable ? '下载并安装' : '检查更新';
  const status = checked || state.updateAvailable || state.busy
    ? state.busy && checking ? '正在检查更新…' : state.downloadStatus || state.updateStatus || '' : '';
  const act = () => {
    if (state.busy) { if (cancellable) send('cancel'); return; }
    setChecked(true);
    setOperation(state.updateAvailable ? 'download' : 'check');
    send(state.updateAvailable
      ? software ? 'downloadSoftware' : 'downloadMod'
      : software ? 'checkSoftware' : 'checkMod');
  };
  return <section className={'detail-page minimal-update page-' + state.page} aria-label="更新">
    <div className="update-page-header">
      <nav className="update-tabs" role="tablist" aria-label="更新类型">
        {[['mod', 'Mod 更新'], ['software', '启动器更新']].map(([page, title]) =>
          <button key={page} role="tab" data-update={page} aria-selected={state.page === page}
            disabled={state.busy} onClick={() => send(page)}>{title}</button>)}
      </nav>
      <button className="back-link" disabled={state.busy} onClick={() => send('home')}>返回 <ArrowUpRight size={14}/></button>
    </div>
    <div className="single-update-action">
      <button className="update-action-button" disabled={state.busy && !cancellable} onClick={act}>{label}</button>
      <div className="update-action-status" role="status" aria-live="polite">{status}</div>
      {state.busy && state.progress > 0 && <progress className="single-update-progress" aria-label="下载进度" max="100" value={state.progress}/>}
    </div>
  </section>;
}
export function SecondaryPage({ state, send, draft, setDraft, onRepair, onCertificate, onReport, onNotice }) {
  if (state.page === 'mod' || state.page === 'software') return <UpdatePage key={state.page} state={state} send={send}/>;
  const disabled = state.busy;
  const isSoftware = state.page === 'software';
  const title = state.page === 'settings' ? '路径设置' : state.page === 'diagnostics' ? '诊断与修复' : isSoftware ? '启动器更新' : 'Mod 更新';
  return <section className={'detail-page page-' + state.page} key={state.page} aria-label={title}>
    <div className="detail-kicker"><span>{state.page === 'settings' ? 'PATH / SETUP' : isSoftware ? 'LAUNCHER / ' + state.launcherVersion : 'MOD / ' + state.modVersion}</span><button className="back-link" disabled={disabled} onClick={() => send('home')}>返回 <ArrowUpRight size={14}/></button></div>
    <h1>{title}</h1><div className="callout-rule"><i /></div>
    {state.page === 'settings' ? <>
      <div className="page-actions"><TextAction onClick={() => send('webSettings')} disabled={disabled}>Mod 网页设置</TextAction></div><div className="settings-top"><span>选择游戏和巴别塔安装位置</span><TextAction onClick={() => send('detect', draft)} disabled={disabled} icon={ArrowClockwise}>自动检测</TextAction></div>
      <form onSubmit={event => { event.preventDefault(); send('saveSettings', draft); }}>
        {[['gameRoot', '游戏目录', 'pickGame', 'Deadlock 根目录，包含 game 文件夹'], ['installRoot', '巴别塔安装目录', 'pickRoot', '专用于存放完整 Mod 版本'], ['currentFolder', '已有版本（可选）', 'pickPackage', '未安装可留空，之后从更新页下载官方完整包']].map(([field, label, command, hint]) => <label className="path-field" key={field}><span>{label}</span><div><input value={draft[field]} onChange={event => setDraft(previous => ({ ...previous, [field]: event.target.value }))} disabled={disabled} list={field === 'gameRoot' ? 'detected-games' : field === 'currentFolder' ? 'detected-packages' : undefined}/><button type="button" disabled={disabled} aria-label={'浏览' + label} onClick={() => send(command, draft)}><FolderOpen size={19}/></button></div><small>{hint}</small></label>)}
        <datalist id="detected-games">{state.detection?.games?.map(path => <option key={path} value={path}/>)}</datalist><datalist id="detected-packages">{state.detection?.packages?.map(path => <option key={path} value={path}/>)}</datalist>
        <button className="page-primary" type="submit" disabled={disabled}>保存路径 <ArrowRight size={22}/></button>
      </form>
    </> : state.page === 'diagnostics' ? <>
      <StatusGrid state={state}/><div className="channel-diagnostic"><div><span>低延迟通道</span><strong>{state.channelText || "尚未检查"}</strong></div><p>{state.certificateConfiguring ? '正在配置证书，如出现 Windows 安全警告，请核对名称后确认。' : state.channelMessage || "低延迟通道尚未检查。"}</p><div className="channel-actions">{state.channelSupported && <TextAction onClick={onCertificate} disabled={disabled}>{state.certificateConfiguring ? '正在配置证书…' : '配置证书'}</TextAction>}<TextAction onClick={() => send('webSettings')} disabled={disabled}>打开 Mod 网页设置</TextAction></div></div><p className="detail-description">{state.installText || '检查安装文件、加载配置与连接状态。'}</p>
      <div className="page-actions"><TextAction onClick={() => send('check')} disabled={disabled} icon={Check}>检查状态</TextAction><TextAction onClick={() => send('restart')} disabled={disabled} icon={ArrowClockwise}>重启桥</TextAction><TextAction onClick={() => send('stop')} disabled={disabled}>停止桥</TextAction></div>
      <p className="detail-description">{state.gameMessage || (state.gameRunning ? '游戏正在运行，文件修复需先退出游戏。' : '游戏尚未运行。')}</p>
      <div className="page-actions"><TextAction onClick={onRepair} disabled={disabled}>一键修复</TextAction><TextAction onClick={onReport} disabled={disabled}>导出报告</TextAction><TextAction onClick={() => send('bridgeLogs')} disabled={disabled}>桥日志</TextAction></div>
      <div className="operation-lines" aria-live="polite">{state.logs.slice(-5).map((line,index) => <p key={index}>{line}</p>)}</div>
    </> : null}
  </section>;
}
