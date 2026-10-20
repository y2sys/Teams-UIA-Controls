import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { Resvg } from '@resvg/resvg-js';
import { icon, presentation } from '../src/core.mjs';

const product = JSON.parse(await readFile('marketplace/product.json', 'utf8'));
const output = 'marketplace/assets';
await mkdir(output, { recursive: true });
const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
const text = (x, y, value, size = 34, color = '#E2E8F0', weight = 400) => `<text x="${x}" y="${y}" font-family="Segoe UI" font-size="${size}" font-weight="${weight}" fill="${color}">${escape(value)}</text>`;
const call = { id: 'preview', platform: 'teams', muted: true, cameraOn: false, sharing: false, capabilities: { microphone: true, camera: true, share: true, leave: true } };
function key(kind, x, y, size, changes = {}) {
  const view = presentation(kind, { session: { ...call, ...changes } });
  const source = Buffer.from(icon(kind, view, 'teams').split(',')[1], 'base64').toString();
  return `<svg x="${x}" y="${y}" width="${size}" height="${size}" viewBox="0 0 144 144">${source.replace(/^<svg[^>]*>/, '').replace(/<\/svg>$/, '')}</svg>`;
}
const footer = text(100, 906, 'Independent integration  |  Windows x64  |  Key previews from plugin artwork', 24, '#94A3B8');
function canvas(content) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="960"><defs><linearGradient id="bg" x2="1" y2="1"><stop stop-color="#0B1220"/><stop offset="1" stop-color="#172744"/></linearGradient></defs><rect width="1920" height="960" fill="url(#bg)"/><rect x="100" y="92" width="72" height="6" rx="3" fill="#5EEAD4"/>${content}${footer}</svg>`;
}
const images = {
  'thumbnail': canvas(text(100, 218, 'Teams UIA', 88, '#FFFFFF', 700) + text(100, 318, 'Controls', 88, '#FFFFFF', 700) + text(104, 400, 'Your meeting. Four keys.', 38) + text(104, 484, 'Microphone · Camera · Share · Leave', 30, '#94A3B8') + text(104, 582, 'For Stream Deck on Windows 11', 30) + text(104, 640, '.NET 10 Desktop Runtime required', 27, '#5EEAD4') + key('microphone', 1080, 155, 290) + key('camera', 1410, 155, 290) + key('share', 1080, 485, 290) + key('leave', 1410, 485, 290)),
  'gallery-01-states': canvas(text(100, 205, 'See the observed state', 72, '#FFFFFF', 700) + text(104, 280, 'Keys follow Teams, including changes made inside the meeting.', 32) + key('microphone', 170, 378, 290) + key('microphone', 590, 378, 290, { muted: false }) + key('camera', 1010, 378, 290) + key('camera', 1430, 378, 290, { cameraOn: true }) + text(190, 744, 'Mic muted', 32) + text(616, 744, 'Mic on', 32) + text(1038, 744, 'Camera off', 32) + text(1470, 744, 'Camera on', 32)),
  'gallery-02-sharing': canvas(text(100, 205, 'Choose what you share', 72, '#FFFFFF', 700) + text(104, 280, 'Open the Teams picker. Select a source yourself. Stop sharing with the same key.', 30) + key('share', 240, 390, 270) + key('share', 825, 390, 270, { picker: true }) + key('share', 1410, 390, 270, { sharing: true }) + text(248, 738, 'Ready to choose', 30) + text(814, 738, 'Choosing a source', 30) + text(1418, 738, 'Sharing active', 30)),
  'gallery-03-setup': canvas(text(100, 205, 'Set up once. Control locally.', 68, '#FFFFFF', 700) + text(104, 280, 'No Teams OAuth or app registration.', 34) + [
    ['1', 'Install the runtime', '.NET 10 Desktop Runtime', 'Windows x64 · installed separately'],
    ['2', 'Add four actions', 'Stream Deck 7.1 or later', 'Microphone · Camera · Share · Leave'],
    ['3', 'Join a Teams meeting', 'Desktop Teams on Windows 11', 'Japanese interface · Personal accounts tested']
  ].map(([n, title, line, detail], i) => { const x = 100 + i * 585; return `<rect x="${x}" y="370" width="550" height="330" rx="24" fill="#101827" stroke="#334155"/>${text(x + 32, 435, n, 38, '#5EEAD4', 700)}${text(x + 32, 505, title, 32, '#FFFFFF', 600)}${text(x + 32, 570, line, 27)}${text(x + 32, 625, detail, 21, '#94A3B8')}`; }).join('') + text(104, 800, 'Teams may briefly come forward during mic or camera actions.', 28, '#94A3B8'))
};
for (const [name, svg] of Object.entries(images)) {
  await writeFile(`${output}/${name}.svg`, svg);
  await writeFile(`${output}/${name}.png`, new Resvg(svg).render().asPng());
}
const app = Buffer.from(icon('microphone', { text: '', color: '#5EEAD4' }).split(',')[1], 'base64');
await writeFile(`${output}/app-icon.png`, new Resvg(app, { fitTo: { mode: 'width', value: 288 } }).render().asPng());
console.log(`Created ${Object.keys(images).length} Marketplace images and app icon for ${product.name}`);
