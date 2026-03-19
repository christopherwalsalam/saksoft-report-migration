import React from 'react';
import { useLocation } from 'react-router-dom';
import { AppBar, Toolbar, Typography, Box } from '@mui/material';
import NavigateNextIcon from '@mui/icons-material/NavigateNext';

const ROUTE_META = {
  '/extraction':    { title: 'SAP BO Extraction',   subtitle: 'Extract and review report metadata from SAP BusinessObjects.' },
  '/stale-reports': { title: 'Stale Reports',        subtitle: 'Identify reports that are unused or have not been accessed recently.' },
  '/kpi-groups':    { title: 'KPI Groups',           subtitle: 'Classify reports into KPI domains using AI-powered SQL analysis.' },
  '/duplicates':    { title: 'Duplicate Reports',    subtitle: 'Detect structurally similar or identical reports across the catalogue.' },
  '/migration':     { title: 'Power BI Migration',   subtitle: 'Generate and upload Power BI report files to Azure.' },
};

const Header = () => {
  const location = useLocation();
  const meta = ROUTE_META[location.pathname] || { title: 'Dashboard', subtitle: '' };

  return (
    <AppBar
      position="fixed"
      elevation={0}
      sx={{
        zIndex: (theme) => theme.zIndex.drawer - 1,
        bgcolor: '#FFFFFF',
        borderBottom: '1px solid #E5E7EB',
        left: 0,
        width: '100%',
      }}
    >
      <Toolbar sx={{ minHeight: '64px !important', px: 3 }}>
        <Box>
          <Typography
            sx={{
              fontSize: '1.25rem',
              fontWeight: 700,
              color: '#111827',
              lineHeight: 1.3,
            }}
          >
            {meta.title}
          </Typography>
          {meta.subtitle && (
            <Typography
              sx={{
                fontSize: '0.8125rem',
                color: '#9CA3AF',
                fontWeight: 400,
                lineHeight: 1.4,
              }}
            >
              {meta.subtitle}
            </Typography>
          )}
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default Header;
