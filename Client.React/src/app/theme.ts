import { createTheme } from '@mui/material/styles';

export function createAppTheme(mode: 'light' | 'dark') {
  const isDark = mode === 'dark';
  return createTheme({
  palette: {
    mode,
    // Professional dark navy primary with vibrant sports orange accent
    primary: {
      main: '#1a2847', // Professional dark navy
      dark: '#0f1729',
      light: '#2a3d5f',
      contrastText: '#FFFFFF',
    },
    secondary: {
      main: '#ff6b35', // Vibrant sports orange
      dark: '#e55a24',
      light: '#ff8555',
      contrastText: '#FFFFFF',
    },
    // Sports action accent - brighter for CTAs and important actions
    info: {
      main: '#3b82f6', // Professional blue
      light: '#60a5fa',
    },
    success: {
      main: '#10b981', // Emerald green for positive actions
      light: '#34d399',
    },
    warning: {
      main: '#f59e0b', // Amber for caution
      light: '#fbbf24',
    },
    error: {
      main: '#ef4444', // Bright red for errors
      light: '#f87171',
    },
    background: {
      default: isDark ? '#0f1729' : '#f9fafb',
      paper: isDark ? '#1a2440' : '#ffffff',
    },
    divider: isDark ? 'rgba(255,255,255,0.08)' : 'rgba(15, 23, 42, 0.1)',
  },
  typography: {
    fontFamily: '"Space Grotesk", "Segoe UI", system-ui, sans-serif',
    h1: {
      fontWeight: 800,
      fontFamily: '"Rajdhani", "Space Grotesk", sans-serif',
      letterSpacing: 0.4,
      color: isDark ? '#e2e8f0' : '#1a2847',
    },
    h2: {
      fontWeight: 700,
      fontFamily: '"Rajdhani", "Space Grotesk", sans-serif',
      letterSpacing: 0.3,
      color: isDark ? '#e2e8f0' : '#1a2847',
    },
    h3: {
      fontWeight: 700,
      fontFamily: '"Rajdhani", "Space Grotesk", sans-serif',
      color: isDark ? '#e2e8f0' : '#1a2847',
    },
    h4: {
      fontWeight: 700,
      color: isDark ? '#e2e8f0' : '#1a2847',
    },
    h5: {
      fontWeight: 600,
      color: isDark ? '#cbd5e1' : '#1f2937',
    },
    h6: {
      fontWeight: 600,
      color: isDark ? '#94a3b8' : '#374151',
    },
    button: { 
      textTransform: 'none', 
      fontWeight: 700, 
      letterSpacing: 0.2 ,
    },
  },
  shape: {
    borderRadius: 12,
  },
  components: {
    MuiAppBar: {
      styleOverrides: {
        root: {
          backdropFilter: 'blur(10px)',
          background: 'linear-gradient(90deg, rgba(26, 40, 71, 0.98), rgba(31, 41, 55, 0.96))',
          borderBottom: '1px solid rgba(255, 107, 53, 0.1)',
          boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          paddingInline: 20,
          paddingBlock: 10,
          minHeight: 44,
          minWidth: 44,
          transition: 'all 0.2s ease-in-out',
          '@media (hover: hover)': {
            '&:hover': {
              transform: 'translateY(-2px)',
              boxShadow: '0 4px 12px rgba(255, 107, 53, 0.15)',
            },
          },
          '&:active': {
            opacity: 0.8,
          },
        },
        contained: {
          '@media (hover: hover)': {
            '&:hover': {
              boxShadow: '0 4px 12px rgba(255, 107, 53, 0.25)',
            },
          },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          border: isDark ? '1px solid rgba(255,255,255,0.07)' : '1px solid rgba(15, 23, 42, 0.08)',
          transition: 'box-shadow 0.2s ease-in-out',
          '&:hover': {
            boxShadow: isDark ? '0 4px 12px rgba(0,0,0,0.3)' : '0 4px 12px rgba(0, 0, 0, 0.05)',
          },
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          border: isDark ? '1px solid rgba(255,255,255,0.07)' : '1px solid rgba(15, 23, 42, 0.08)',
          transition: 'all 0.2s ease-in-out',
          '&:hover': {
            borderColor: 'rgba(255, 107, 53, 0.2)',
            boxShadow: '0 8px 24px rgba(255, 107, 53, 0.08)',
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          fontWeight: 600,
          fontSize: '0.85rem',
        },
        filledSecondary: {
          backgroundColor: 'rgba(255, 107, 53, 0.1)',
          color: '#ff6b35',
          '&:hover': {
            backgroundColor: 'rgba(255, 107, 53, 0.15)',
          },
        },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          minHeight: 44,
          transition: 'all 0.15s ease-in-out',
          '@media (hover: hover)': {
            '&:hover': {
              backgroundColor: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(26, 40, 71, 0.06)',
            },
          },
          '&.active, &[class*="active"]': {
            backgroundColor: 'rgba(255, 107, 53, 0.12)',
            borderLeft: '3px solid #ff6b35',
            paddingLeft: 'calc(16px - 3px)',
          },
        },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          minHeight: 44,
          minWidth: 44,
          padding: '8px',
          '@media (hover: hover)': {
            '&:hover': {
              backgroundColor: isDark ? 'rgba(255,255,255,0.08)' : 'rgba(26, 40, 71, 0.06)',
            },
          },
        },
      },
    },
    MuiCircularProgress: {
      defaultProps: {
        color: isDark ? 'secondary' : 'primary',
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          fontWeight: 700,
          color: isDark ? '#e2e8f0' : '#1a2847',
          backgroundColor: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(26, 40, 71, 0.04)',
          borderBottom: isDark ? '2px solid rgba(255,255,255,0.1)' : '2px solid rgba(26, 40, 71, 0.12)',
        },
      },
    },
  },
  });
}

export const theme = createAppTheme('light');
