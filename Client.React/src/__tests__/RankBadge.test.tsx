import { render, screen } from '@testing-library/react';
import RankBadge from '../components/sports/RankBadge';

describe('RankBadge', () => {
  it('renders the rank when provided', () => {
    render(<RankBadge rank={3} />);
    expect(screen.getByText('#3')).toBeInTheDocument();
  });

  it('renders nothing when rank is null', () => {
    const { container } = render(<RankBadge rank={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when rank is undefined', () => {
    const { container } = render(<RankBadge rank={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });
});
