import type { ReactNode } from 'react';
import { Box, Stack, Typography } from '@mui/material';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}

export default function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <Box sx={{ mb: 3 }}>
      {/* The action keeps its natural width and wraps below the title on a narrow screen, rather
          than being squeezed until its label spills past its own edges. */}
      <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2} useFlexGap flexWrap="wrap">
        <Typography variant="h4" className="section-title">
          {title}
        </Typography>
        {action && <Box sx={{ flexShrink: 0 }}>{action}</Box>}
      </Stack>
      {subtitle && (
        <Typography variant="body1" color="text.secondary">
          {subtitle}
        </Typography>
      )}
    </Box>
  );
}
