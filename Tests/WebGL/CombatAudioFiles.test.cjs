const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function bridge(decode) {
  const sent = [];
  const listeners = {};
  const canvas = { getBoundingClientRect: () => ({ left: 10, top: 20, width: 400, height: 200 }) };
  const context = {
    LibraryManager: { library: {} },
    mergeInto: Object.assign,
    UTF8ToString: x => x,
    SendMessage: (receiver, method, json) => sent.push({ receiver, method, data: JSON.parse(json) }),
    HEAPF32: new Float32Array(32),
    Module: { canvas },
    window: { AudioContext: function() { this.decodeAudioData = decode; },
      addEventListener: (name, handler) => listeners[name] = handler,
      removeEventListener: name => delete listeners[name] },
    document: { createElement: () => ({ click() {} }) },
  };
  vm.createContext(context);
  vm.runInContext(fs.readFileSync('Assets/Plugins/WebGL/CombatAudioFiles.jslib', 'utf8'), context);
  context.CombatAudioFiles = context.LibraryManager.library.$CombatAudioFiles;
  const api = context.LibraryManager.library;
  api.CombatAudioFiles_Enable('AudioSettings');
  return { context, api, sent, listeners, canvas, state: context.CombatAudioFiles };
}
const file = (name = 'sound.wav', size = 10) => ({ name, size, arrayBuffer: async () => new ArrayBuffer(1) });
const audio = { length: 2, numberOfChannels: 2, sampleRate: 44100,
  getChannelData: channel => new Float32Array(channel ? [0.3, 0.4] : [0.1, 0.2]) };

test('browser drop converts CSS coordinates, preserves names, and imports stereo PCM', async () => {
  const b = bridge(async () => audio);
  let copied;
  b.context.SendMessage = (receiver, method, json) => {
    const data = JSON.parse(json);
    b.sent.push({ method, data });
    if (method === 'OnBrowserAudio') {
      assert.equal(b.api.CombatAudioFiles_Copy(data.id, 0, 4), 1);
      copied = Array.from(b.context.HEAPF32.slice(0, 4));
    }
  };
  let prevented = false;
  b.listeners.drop({ target: b.canvas, clientX: 110, clientY: 70,
    preventDefault() { prevented = true; }, dataTransfer: { files: [file('日本語の音.wav')] } });
  await new Promise(resolve => setImmediate(resolve));
  const imported = b.sent.find(x => x.method === 'OnBrowserAudio').data;
  assert.equal(prevented, true);
  assert.equal(imported.x, 0.25);
  assert.equal(imported.y, 0.75);
  assert.equal(imported.name, '日本語の音.wav');
  assert.deepEqual(copied.map(x => Math.round(x * 10)), [1, 3, 2, 4]);
  assert.equal(Object.keys(b.state.buffers).length, 0);
});

test('multiple files assign only the first and keep later imports in the library', async () => {
  const b = bridge(async () => audio);
  b.listeners.drop({ target: b.canvas, clientX: 110, clientY: 70, preventDefault() {},
    dataTransfer: { files: [file('a.wav'), file('b.wav')] } });
  await new Promise(resolve => setImmediate(resolve));
  const imported = b.sent.filter(x => x.method === 'OnBrowserAudio');
  assert.equal(imported.length, 2);
  assert.equal(imported[1].data.x, -1);
});

test('closing the panel discards pending decoding without assigning into a new panel', async () => {
  let finish;
  const b = bridge(() => new Promise(resolve => finish = resolve));
  const pending = b.state.read(file(), 0.5, 0.5, b.state.generation);
  await new Promise(resolve => setImmediate(resolve));
  b.api.CombatAudioFiles_Disable();
  b.api.CombatAudioFiles_Enable('NewPanel');
  finish(audio);
  await pending;
  assert.equal(b.sent.length, 0);
});

test('invalid formats and oversized files show an error without allocating PCM', async () => {
  const b = bridge(async () => { throw new Error('unsupported format'); });
  await b.state.read(file(), 0, 0, b.state.generation);
  await b.state.read(file('large.wav', 51 * 1024 * 1024), 0, 0, b.state.generation);
  assert.equal(b.sent.length, 2);
  assert.ok(b.sent.every(x => x.method === 'OnBrowserAudioError'));
  assert.equal(Object.keys(b.state.buffers).length, 0);
});
