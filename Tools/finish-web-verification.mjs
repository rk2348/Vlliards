import { readFileSync, appendFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { setTimeout } from 'node:timers/promises';

const log = 'Temp/WebVerifyBuild.log';
const resultLog = 'Temp/WebVerification.log';
const deadline = Date.now() + 60 * 60 * 1000;
const report = message => {
  const line = `${new Date().toISOString()} ${message}\n`;
  appendFileSync(resultLog, line);
  process.stdout.write(line);
};
report('Waiting for the current Unity WebGL build.');
while (true) {
  const output = readFileSync(log, 'utf8');
  if (/Build Finished, Result: Success\.|Build completed with a result of 'Succeeded'/.test(output) &&
      /Exiting batchmode successfully|Application will terminate with return code 0/.test(output)) break;
  if (/BuildFailedException|Build Finished, Result: Failed|Aborting batchmode|Application will terminate with return code [1-9]/.test(output)) {
    report(`FAIL: Unity build failed; see ${log}.`);
    process.exit(1);
  }
  if (Date.now() > deadline) {
    report(`FAIL: timed out waiting for Unity; see ${log}.`);
    process.exit(1);
  }
  await setTimeout(10000);
}
for (const [script, cwd] of [
  ['Tools/export-web-site.mjs', process.cwd()],
  ['check-build.mjs', `${process.cwd()}/Sites/vlliards-mobile`],
]) {
  const result = spawnSync(process.execPath, [script], { cwd, encoding: 'utf8', windowsHide: true });
  report((result.stdout || '') + (result.stderr || ''));
  if (result.error || result.status !== 0) {
    report(`FAIL: ${script}: ${result.error?.message || result.status}`);
    process.exit(1);
  }
}
report('PASS: WebGL rebuilt, exported, and artifact integrity verified. Browser and device tests remain pending.');
