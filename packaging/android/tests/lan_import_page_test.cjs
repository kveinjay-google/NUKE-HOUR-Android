// Exercise the shipped page script without a phone or browser dependencies.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const html = fs.readFileSync(path.resolve(__dirname, '../../../android/OpenRA.Android/LanImport/index.html'), 'utf8');
const code = html.match(/<script>([\s\S]*)<\/script>/)[1];
(async () => {
  for (const mode of ['valid', 'missing', 'expired']) {
    const nodes = {};
    const element = id => nodes[id] ??= { hidden: true };
    const credential = mode === 'missing' ? '' : 'a'.repeat(64);
    let calls = 0;
    vm.runInNewContext(code, {
      document: { getElementById: element }, location: { hash: '#' + credential }, AbortSignal,
      fetch: async (url, options) => {
        calls++;
        assert.equal(url, '/status');
        assert.equal(options.headers['X-Import-Token'], credential);
        if (mode === 'expired') throw Error('Session closed');
        return { ok: true, json: async () => ({ state: 'receiving' }) };
      }
    });
    await new Promise(setImmediate);
    assert.equal(calls, mode === 'missing' ? 0 : 1);
    if (mode === 'valid') assert.equal(element('transfer').hidden, false);
    else {
      assert.equal(element('transfer').hidden, true);
      assert.ok(element('error').textContent);
    }
  }
  console.log('PASS: complete link auto-connect, missing credential and closed session stay blocked');
})().catch(error => { console.error(error); process.exitCode = 1; });
