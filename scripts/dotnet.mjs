import { existsSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const localSdk = path.join(root, '.tools/dotnet/dotnet.exe');
export const dotnet = process.env.DOTNET_EXE ?? (existsSync(localSdk) ? localSdk : 'dotnet');
export function runDotnet(args) {
  const home = path.join(root, '.tools/dotnet-home');
  const packages = path.join(root, '.tools/nuget');
  mkdirSync(home, { recursive: true });
  mkdirSync(packages, { recursive: true });
  const result = spawnSync(dotnet, args, { cwd: root, stdio: 'inherit', windowsHide: true,
    env: { ...process.env, DOTNET_CLI_HOME: home, NUGET_PACKAGES: packages,
      DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1' } });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`dotnet command failed (${result.status})`);
}
