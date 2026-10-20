// Providers own platform-specific detection and commands. Actions only see this model.
export function selectSession(snapshot, target = 'auto', now = Date.now()) {
  if (!snapshot || now - snapshot.receivedAt > 4000 || snapshot.error) return { reason: 'unknown' };
  const sessions = snapshot.sessions.filter(s => s.inMeeting);
  if (target !== 'auto') {
    const session = sessions.find(s => s.id === target);
    return session ? { session } : { reason: 'missing' };
  }
  if (sessions.length > 1) return { reason: 'ambiguous' };
  return sessions.length ? { session: sessions[0] } : { reason: 'idle' };
}

export function presentation(kind, selection) {
  const names = { microphone: 'マイク', camera: 'カメラ', share: '共有', leave: '退出' };
  if (!selection.session) {
    const labels = { idle: '通話なし', ambiguous: '対象選択', missing: '対象なし', unknown: '状態不明' };
    return { text: labels[selection.reason] ?? '状態不明', color: '#64748b', active: false };
  }
  const s = selection.session;
  if (s.picker && kind === 'share') return { text: '共有先選択中', color: '#eab308', active: false };
  if ((s.picker || s.transition) && kind !== 'share' && s.lastKnown) {
    return presentation(kind, { session: s.lastKnown });
  }
  const value = { microphone: s.muted, camera: s.cameraOn, share: s.sharing, leave: true }[kind];
  if (value == null) return { text: '状態不明', color: '#eab308', active: false };
  if (!s.capabilities?.[kind]) return { text: '操作不可', color: '#64748b', active: false };
  return { text: kind === 'microphone' ? (value ? 'ミュート' : 'マイク ON') : kind === 'camera' ? (value ? 'カメラ ON' : 'カメラ OFF') : kind === 'share' ? (value ? '共有中' : '共有') : names[kind], color: kind === 'leave' || (kind === 'microphone' && value) ? '#ef4444' : value ? '#22c55e' : '#cbd5e1', active: value };
}

const glyphs = {
  microphone: '<rect x="62" y="28" width="20" height="43" rx="10"/><path d="M51 58v6a21 21 0 0 0 42 0v-6M72 85v13M58 98h28"/>',
  camera: '<rect x="36" y="39" width="50" height="40" rx="7"/><path d="m86 51 23-12v40L86 67"/>',
  share: '<rect x="34" y="34" width="76" height="53" rx="5"/><path d="M72 75V44m-12 12 12-12 12 12M57 99h30M72 87v12"/>',
  leave: '<path d="M37 68c18-22 52-22 70 0l-10 15-16-8V65H63v10l-16 8z"/>'
};
export function icon(kind, view, platform = '') {
  const slash = (kind === 'microphone' && view.text === 'ミュート') || (kind === 'camera' && view.text === 'カメラ OFF');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect width="144" height="144" rx="16" fill="#101827"/><g fill="none" stroke="${view.color}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round">${glyphs[kind]}${slash ? '<path d="m36 29 73 72"/>' : ''}</g><text x="72" y="123" text-anchor="middle" font-family="Segoe UI,sans-serif" font-size="17" fill="#e2e8f0">${platform === 'teams' ? 'Teams' : ''}</text></svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
}

// Marketplace action-list icons use white strokes and a transparent background.
export function actionListIcon(kind) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="20 16 104 104"><g fill="none" stroke="#FFFFFF" stroke-width="6" stroke-linecap="round" stroke-linejoin="round">${glyphs[kind]}</g></svg>`;
}
