import { test } from 'node:test';
import assert from 'node:assert/strict';
import { followPointer } from '../src/launcher-motion.js';

test('cross-window pointer movement follows gently instead of jumping', () => {
  const next = followPointer(0, 1000, 1 / 60);
  assert.ok(next > 0 && next < 80);
});
test('pointer response depends on elapsed time instead of frame count', () => {
  const move = fps => {
    let value = 0;
    for (let i = 0; i < fps / 2; i++) value = followPointer(value, 1000, 1 / fps);
    return value;
  };
  assert.ok(Math.abs(move(30) - move(60)) < .001);
});
test('returning from a hidden window cannot snap the background', () => {
  assert.ok(followPointer(0, 1000, 10) < 200);
});
test('pointer follows without overshoot in either direction', () => {
  assert.ok(followPointer(900, 100, .02) < 900);
  assert.ok(followPointer(900, 100, .02) > 100);
  assert.equal(followPointer(100, 100, .02), 100);
});
