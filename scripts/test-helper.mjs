import { runDotnet } from './dotnet.mjs';
runDotnet(['run', '--project', 'tests/Helper.Checks/Helper.Checks.csproj', '-c', 'Release', '--', ...process.argv.slice(2)]);
