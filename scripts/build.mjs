import { build } from 'esbuild';
import { mkdir, writeFile, readFile, unlink } from 'node:fs/promises';
import { icon } from '../src/core.mjs';
import { Resvg } from '@resvg/resvg-js';
const root = 'com.meetingcontrols.streamdeck.sdPlugin';
await mkdir(`${root}/bin`, { recursive: true });
await mkdir(`${root}/imgs`, { recursive: true });
await mkdir(`${root}/ui`, { recursive: true });
await build({ entryPoints: ['src/plugin.ts'], bundle: true, platform: 'node', format: 'esm', target: 'node24', outfile: `${root}/bin/plugin.js`, banner: { js: "import { createRequire } from 'node:module'; const require = createRequire(import.meta.url);" } });
await writeFile(`${root}/bin/teams.ps1`, '\uFEFF' + (await readFile('src/teams.ps1', 'utf8')).replace(/^\uFEFF/, ''), 'utf8');
await writeFile(`${root}/bin/FocusReturnGuard.cs`, await readFile('src/FocusReturnGuard.cs'));
await writeFile(`${root}/bin/TransitionNotices.cs`, await readFile('src/TransitionNotices.cs'));
await writeFile(`${root}/bin/MeetingOsd.cs`, await readFile('src/MeetingOsd.cs'));
await writeFile(`${root}/package.json`, JSON.stringify({ type: 'module' }));
await writeFile(`${root}/ui/settings.html`, await readFile('src/settings.html'));
const items = [['microphone','マイク'],['camera','カメラ'],['share','画面共有'],['leave','退出']];
for (const [kind] of items) {
  const svg = Buffer.from(icon(kind, { text: '', color: '#cbd5e1' }).split(',')[1], 'base64');
  await writeFile(`${root}/imgs/${kind}.svg`, svg);
  for (const suffix of ['', '@2x']) await unlink(`${root}/imgs/${kind}${suffix}.png`).catch(error => { if (error.code !== 'ENOENT') throw error; });
  if (kind === 'microphone') for (const [suffix, width] of [['', 256], ['@2x', 512]]) await writeFile(`${root}/imgs/plugin${suffix}.png`, new Resvg(svg, { fitTo: { mode: 'width', value: width } }).render().asPng());
}
await writeFile(`${root}/manifest.json`, JSON.stringify({
  $schema: 'https://schemas.elgato.com/streamdeck/plugins/manifest.json',
  UUID: 'com.meetingcontrols.streamdeck', Name: 'Meeting Controls', Author: 'Meeting Controls',
  Description: '共通ボタンでWeb会議を操作します。現在はWindows版Teamsに対応。',
  Version: '0.1.0.0', SDKVersion: 2, CodePath: 'bin/plugin.js',
  Nodejs: { Version: '24' }, Software: { MinimumVersion: '7.1' },
  OS: [{ Platform: 'windows', MinimumVersion: '10' }], Icon: 'imgs/plugin',
  Category: 'Meeting Controls', CategoryIcon: 'imgs/microphone',
  Actions: items.map(([kind, name]) => ({ UUID: `com.meetingcontrols.streamdeck.${kind}`, Name: name, Icon: `imgs/${kind}`, Tooltip: name, Controllers: ['Keypad'], SupportedInMultiActions: false, PropertyInspectorPath: 'ui/settings.html', States: [{ Image: `imgs/${kind}`, ShowTitle: false }] }))
}, null, 2));
console.log(`Built ${root}`);
