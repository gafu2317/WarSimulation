import { readFileSync } from 'node:fs';
import vm from 'node:vm';

export const readGameSource = () => readFileSync(new URL('../js/country-v131.js', import.meta.url), 'utf8');

export function loadGame(context, source = readGameSource()) {
  for (const path of ['../js/balance.js', '../js/balance-combat.js']) {
    vm.runInContext(readFileSync(new URL(path, import.meta.url), 'utf8'), context, { filename: path });
  }
  vm.runInContext(source.split("document.addEventListener('dragstart'")[0], context, { filename: 'country-v131.js' });
}

export function loadEditorEvents(context, source = readGameSource()) {
  for (const [start, end] of [
    ["document.addEventListener('click', event => {", "document.addEventListener('input',event=>{"],
    ['function migrateTrainingRoute()', "window.addEventListener('pagehide'"]
  ]) {
    const first = source.indexOf(start), last = source.indexOf(end, first);
    if (first < 0 || last < first) throw new Error(`Game event boundary missing: ${start}`);
    vm.runInContext(source.slice(first, last), context);
  }
}
