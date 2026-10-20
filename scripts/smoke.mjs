import assert from 'node:assert/strict';
import path from 'node:path';
import { TeamsProvider } from '../src/bridge.mjs';
const provider = new TeamsProvider(path.resolve('com.meetingcontrols.streamdeck.sdPlugin/bin/teams.ps1'));
const timeout = setTimeout(() => { provider.stop(); console.error('Bridge smoke test timed out'); process.exitCode = 1; }, 12000);
try {
  const state = await new Promise(resolve => { provider.once('state', resolve); provider.start(); });
  assert.ok(state && Array.isArray(state.sessions));
  assert.equal(state.error, '');
  // A deliberately invalid session must never be routed to an existing meeting.
  await assert.rejects(provider.execute('microphone', { id: 'invalid-test-session' }));
  console.log(`Bridge OK; ${state.sessions.length} session(s); invalid target rejected`);
} finally { clearTimeout(timeout); provider.stop(); }
