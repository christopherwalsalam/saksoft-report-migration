import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  Drawer, List, ListItemButton, ListItemIcon, ListItemText,
  Box, Typography, Tooltip, IconButton, Divider,
} from '@mui/material';
import CloudDownloadIcon from '@mui/icons-material/CloudDownload';
import ReportProblemIcon from '@mui/icons-material/ReportProblem';
import CategoryIcon from '@mui/icons-material/Category';
import FileCopyIcon from '@mui/icons-material/FileCopy';
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import MenuIcon from '@mui/icons-material/Menu';
import BarChartIcon from '@mui/icons-material/BarChart';

const SIDEBAR_FULL_WIDTH = 240;
const SIDEBAR_MINI_WIDTH = 64;

const NAV_ITEMS = [
  {
    path: '/extraction',
    label: 'SAP BO Extraction',
    icon: <CloudDownloadIcon />,
    description: 'Extract report metadata',
  },
  {
    path: '/stale-reports',
    label: 'Stale Reports',
    icon: <ReportProblemIcon />,
    description: 'Identify unused reports',
  },
  {
    path: '/kpi-groups',
    label: 'KPI Groups',
    icon: <CategoryIcon />,
    description: 'Group reports by KPI',
  },
  {
    path: '/duplicates',
    label: 'Duplicate Reports',
    icon: <FileCopyIcon />,
    description: 'Find duplicate reports',
  },
  {
    path: '/migration',
    label: 'Power BI Migration',
    icon: <RocketLaunchIcon />,
    description: 'Migrate to Power BI',
  },
];

const Sidebar = ({ collapsed, onToggle }) => {
  const navigate = useNavigate();
  const location = useLocation();

  const width = collapsed ? SIDEBAR_MINI_WIDTH : SIDEBAR_FULL_WIDTH;

  return (
    <Drawer
      variant="permanent"
      sx={{
        width,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width,
          boxSizing: 'border-box',
          transition: 'width 0.2s ease',
          overflowX: 'hidden',
          bgcolor: '#0D1B2A',
          color: '#FFFFFF',
          borderRight: 'none',
          boxShadow: '4px 0 20px rgba(0,0,0,0.15)',
        },
      }}
    >
      {/* Logo / App title */}
      <Box
        sx={{
          height: 64,
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'space-between',
          px: collapsed ? 0 : 2,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
        }}
      >
        {!collapsed && (
          <Box display="flex" alignItems="center" gap={1}>
            <BarChartIcon sx={{ color: '#29B6F6', fontSize: 26 }} />
            <Box>
              <Typography variant="subtitle2" fontWeight={700} sx={{ color: '#FFFFFF', lineHeight: 1.2 }}>
                ReportConversion
              </Typography>
              <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.45)', fontSize: '0.65rem' }}>
                SAP BO → Power BI
              </Typography>
            </Box>
          </Box>
        )}
        {collapsed && <BarChartIcon sx={{ color: '#29B6F6', fontSize: 26 }} />}
        {!collapsed && (
          <IconButton size="small" onClick={onToggle} sx={{ color: 'rgba(255,255,255,0.5)' }}>
            <ChevronLeftIcon />
          </IconButton>
        )}
      </Box>

      {/* Collapse toggle when mini */}
      {collapsed && (
        <Box display="flex" justifyContent="center" pt={1} pb={0.5}>
          <IconButton size="small" onClick={onToggle} sx={{ color: 'rgba(255,255,255,0.5)' }}>
            <MenuIcon />
          </IconButton>
        </Box>
      )}

      <Divider sx={{ borderColor: 'rgba(255,255,255,0.08)', mb: 1 }} />

      {/* Navigation items */}
      <List sx={{ px: collapsed ? 0.5 : 1.5 }} disablePadding>
        {NAV_ITEMS.map((item) => {
          const isActive = location.pathname === item.path ||
            (item.path !== '/' && location.pathname.startsWith(item.path));

          const btn = (
            <ListItemButton
              key={item.path}
              onClick={() => navigate(item.path)}
              sx={{
                borderRadius: 2,
                mb: 0.5,
                py: 1.2,
                px: collapsed ? 1 : 1.5,
                justifyContent: collapsed ? 'center' : 'flex-start',
                bgcolor: isActive ? 'rgba(41,182,246,0.15)' : 'transparent',
                borderLeft: isActive ? '3px solid #29B6F6' : '3px solid transparent',
                '&:hover': {
                  bgcolor: 'rgba(255,255,255,0.06)',
                },
              }}
            >
              <ListItemIcon
                sx={{
                  color: isActive ? '#29B6F6' : 'rgba(255,255,255,0.55)',
                  minWidth: collapsed ? 0 : 36,
                  mr: collapsed ? 0 : 0,
                }}
              >
                {item.icon}
              </ListItemIcon>
              {!collapsed && (
                <ListItemText
                  primary={item.label}
                  primaryTypographyProps={{
                    fontSize: '0.85rem',
                    fontWeight: isActive ? 600 : 400,
                    color: isActive ? '#FFFFFF' : 'rgba(255,255,255,0.7)',
                  }}
                />
              )}
            </ListItemButton>
          );

          return collapsed ? (
            <Tooltip title={item.label} placement="right" arrow key={item.path}>
              {btn}
            </Tooltip>
          ) : (
            btn
          );
        })}
      </List>

      {/* Bottom spacer */}
      <Box sx={{ flexGrow: 1 }} />
      {!collapsed && (
        <Box sx={{ p: 2, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.3)', fontSize: '0.65rem' }}>
            ReportConversion v1.0.0
          </Typography>
        </Box>
      )}
    </Drawer>
  );
};

export default Sidebar;
