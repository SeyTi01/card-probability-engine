import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const packageId = 'CardProbability.Engine';
export function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: repository, stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} ${args[0]} failed (${result.status}).`);
  return result.stdout?.trim();
}
export function capture(command, args, options = {}) {
  return run(command, args, { stdio: ['ignore', 'pipe', 'inherit'], encoding: 'utf8', ...options });
}
export function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}
export function requireVersion(version) {
  if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$/.test(version)) throw new Error('Expected a package version without build metadata.');
  return version;
}
export function requireCommit(commit) {
  if (!/^[0-9a-f]{40}$/.test(commit)) throw new Error('Expected a full lowercase Git source commit.');
  return commit;
}
