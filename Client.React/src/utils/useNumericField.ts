import { useEffect, useRef, useState, type ChangeEvent } from 'react';

// Buffers a numeric <TextField>'s displayed value as a raw string so mid-edit states
// (empty, "-", "17.") aren't stomped back to a parsed number on every keystroke. Only
// pushes a value upward once it actually parses to a finite number; resyncs from `value`
// on a genuinely external change (not one this hook itself just committed) and on blur.
export function useNumericField(value: number, onChange: (n: number) => void) {
  const [raw, setRaw] = useState(String(value));
  const lastCommitted = useRef(value);
  useEffect(() => {
    // Skip if `value` only changed because our own onChange call just pushed it —
    // otherwise a mid-edit string that already parses to the new value (e.g. "17."
    // while committing 17) gets its trailing characters stomped on the very next render.
    if (value === lastCommitted.current) return;
    lastCommitted.current = value;
    setRaw(String(value));
  }, [value]);
  return {
    value: raw,
    onChange: (e: ChangeEvent<HTMLInputElement>) => {
      const str = e.target.value;
      setRaw(str);
      const parsed = Number(str);
      if (str.trim() !== '' && Number.isFinite(parsed)) {
        lastCommitted.current = parsed;
        onChange(parsed);
      }
    },
    // Leaving the field empty or mid-typed ("-", "17.") snaps back to the last valid number
    // once the user clicks away, rather than silently saving a value that doesn't match what's
    // displayed.
    onBlur: () => setRaw(String(value)),
  };
}
