import { useEffect, useRef, useState, type ChangeEvent } from 'react';

interface UseNumericFieldOptions {
  // Strips a typed decimal point instead of accepting it — for fields whose backend DTO is
  // an `int` (e.g. Juice/Cost settings), where a decimal value would otherwise display fine
  // but 400 silently on save.
  integerOnly?: boolean;
}

// Buffers a numeric <TextField>'s displayed value as a raw string so mid-edit states
// (empty, "-", "17.") aren't stomped back to a parsed number on every keystroke. Only
// pushes a value upward once it actually parses to a finite number; resyncs from `value`
// on a genuinely external change (not one this hook itself just committed) and on blur.
export function useNumericField(value: number, onChange: (n: number) => void, options?: UseNumericFieldOptions) {
  const integerOnly = options?.integerOnly ?? false;
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
      const typed = e.target.value;
      const str = integerOnly ? (typed.startsWith('-') ? '-' : '') + typed.replace(/\D/g, '') : typed;
      setRaw(str);
      const parsed = Number(str);
      if (str.trim() !== '' && str !== '-' && Number.isFinite(parsed)) {
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
