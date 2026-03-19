import { createTheme } from '@mui/material/styles';

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#4F6CF7',
      light: '#6B83F8',
      dark: '#3A54E0',
      contrastText: '#ffffff',
    },
    secondary: {
      main: '#818CF8',
      light: '#A5B0FB',
      dark: '#6366F1',
    },
    success: {
      main: '#16A34A',
      light: '#22C55E',
    },
    warning: {
      main: '#D97706',
      light: '#F59E0B',
    },
    error: {
      main: '#DC2626',
      light: '#EF4444',
    },
    background: {
      default: '#F8FAFC',
      paper: '#FFFFFF',
    },
    grey: {
      50:  '#F9FAFB',
      100: '#F3F4F6',
      200: '#E5E7EB',
      300: '#D1D5DB',
      400: '#9CA3AF',
      500: '#6B7280',
    },
    text: {
      primary:   '#111827',
      secondary: '#6B7280',
    },
    divider: '#E5E7EB',
  },

  typography: {
    fontFamily: '"Inter", "Helvetica Neue", Arial, sans-serif',
    h4: { fontWeight: 700, fontSize: '1.25rem', letterSpacing: '-0.3px', color: '#111827' },
    h5: { fontWeight: 700, fontSize: '1.05rem', letterSpacing: '-0.2px', color: '#111827' },
    h6: { fontWeight: 600, fontSize: '0.9375rem', color: '#111827' },
    subtitle1: { fontWeight: 500, fontSize: '0.875rem', color: '#374151' },
    subtitle2: { fontWeight: 400, color: '#6B7280', fontSize: '0.8125rem' },
    body1: { color: '#374151', fontSize: '0.875rem' },
    body2: { color: '#6B7280', fontSize: '0.8125rem' },
    caption: { color: '#9CA3AF', fontSize: '0.75rem' },
    button: {
      fontWeight: 600,
      textTransform: 'none',
      letterSpacing: '0.1px',
      fontSize: '0.875rem',
    },
  },

  shape: {
    borderRadius: 10,
  },

  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          padding: '7px 18px',
          boxShadow: 'none',
          fontSize: '0.875rem',
          fontWeight: 600,
          '&:hover': {
            boxShadow: '0 2px 8px rgba(79,108,247,0.20)',
          },
        },
        containedPrimary: {
          backgroundColor: '#4F6CF7',
          '&:hover': {
            backgroundColor: '#3A54E0',
            boxShadow: '0 2px 8px rgba(79,108,247,0.25)',
          },
        },
        containedSecondary: {
          backgroundColor: '#818CF8',
          '&:hover': {
            backgroundColor: '#6366F1',
          },
        },
        outlined: {
          borderColor: '#D1D5DB',
          color: '#374151',
          '&:hover': {
            borderColor: '#4F6CF7',
            color: '#4F6CF7',
            background: '#F5F8FF',
            boxShadow: 'none',
          },
        },
        outlinedPrimary: {
          borderColor: '#4F6CF7',
          color: '#4F6CF7',
          '&:hover': {
            background: '#EEF2FF',
            boxShadow: 'none',
          },
        },
        sizeLarge: {
          padding: '9px 24px',
          fontSize: '0.9375rem',
        },
        sizeSmall: {
          padding: '4px 12px',
          fontSize: '0.8125rem',
        },
      },
    },

    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 12,
          boxShadow: '0 1px 3px rgba(0,0,0,0.06), 0 1px 2px rgba(0,0,0,0.04)',
          border: '1px solid #E5E7EB',
        },
      },
    },

    MuiCardContent: {
      styleOverrides: {
        root: { padding: '20px 24px' },
      },
    },

    MuiTableHead: {
      styleOverrides: {
        root: {
          '& .MuiTableCell-head': {
            backgroundColor: '#F9FAFB',
            color: '#6B7280',
            fontWeight: 600,
            fontSize: '0.72rem',
            textTransform: 'uppercase',
            letterSpacing: '0.7px',
            borderBottom: '1px solid #E5E7EB',
          },
        },
      },
    },

    MuiTableRow: {
      styleOverrides: {
        root: {
          '&:hover': {
            backgroundColor: '#F5F8FF',
            cursor: 'pointer',
          },
          '&:last-child td': { border: 0 },
        },
      },
    },

    MuiTableCell: {
      styleOverrides: {
        root: {
          borderColor: '#F3F4F6',
          padding: '11px 16px',
          fontSize: '0.875rem',
          color: '#374151',
        },
      },
    },

    MuiChip: {
      styleOverrides: {
        root: {
          fontWeight: 600,
          fontSize: '0.7rem',
          borderRadius: 6,
          height: 24,
        },
      },
    },

    MuiLinearProgress: {
      styleOverrides: {
        root: { borderRadius: 99, height: 6, backgroundColor: '#E5E7EB' },
        bar: { borderRadius: 99 },
      },
    },

    MuiTextField: {
      defaultProps: { size: 'small' },
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': {
            borderRadius: 8,
            backgroundColor: '#FFFFFF',
            fontSize: '0.875rem',
            '& fieldset': { borderColor: '#D1D5DB' },
            '&:hover fieldset': { borderColor: '#9CA3AF' },
            '&.Mui-focused fieldset': { borderColor: '#4F6CF7', borderWidth: 1.5 },
          },
          '& .MuiInputLabel-root': {
            fontSize: '0.875rem',
            color: '#9CA3AF',
          },
        },
      },
    },

    MuiSelect: {
      defaultProps: { size: 'small' },
      styleOverrides: {
        outlined: {
          borderRadius: 8,
          fontSize: '0.875rem',
          backgroundColor: '#FFFFFF',
        },
      },
    },

    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          '& fieldset': { borderColor: '#D1D5DB' },
          '&:hover fieldset': { borderColor: '#9CA3AF' },
          '&.Mui-focused fieldset': { borderColor: '#4F6CF7', borderWidth: 1.5 },
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },

    MuiDrawer: {
      styleOverrides: {
        paper: { backgroundImage: 'none' },
      },
    },

    MuiDivider: {
      styleOverrides: {
        root: { borderColor: '#F3F4F6' },
      },
    },

    MuiAccordion: {
      styleOverrides: {
        root: {
          boxShadow: 'none',
          '&:before': { display: 'none' },
          border: '1px solid #E5E7EB',
          borderRadius: '8px !important',
          '&.Mui-expanded': { margin: 0 },
        },
      },
    },

    MuiAccordionSummary: {
      styleOverrides: {
        root: {
          minHeight: 48,
          '&.Mui-expanded': { minHeight: 48 },
          padding: '0 16px',
        },
        content: {
          margin: '12px 0',
          '&.Mui-expanded': { margin: '12px 0' },
        },
      },
    },

    MuiPagination: {
      styleOverrides: {
        root: { '& .MuiPaginationItem-root': { borderRadius: 8 } },
      },
    },

    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: 10, fontSize: '0.875rem' },
      },
    },

    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          backgroundColor: '#1F2937',
          fontSize: '0.75rem',
          borderRadius: 6,
          padding: '5px 10px',
        },
      },
    },
  },
});

export default theme;
