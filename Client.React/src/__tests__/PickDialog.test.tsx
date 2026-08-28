import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import PickDialog from '../components/PickDialog';

describe('PickDialog', () => {
  it('shows the team abbreviation and everyone who picked it, for a Spread pick', () => {
    render(
      <PickDialog
        open
        onClose={vi.fn()}
        userNames={['Alice', 'Bob']}
        userNamesOver={[]}
        userNamesUnder={[]}
        teamAbbr="SEA"
        pickType="Spread"
      />,
    );

    expect(screen.getByRole('heading', { name: 'SEA' })).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('shows the Over label and everyone who picked it, for an Over pick', () => {
    render(
      <PickDialog
        open
        onClose={vi.fn()}
        userNames={[]}
        userNamesOver={['Carlos']}
        userNamesUnder={[]}
        teamAbbr="SEA"
        pickType="Over"
      />,
    );

    expect(screen.getByText('Over')).toBeInTheDocument();
    expect(screen.getByText('Carlos')).toBeInTheDocument();
  });

  it('shows the Under label and everyone who picked it, for an Under pick', () => {
    render(
      <PickDialog
        open
        onClose={vi.fn()}
        userNames={[]}
        userNamesOver={[]}
        userNamesUnder={['Dana']}
        teamAbbr="SEA"
        pickType="Under"
      />,
    );

    expect(screen.getByText('Under')).toBeInTheDocument();
    expect(screen.getByText('Dana')).toBeInTheDocument();
  });

  // frizat: on iOS the dialog had no reachable escape hatch other than "carefully click behind
  // it" — an unbounded-height Dialog made the backdrop nearly unreachable. A real close control
  // is the fix; regression-test the control itself, not the visual sizing (unassertable via RTL).
  it('closes via an explicit close button, not just backdrop click', async () => {
    const onClose = vi.fn();
    render(
      <PickDialog
        open
        onClose={onClose}
        userNames={['Alice']}
        userNamesOver={[]}
        userNamesUnder={[]}
        teamAbbr="SEA"
        pickType="Spread"
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /close/i }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
