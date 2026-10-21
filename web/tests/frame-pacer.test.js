import { test } from 'node:test';
import assert from 'node:assert/strict';
import { FramePacer } from '../src/frame-pacer.js';

function samples(refresh, fps, seconds = 5) {
  const pacer = new FramePacer();
  let count = 0;
  for (let tick = 0; tick < refresh * seconds; tick++) {
    if (pacer.take(tick * 1000 / refresh, fps, false)) count++;
  }
  return count / seconds;
}
test('60 Hz refresh draws every frame instead of accidentally halving cadence', () => assert.equal(samples(60, 60), 60));
test('144 Hz refresh stays near the 60 fps foreground target', () => assert.ok(Math.abs(samples(144, 60) - 60) < 1));
test('30 fps scheduling on 60 Hz does not drop additional frames', () => assert.equal(samples(60, 30), 30));
test('slower devices never accumulate catch-up renders', () => assert.equal(samples(20, 60), 20));
test('hidden windows do not render, and resume without a catch-up burst', () => {
  const pacer = new FramePacer();
  assert.equal(pacer.take(0, 60, false), true);
  assert.equal(pacer.take(1000, 60, true), false);
  assert.equal(pacer.take(2000, 60, false), true);
  assert.equal(pacer.take(2001, 60, false), false);
});
