import assert from 'node:assert/strict';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, mkdirSync, readdirSync, copyFileSync, unlinkSync, rmdirSync } from 'node:fs';
import os from 'node:os';
import { TeamsProvider } from '../src/bridge.mjs';
if (process.argv.includes('--isolated')) {
  // Only this child sees missing runtime roots; never changes machine settings.
  const missing = mkdtempSync(path.join(os.tmpdir(), 'meetingcontrols-runtime-check-'));
  // A nonexistent DOTNET_ROOT falls back to the system install. Supply only
  // its host loader in a private test root, without any shared frameworks.
  const roots = [process.env.DOTNET_ROOT_X64, process.env.DOTNET_ROOT,
    path.join(process.env.ProgramFiles ?? 'C:/Program Files', 'dotnet'), path.resolve('.tools/dotnet')].filter(Boolean);
  const hostRoot = roots.map(root => path.join(root, 'host/fxr')).find(existsSync);
  let hostDirectory;
  if (hostRoot) {
    const version = readdirSync(hostRoot).sort((a, b) => b.localeCompare(a, undefined, { numeric: true }))[0];
    hostDirectory = path.join(missing, 'host/fxr', version);
    mkdirSync(hostDirectory, { recursive: true });
    copyFileSync(path.join(hostRoot, version, 'hostfxr.dll'), path.join(hostDirectory, 'hostfxr.dll'));
  }
  try {
  const result = spawnSync(path.resolve('com.meetingcontrols.streamdeck.sdPlugin/bin/MeetingControls.Helper.exe'), [], {
    windowsHide: true, encoding: 'utf8', timeout: 10000,
    env: { ...process.env, PATH: '', DOTNET_ROOT: missing, DOTNET_ROOT_X64: missing,
      DOTNET_MULTILEVEL_LOOKUP: '0', DOTNET_DISABLE_GUI_ERRORS: '1' }
  });
  assert.ifError(result.error);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Microsoft.WindowsDesktop.App|install.*\.NET/i);
  console.log('Missing Desktop Runtime correctly prevents helper startup');
  } finally {
    if (hostDirectory) {
      unlinkSync(path.join(hostDirectory, 'hostfxr.dll'));
      rmdirSync(hostDirectory); rmdirSync(path.join(missing, 'host/fxr')); rmdirSync(path.join(missing, 'host'));
    }
    rmdirSync(missing);
  }
  process.exit(0);
}
const provider = new TeamsProvider(path.resolve('com.meetingcontrols.streamdeck.sdPlugin/bin/MeetingControls.Helper.exe'));
const timeout = setTimeout(() => { provider.stop(); console.error('Bridge smoke test timed out'); process.exitCode = 1; }, 12000);
try {
  const state = await new Promise(resolve => { provider.once('state', resolve); provider.start(); });
  assert.ok(state && Array.isArray(state.sessions));
  assert.equal(state.error, '');
  // A deliberately invalid session must never be routed to an existing meeting.
  await assert.rejects(provider.execute('microphone', { id: 'invalid-test-session' }));
  console.log(`Bridge OK; ${state.sessions.length} session(s); invalid target rejected`);
} finally { clearTimeout(timeout); provider.stop(); }
