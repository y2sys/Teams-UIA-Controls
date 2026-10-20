import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { EventEmitter } from 'node:events';

export class TeamsProvider extends EventEmitter {
  platform = 'teams';
  snapshot;
  pending = new Map();
  sequence = 0;
  stopped = false;
  constructor(executable) { super(); this.executable = executable; }
  start() {
    if (this.stopped) return;
    this.snapshot = undefined;
    const child = this.child = spawn(this.executable, [], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    let lastMessage = Date.now();
    const lines = createInterface({ input: child.stdout });
    lines.on('line', line => {
      try {
        const data = JSON.parse(line.replace(/^\uFEFF/, ''));
        lastMessage = Date.now();
        if (data.type === 'state') {
          this.snapshot = { ...data, receivedAt: Date.now() };
          this.emit('state', this.snapshot);
        } else if (data.type === 'result') {
          const task = this.pending.get(data.requestId);
          if (task) { this.pending.delete(data.requestId); data.ok ? task.resolve(data) : task.reject(new Error(data.error)); }
        }
      } catch { /* A malformed record never becomes a valid state. */ }
    });
    // Drain stderr but do not log meeting content or user names.
    child.stderr.on('data', () => {});
    child.stdin.on('error', () => {});
    const watchdog = setInterval(() => { if (Date.now() - lastMessage > 7000) child.kill(); }, 1000);
    const fail = () => {
      clearInterval(watchdog);
      if (this.child !== child) return;
      this.snapshot = undefined;
      this.emit('state', undefined);
      for (const task of this.pending.values()) task.reject(new Error('Teams bridge disconnected'));
      this.pending.clear();
      this.child = undefined;
      if (!this.stopped) this.retry = setTimeout(() => this.start(), 2000);
    };
    child.once('error', fail);
    child.once('exit', fail);
  }
  async execute(kind, session) {
    if (!this.child || !this.snapshot || Date.now() - this.snapshot.receivedAt > 4000) throw new Error('State unavailable');
    const requestId = String(++this.sequence);
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(requestId); reject(new Error('Teams command timed out')); }, 5000);
      this.pending.set(requestId, { resolve: value => { clearTimeout(timer); resolve(value); }, reject: error => { clearTimeout(timer); reject(error); } });
      this.child.stdin.write(JSON.stringify({ requestId, kind, sessionId: session.id }) + '\n');
    });
  }
  stop() { this.stopped = true; clearTimeout(this.retry); this.child?.kill(); }
}
