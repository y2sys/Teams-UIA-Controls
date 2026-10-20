import { build } from 'esbuild';
import { mkdir, writeFile, readFile, unlink, copyFile } from 'node:fs/promises';
import { runDotnet } from './dotnet.mjs';
import { icon, actionListIcon } from '../src/core.mjs';
import { Resvg } from '@resvg/resvg-js';
const product = JSON.parse(await readFile('marketplace/product.json', 'utf8'));
const marketplace = process.argv.includes('--marketplace');
const uuid = marketplace ? product.uuid : 'com.meetingcontrols.streamdeck';
const root = marketplace ? `dist/${uuid}.sdPlugin` : `${uuid}.sdPlugin`;
await mkdir(`${root}/bin`, { recursive: true });
await mkdir(`${root}/imgs`, { recursive: true });
await mkdir(`${root}/ui`, { recursive: true });
await build({ entryPoints: ['src/plugin.ts'], bundle: true, platform: 'node', format: 'esm', target: 'node24', define: { __PLUGIN_UUID__: JSON.stringify(uuid) }, outfile: `${root}/bin/plugin.js`, banner: { js: "import { createRequire } from 'node:module'; const require = createRequire(import.meta.url);" } });
runDotnet(['publish', 'native/MeetingControls.Helper/MeetingControls.Helper.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', '.tools/helper-framework-dependent']);
await copyFile('.tools/helper-framework-dependent/MeetingControls.Helper.exe', `${root}/bin/MeetingControls.Helper.exe`);
// Remove only the known legacy runtime files from this project's build output.
for (const file of ['teams.ps1', 'FocusReturnGuard.cs', 'TransitionNotices.cs', 'MeetingOsd.cs']) {
  await unlink(`${root}/bin/${file}`).catch(error => { if (error.code !== 'ENOENT') throw error; });
}
await writeFile(`${root}/package.json`, JSON.stringify({ type: 'module' }));
await writeFile(`${root}/ui/settings.html`, await readFile('src/settings.html'));
await import('./third-party-notices.mjs');
await copyFile('THIRD-PARTY-NOTICES.txt', `${root}/THIRD-PARTY-NOTICES.txt`);
await copyFile('docs/SETUP.en.md', `${root}/SETUP.txt`);
const items = [['microphone','マイク'],['camera','カメラ'],['share','画面共有'],['leave','退出']];
for (const [kind] of items) {
  const svg = Buffer.from(icon(kind, { text: '', color: '#cbd5e1' }).split(',')[1], 'base64');
  await writeFile(`${root}/imgs/${kind}.svg`, actionListIcon(kind));
  await writeFile(`${root}/imgs/${kind}-key.svg`, svg);
  for (const suffix of ['', '@2x']) await unlink(`${root}/imgs/${kind}${suffix}.png`).catch(error => { if (error.code !== 'ENOENT') throw error; });
  if (kind === 'microphone') for (const [suffix, width] of [['', 256], ['@2x', 512]]) await writeFile(`${root}/imgs/plugin${suffix}.png`, new Resvg(svg, { fitTo: { mode: 'width', value: width } }).render().asPng());
}
await writeFile(`${root}/manifest.json`, JSON.stringify({
  $schema: 'https://schemas.elgato.com/streamdeck/plugins/manifest.json',
  UUID: uuid, Name: product.name, Author: product.author,
  Description: product.description,
  ...(product.publicLinksVerified ? { URL: product.website } : {}),
  Version: product.version, SDKVersion: marketplace ? 3 : 2, CodePath: 'bin/plugin.js',
  Nodejs: { Version: '24' }, Software: { MinimumVersion: '7.1' },
  OS: [{ Platform: 'windows', MinimumVersion: marketplace ? '10.0.22000' : '10' }], Icon: 'imgs/plugin',
  Category: 'Teams UIA Controls', CategoryIcon: 'imgs/microphone',
  Actions: items.map(([kind, name]) => ({ UUID: `${uuid}.${kind}`, Name: name, Icon: `imgs/${kind}`, Tooltip: name, Controllers: ['Keypad'], SupportedInMultiActions: false, PropertyInspectorPath: 'ui/settings.html', States: [{ Image: `imgs/${kind}-key`, ShowTitle: false }] }))
}, null, 2));
console.log(`Built ${root}`);
