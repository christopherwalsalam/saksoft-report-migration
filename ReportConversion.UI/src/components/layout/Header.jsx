import React from 'react';
import { useLocation } from 'react-router-dom';
import {
  AppBar, Toolbar, Typography, Box, Breadcrumbs, Link,
} from '@mui/material';
import NavigateNextIcon from '@mui/icons-material/NavigateNext';

const ROUTE_META = {
  '/extraction':    { title: 'SAP BO Extraction',    breadcrumbs: ['Migration Pipeline', 'SAP BO Extraction'] },
  '/stale-reports': { title: 'Stale Reports',         breadcrumbs: ['Migration Pipeline', 'Stale Reports'] },
  '/kpi-groups':    { title: 'KPI Groups',            breadcrumbs: ['Migration Pipeline', 'KPI Groups'] },
  '/duplicates':    { title: 'Duplicate Reports',     breadcrumbs: ['Migration Pipeline', 'Duplicate Reports'] },
  '/migration':     { title: 'Power BI Migration',    breadcrumbs: ['Migration Pipeline', 'Power BI Migration'] },
};

const Header = ({ sidebarWidth }) => {
  const location = useLocation();
  const meta = ROUTE_META[location.pathname] || { title: 'Dashboard', breadcrumbs: [] };

  return (
    <AppBar
      position="fixed"
      elevation={0}
      sx={{
        zIndex: (theme) => theme.zIndex.drawer - 1,
        bgcolor: '#FFFFFF',
        borderBottom: '1px solid #E0E0E0',
        left: sidebarWidth,
        width: `calc(100% - ${sidebarWidth}px)`,
        transition: 'left 0.2s ease, width 0.2s ease',
      }}
    >
      <Toolbar sx={{ minHeight: '64px !important', px: 3 }}>
        <Box>
          <Typography variant="h6" fontWeight={700} color="text.primary" lineHeight={1.2}>
            {meta.title}
          </Typography>
          {meta.breadcrumbs.length > 0 && (
            <Breadcrumbs
              separator={<NavigateNextIcon fontSize="small" />}
              aria-label="breadcrumb"
              sx={{ '& .MuiBreadcrumbs-separator': { mx: 0.5 } }}
            >
              {meta.breadcrumbs.map((crumb, idx) => (
                <Typography
                  key={idx}
                  variant="caption"
                  color={idx === meta.breadcrumbs.length - 1 ? 'primary.main' : 'text.secondary'}
                  fontWeight={idx === meta.breadcrumbs.length - 1 ? 600 : 400}
                >
                  {crumb}
                </Typography>
              ))}
            </Breadcrumbs>
          )}
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default Header;
