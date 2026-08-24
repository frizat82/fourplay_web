import { act, renderHook } from '@testing-library/react';
import { useNumericField } from '../utils/useNumericField';

describe('useNumericField', () => {
  it('reflects the initial value as a string', () => {
    const { result } = renderHook(() => useNumericField(13, () => {}));
    expect(result.current.value).toBe('13');
  });

  it('preserves an incomplete intermediate value without pushing it upward', () => {
    const onChange = vi.fn();
    const { result } = renderHook(() => useNumericField(1, onChange));

    act(() => result.current.onChange({ target: { value: '-' } } as React.ChangeEvent<HTMLInputElement>));

    expect(result.current.value).toBe('-');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('pushes the parsed number upward once the value is valid', () => {
    const onChange = vi.fn();
    const { result } = renderHook(() => useNumericField(1, onChange));

    act(() => result.current.onChange({ target: { value: '1.5' } } as React.ChangeEvent<HTMLInputElement>));

    expect(result.current.value).toBe('1.5');
    expect(onChange).toHaveBeenCalledWith(1.5);
  });

  it('snaps back to the last valid value on blur after an incomplete edit', () => {
    const { result } = renderHook(() => useNumericField(7, () => {}));

    act(() => result.current.onChange({ target: { value: '' } } as React.ChangeEvent<HTMLInputElement>));
    expect(result.current.value).toBe('');

    act(() => result.current.onBlur());
    expect(result.current.value).toBe('7');
  });

  it('does not clobber a mid-edit decimal when backspacing produces the same committed number', () => {
    // /code-review finding: 17.5 -> backspace to "17." parses to 17, which matches the
    // previously-committed 17.5's new value of 17 once rounded down — the resync effect
    // must not stomp "17." back to "17" just because the committed number didn't change
    // in a way distinguishable from an external update.
    const onChange = vi.fn();
    const { result, rerender } = renderHook(({ value }) => useNumericField(value, onChange), {
      initialProps: { value: 17.5 },
    });

    act(() => result.current.onChange({ target: { value: '17.' } } as React.ChangeEvent<HTMLInputElement>));
    expect(onChange).toHaveBeenCalledWith(17);

    rerender({ value: 17 });
    expect(result.current.value).toBe('17.');
  });

  it('does not push Infinity upward for overflowing/scientific-notation input', () => {
    const onChange = vi.fn();
    const { result } = renderHook(() => useNumericField(1, onChange));

    act(() => result.current.onChange({ target: { value: '1e400' } } as React.ChangeEvent<HTMLInputElement>));

    expect(result.current.value).toBe('1e400');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('resyncs the displayed value when the parent value prop changes', () => {
    const { result, rerender } = renderHook(({ value }) => useNumericField(value, () => {}), {
      initialProps: { value: 5 },
    });
    expect(result.current.value).toBe('5');

    rerender({ value: 20 });
    expect(result.current.value).toBe('20');
  });
});
