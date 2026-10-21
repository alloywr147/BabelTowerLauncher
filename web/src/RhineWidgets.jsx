import { createContext, useContext, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { ContentTransition, SurfaceTransition } from './vendor/rhine/src/ui-transitions';

export const ReducedMotion = createContext(false);

export function useReducedMotion(motion) {
  // The launcher has an explicit, visible motion preference, defaulting on as requested.
  // Use the same switch for foreground and background instead of silently pausing one.
  return !motion;
}

// Retain the outgoing content until the upstream transition has finished.
// Inertness prevents actions against a page whose native context already changed.
export function useRhinePage(requestedPage, reduced) {
  const root = useRef(null);
  const surface = useRef(null);
  const displayed = useRef(requestedPage);
  const entries = useRef([]);
  const [page, setPage] = useState(requestedPage);
  const [switching, setSwitching] = useState(false);
  useLayoutEffect(() => {
    surface.current = new SurfaceTransition(root.current, root.current, 460, 180, 24, 14);
    root.current.hidden = true;
    return () => {
      surface.current?.dispose();
      entries.current.forEach(animation => animation.cancel());
    };
  }, []);
  useLayoutEffect(() => {
    displayed.current = page;
    window.scrollTo(0, 0);
    root.current.inert = false;
    surface.current.show(reduced);
    entries.current.forEach(animation => animation.cancel());
    entries.current = [];
    if (!reduced) {
      const section = root.current.firstElementChild;
      [...section.children].filter(element => !element.hidden).forEach((element, index) => {
        const animation = element.animate([
          { opacity: 0, transform: 'translateY(20px)' },
          { opacity: 1, transform: 'translateY(0)' }
        ], { duration: 520, delay: Math.min(index * 60, 240), easing: 'cubic-bezier(.22,1,.36,1)', fill: 'both' });
        entries.current.push(animation);
        animation.finished.then(() => animation.cancel()).catch(() => {});
      });
    }
    setSwitching(false);
  }, [page]);
  useLayoutEffect(() => {
    if (displayed.current === requestedPage) {
      // A quick return to the outgoing page must cancel the pending destination.
      if (root.current.dataset.transition === 'closing') {
        surface.current.show(reduced);
        root.current.inert = false;
        setSwitching(false);
      }
      if (reduced) {
        surface.current.finish();
        entries.current.forEach(animation => animation.cancel());
      }
      return;
    }
    setSwitching(true);
    root.current.inert = true;
    surface.current.hide(reduced, () => setPage(requestedPage));
  }, [requestedPage, reduced]);
  return { root, page, switching };
}

export function StatusValue({ value }) {
  const element = useRef(null);
  const lastValue = useRef(value);
  const transition = useRef(new ContentTransition(320, 6));
  const reduced = useContext(ReducedMotion);
  useLayoutEffect(() => {
    // The actual new status is rendered immediately; only its opacity animates.
    if (lastValue.current !== value) transition.current.reveal(element.current, reduced);
    lastValue.current = value;
  }, [value, reduced]);
  useEffect(() => () => transition.current.cancel(), []);
  return <span className="status-value" ref={element}>{value}</span>;
}

export function RollingNumber({ value, pad = 0 }) {
  const reduced = useContext(ReducedMotion);
  const digits = String(Math.max(0, Math.round(value))).padStart(pad, '0');
  const previous = useRef(digits);
  const [rolling, setRolling] = useState(false);
  useEffect(() => {
    if (previous.current === digits) return;
    previous.current = digits;
    if (reduced) { setRolling(false); return; }
    setRolling(true);
    const timer = setTimeout(() => setRolling(false), 600);
    return () => clearTimeout(timer);
  }, [digits, reduced]);
  return <span className={'rolling-number' + (rolling && !reduced ? ' is-rolling' : '')} aria-label={digits}>
    {[...digits].map((digit, index) => <span className="digit-window" aria-hidden="true" key={digits.length - index}>
      <span className="digit-strip" style={{ transform: `translateY(-${Number(digit)}em)`, transition: reduced ? 'none' : undefined }}>
        {[...Array(10)].map((_, number) => <span className="rolling-digit" key={number}>{number}</span>)}
      </span>
    </span>)}
  </span>;
}

export function useTerminalDialog(identity, reduced, onClosed) {
  const root = useRef(null);
  const panel = useRef(null);
  const transition = useRef(null);
  const closingRef = useRef(false);
  const callback = useRef(onClosed);
  const [closing, setClosing] = useState(false);
  callback.current = onClosed;
  useLayoutEffect(() => {
    if (!identity) return;
    closingRef.current = false;
    setClosing(false);
    root.current.inert = false;
    const surface = new SurfaceTransition(root.current, panel.current, 400, 220, 32, 18);
    transition.current = surface;
    root.current.hidden = true;
    surface.show(reduced);
    return () => { surface.dispose(); transition.current = null; };
  }, [identity]);
  useEffect(() => { if (reduced) transition.current?.finish(); }, [reduced]);
  const close = () => {
    if (closingRef.current || !transition.current) return false;
    closingRef.current = true;
    setClosing(true);
    root.current.inert = true;
    transition.current.hide(reduced, () => {
      closingRef.current = false;
      setClosing(false);
      callback.current();
    });
    return true;
  };
  return { root, panel, close, closing };
}
