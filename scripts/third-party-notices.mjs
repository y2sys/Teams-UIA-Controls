import { readFile, writeFile } from 'node:fs/promises';
const packages = ['@elgato/streamdeck', '@elgato/utils', '@elgato/schemas', 'ws', 'zod'];
const sections = ['Teams UIA Controls — third-party notices\n\nThe following notices cover runtime JavaScript dependencies that may be included in the bundle. The .NET Desktop Runtime and Node.js runtime are not redistributed in this plugin. Development-only tools are not included.\n'];
for (const name of packages) {
  const info = JSON.parse(await readFile(`node_modules/${name}/package.json`, 'utf8'));
  const license = await readFile(`node_modules/${name}/LICENSE`, 'utf8');
  sections.push(`\n${'='.repeat(72)}\n${name} ${info.version}\n${license}\n`);
}
await writeFile('THIRD-PARTY-NOTICES.txt', `${sections.join('').trimEnd()}\n`);
