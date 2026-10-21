// Preserve the deadline across refresh jitter instead of rounding 60 Hz down to 30 Hz.
export class FramePacer {
  next = null;
  rate = null;
  take(now, fps, hidden) {
    if (hidden) { this.next = null; return false; }
    const interval = 1000 / fps;
    if (this.next === null || this.rate !== fps) { this.next = now; this.rate = fps; }
    if (now + .75 < this.next) return false;
    if (now - this.next > interval) this.next = now;
    this.next += interval;
    return true;
  }
}
