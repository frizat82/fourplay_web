import { Alert, Box, Button } from '@mui/material';
import PageHeader from './PageHeader';

interface InlineQueryErrorAlertProps {
  entityName: string;
  onRetry: () => void;
}

// frizat-05h: the scoped, non-full-page error affordance — used wherever only PART of a page's
// data failed to load and the rest of the page (a header, other sections, other controls) still
// works fine, so a full-page replacement would hide more than the actual failure. Extracted from
// QueryErrorAlert below after the identical Alert+Retry markup was found independently
// duplicated in LeagueCostsPage.tsx and reintroduced a third time in LeaguePortalPage.tsx's
// missing-picks error state — now the one place this pattern's copy/styling lives.
export function InlineQueryErrorAlert({ entityName, onRetry }: InlineQueryErrorAlertProps) {
  return (
    <Alert
      severity="error"
      action={<Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}
    >
      Couldn&apos;t load {entityName}. Check your connection and try again.
    </Alert>
  );
}

interface QueryErrorAlertProps {
  title: string;
  entityName?: string;
  onRetry: () => void;
}

// Full-page variant — a whole page's primary query failed, so nothing below the header rendered
// at all. Not for a partial/scoped failure where the rest of the page still works; see
// InlineQueryErrorAlert above for that case.
export default function QueryErrorAlert({ title, entityName = title.toLowerCase(), onRetry }: QueryErrorAlertProps) {
  return (
    <Box>
      <PageHeader title={title} />
      <Box sx={{ mt: 4 }}>
        <InlineQueryErrorAlert entityName={entityName} onRetry={onRetry} />
      </Box>
    </Box>
  );
}
