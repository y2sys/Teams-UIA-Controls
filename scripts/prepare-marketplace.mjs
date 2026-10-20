import { spawnSync } from 'node:child_process';
import { mkdir, readFile, copyFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import assert from 'node:assert/strict';

const product = JSON.parse(await readFile('marketplace/product.json', 'utf8'));
function run(args) {
  const result = spawnSync(process.execPath, args, { stdio: 'inherit', windowsHide: true });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`Preparation failed: ${args[0]} (${result.status})`);
}
run(['scripts/build.mjs', '--marketplace']);
run(['scripts/marketplace-assets.mjs']);
const directory = `dist/${product.uuid}.sdPlugin`;
const manifest = JSON.parse(await readFile(`${directory}/manifest.json`, 'utf8'));
assert.equal(manifest.Author, product.author);
assert.equal(manifest.UUID, product.uuid);
assert.equal(manifest.Version, product.version);
assert.ok(product.description.length >= 250 && product.description.length <= 1500);
const bundle = await readFile(`${directory}/bin/plugin.js`, 'utf8');
assert.ok(bundle.includes(product.uuid));
assert.ok(!bundle.includes('com.meetingcontrols.streamdeck.'));
const cli = 'node_modules/@elgato/cli/bin/streamdeck.mjs';
run([cli, 'validate', directory, '--no-update-check']);
await mkdir('dist/marketplace', { recursive: true });
// Use a predictable output directory, independent of the caller's CLI settings.
const result = spawnSync(process.execPath, [path.resolve(cli), 'pack', path.resolve(directory), '--no-update-check', '--no-file-list', '--force'], { cwd: path.resolve('dist/marketplace'), stdio: 'inherit', windowsHide: true });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error('Marketplace packaging failed');
const file = `${product.uuid}.streamDeckPlugin`;
const archive = await readFile(`dist/marketplace/${file}`);
await writeFile('dist/marketplace/SHA256SUMS.txt', `${createHash('sha256').update(archive).digest('hex')}  ${file}\n`);
for (const name of ['app-icon', 'thumbnail', 'gallery-01-states', 'gallery-02-sharing', 'gallery-03-setup'])
  await copyFile(`marketplace/assets/${name}.png`, `dist/marketplace/${name}.png`);
for (const name of ['README.md', 'listing.en.md', 'reviewer-notes.en.md'])
  await copyFile(`marketplace/${name}`, `dist/marketplace/${name}`);
console.log(`Submission candidate prepared: dist/marketplace/${file}`);
console.log('Not uploaded. See README.md for the remaining account and live-test checks.');
