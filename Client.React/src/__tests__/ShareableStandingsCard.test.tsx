import { render, screen } from '@testing-library/react';
import { createRef } from 'react';
import ShareableStandingsCard from '../components/ShareableStandingsCard';

describe('ShareableStandingsCard', () => {
  it('renders the league name, user name, rank, and total', () => {
    render(
      <ShareableStandingsCard
        ref={createRef<HTMLDivElement>()}
        leagueName="Demo League"
        userName="Carlos"
        rank="1"
        total={45}
      />
    );

    expect(screen.getByText('Demo League')).toBeInTheDocument();
    expect(screen.getByText('Carlos')).toBeInTheDocument();
    expect(screen.getByText(/#1/)).toBeInTheDocument();
    expect(screen.getByText('+45')).toBeInTheDocument();
  });

  it('shows a negative total without a leading plus sign', () => {
    render(
      <ShareableStandingsCard
        ref={createRef<HTMLDivElement>()}
        leagueName="Demo League"
        userName="Bob"
        rank="4"
        total={-12}
      />
    );

    expect(screen.getByText('-12')).toBeInTheDocument();
  });

  it('shows zero without a sign', () => {
    render(
      <ShareableStandingsCard
        ref={createRef<HTMLDivElement>()}
        leagueName="Demo League"
        userName="Eve"
        rank="5"
        total={0}
      />
    );

    expect(screen.getByText('0')).toBeInTheDocument();
  });
});
