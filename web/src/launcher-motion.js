// Time-based damping keeps fast cursor moves gentle at every refresh rate.
export function followPointer(value, target, elapsed) {
  const dt = Math.min(Math.max(elapsed, 0), .05);
  return value + (target - value) * (1 - Math.exp(-dt * 4));
}
