// Read-only diagnostic: all commands use a nonexistent session and are rejected.
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { setTimeout as delay } from 'node:timers/promises';
import { TeamsProvider } from '../src/bridge.mjs';
const provider = new TeamsProvider(path.resolve('com.meetingcontrols.streamdeck.sdPlugin/bin/teams.ps1'));
const timer = setTimeout(() => { provider.stop(); console.error('Diagnostic timed out'); process.exit(1); }, 20000);
try {
  await new Promise(resolve => { provider.once('state', resolve); provider.start(); });
  const samples = [];
  for (const wait of [40, 90, 160, 230, 310]) {
    await delay(wait);
    const started = performance.now();
    try { await provider.execute('microphone', { id: 'invalid-latency-test-session' }); throw new Error('Invalid session accepted'); }
    catch (error) { if (error.message !== 'Meeting control unavailable') throw error; }
    samples.push(Math.round(performance.now() - started));
  }
  console.log(JSON.stringify({ rejectedCommandRoundTripMs: samples, averageMs: Math.round(samples.reduce((a,b)=>a+b,0)/samples.length) }));
} finally { clearTimeout(timer); provider.stop(); }
