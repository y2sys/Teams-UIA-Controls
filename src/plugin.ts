import streamDeck, { action, SingletonAction, type KeyDownEvent, type WillAppearEvent, type DidReceiveSettingsEvent } from '@elgato/streamdeck';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { TeamsProvider } from './bridge.mjs';
import { selectSession, presentation, icon } from './core.mjs';

// The Marketplace build has its own publisher-qualified identity. Local builds
// keep the existing UUID so the user's current key assignments remain valid.
declare const __PLUGIN_UUID__: string;
const PLUGIN_UUID = __PLUGIN_UUID__;

const provider = new TeamsProvider(path.join(path.dirname(fileURLToPath(import.meta.url)), 'MeetingControls.Helper.exe'));
const locks = new Set<string>();
let target = 'auto';
const instances: MeetingAction[] = [];
class MeetingAction extends SingletonAction {
  kind: string;
  rendered = new Map<string, string>();
  constructor(kind: string) { super(); this.kind = kind; instances.push(this); }
  async render() {
    const selection = selectSession(provider.snapshot, target);
    const view = presentation(this.kind, selection);
    for (const key of this.actions) {
      if (!key.isKey()) continue;
      const signature = JSON.stringify([view, selection.session?.platform]);
      if (this.rendered.get(key.id) === signature) continue;
      await key.setImage(icon(this.kind, view, selection.session?.platform));
      // The SVG carries the platform name. State is conveyed by icon and color.
      await key.setTitle('');
      this.rendered.set(key.id, signature);
    }
  }
  async onWillAppear(_ev: WillAppearEvent) { this.rendered.clear(); await this.render(); }
  async onDidReceiveSettings(_ev: DidReceiveSettingsEvent) { this.rendered.clear(); await this.render(); }
  async onPropertyInspectorDidAppear() { await sendStatus(); }
  async onSendToPlugin(ev: any) {
    if (ev.payload?.event === 'open-runtime') {
      await streamDeck.system.openUrl('https://dotnet.microsoft.com/en-us/download/dotnet/10.0');
      return;
    }
    if (ev.payload?.event === 'target' && typeof ev.payload.target === 'string') {
      target = ev.payload.target;
      await streamDeck.settings.setGlobalSettings({ target });
      await renderAll();
    }
    await sendStatus();
  }
  async onKeyDown(ev: KeyDownEvent) {
    const { session } = selectSession(provider.snapshot, target);
    if (!session || session.picker || !session.capabilities?.[this.kind] || locks.has(session.id)) {
      await ev.action.showAlert(); return;
    }
    locks.add(session.id);
    try { await provider.execute(this.kind, session); }
    catch { await ev.action.showAlert(); }
    finally { setTimeout(() => locks.delete(session.id), 150); }
  }
}
@action({ UUID: `${PLUGIN_UUID}.microphone` })
class Microphone extends MeetingAction { constructor() { super('microphone'); } }
@action({ UUID: `${PLUGIN_UUID}.camera` })
class Camera extends MeetingAction { constructor() { super('camera'); } }
@action({ UUID: `${PLUGIN_UUID}.share` })
class Share extends MeetingAction { constructor() { super('share'); } }
@action({ UUID: `${PLUGIN_UUID}.leave` })
class Leave extends MeetingAction { constructor() { super('leave'); } }
for (const handler of [new Microphone(), new Camera(), new Share(), new Leave()]) streamDeck.actions.registerAction(handler);
async function sendStatus() {
  await streamDeck.ui.sendToPropertyInspector({ event: 'status', target, sessions: (provider.snapshot?.sessions ?? []).map((s: any) => ({ id: s.id, platform: s.platform, title: s.title, picker: s.picker })), reason: selectSession(provider.snapshot, target).reason ?? '', error: provider.snapshot?.error ?? '' });
}
async function renderAll() { await Promise.all(instances.map(item => item.render())); }
function update() { renderAll().catch(() => {}); sendStatus().catch(() => {}); }
provider.on('state', update);
await streamDeck.connect();
const settings = await streamDeck.settings.getGlobalSettings();
target = typeof settings.target === 'string' ? settings.target : 'auto';
provider.start();
setInterval(update, 1000);
process.on('exit', () => provider.stop());
