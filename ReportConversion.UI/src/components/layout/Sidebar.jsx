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

const SIDEBAR_FULL_WIDTH = 248;
const SIDEBAR_MINI_WIDTH = 64;

const NAV_ITEMS = [
  { path: '/extraction',    label: 'SAP BO Extraction',  icon: <CloudDownloadIcon fontSize="small" /> },
  { path: '/stale-reports', label: 'Stale Reports',       icon: <ReportProblemIcon fontSize="small" /> },
  { path: '/kpi-groups',    label: 'KPI Groups',          icon: <CategoryIcon fontSize="small" /> },
  { path: '/duplicates',    label: 'Duplicate Reports',   icon: <FileCopyIcon fontSize="small" /> },
  { path: '/migration',     label: 'Power BI Migration',  icon: <RocketLaunchIcon fontSize="small" /> },
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
          bgcolor: '#FFFFFF',
          borderRight: '1px solid #E5E7EB',
          boxShadow: 'none',
        },
      }}
    >
      {/* ── Logo / App title ── */}
      <Box
        sx={{
          height: 64,
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'space-between',
          px: collapsed ? 0 : 2,
          borderBottom: '1px solid #F3F4F6',
        }}
      >
        {!collapsed && (
          <Box display="flex" alignItems="center" gap={1.5}>
            <Box
              sx={{
                width: 34,
                height: 34,
                borderRadius: 2,
                background: 'linear-gradient(135deg, #4F6CF7 0%, #818CF8 100%)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}
            >
              <BarChartIcon sx={{ color: '#FFFFFF', fontSize: 18 }} />
            </Box>
            <Box>
              <Typography
                sx={{
                  fontSize: '0.875rem',
                  fontWeight: 700,
                  color: '#111827',
                  lineHeight: 1.2,
                  whiteSpace: 'nowrap',
                }}
              >
                ReportConversion
              </Typography>
              <Typography
                sx={{ fontSize: '0.7rem', color: '#9CA3AF', lineHeight: 1.2 }}
              >
                SAP BO → Power BI
              </Typography>
            </Box>
          </Box>
        )}

        {collapsed && (
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: 2,
              background: 'linear-gradient(135deg, #4F6CF7 0%, #818CF8 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <BarChartIcon sx={{ color: '#FFFFFF', fontSize: 18 }} />
          </Box>
        )}

        {!collapsed && (
          <IconButton size="small" onClick={onToggle} sx={{ color: '#9CA3AF', '&:hover': { color: '#4F6CF7', bgcolor: '#EEF2FF' } }}>
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
        )}
      </Box>

      {/* ── Expand toggle when mini ── */}
      {collapsed && (
        <Box display="flex" justifyContent="center" pt={1.5}>
          <IconButton
            size="small"
            onClick={onToggle}
            sx={{ color: '#9CA3AF', '&:hover': { color: '#4F6CF7', bgcolor: '#EEF2FF' } }}
          >
            <MenuIcon fontSize="small" />
          </IconButton>
        </Box>
      )}

      {/* ── Section label ── */}
      {!collapsed && (
        <Box px={2} pt={2.5} pb={0.5}>
          <Typography
            sx={{
              fontSize: '0.65rem',
              fontWeight: 600,
              color: '#9CA3AF',
              textTransform: 'uppercase',
              letterSpacing: '0.8px',
            }}
          >
            Navigation
          </Typography>
        </Box>
      )}

      {/* ── Nav items ── */}
      <List sx={{ px: collapsed ? 0.75 : 1.5, pt: 0.5 }} disablePadding>
        {NAV_ITEMS.map((item) => {
          const isActive =
            location.pathname === item.path ||
            (item.path !== '/' && location.pathname.startsWith(item.path));

          const btn = (
            <ListItemButton
              key={item.path}
              onClick={() => navigate(item.path)}
              sx={{
                borderRadius: 2,
                mb: 0.25,
                py: 1,
                px: collapsed ? 1.25 : 1.25,
                justifyContent: collapsed ? 'center' : 'flex-start',
                minHeight: 40,
                bgcolor: isActive ? '#EEF2FF' : 'transparent',
                '&:hover': {
                  bgcolor: isActive ? '#EEF2FF' : '#F9FAFB',
                },
              }}
            >
              <ListItemIcon
                sx={{
                  color: isActive ? '#4F6CF7' : '#9CA3AF',
                  minWidth: collapsed ? 0 : 32,
                  '& svg': { fontSize: '1.1rem' },
                }}
              >
                {item.icon}
              </ListItemIcon>
              {!collapsed && (
                <ListItemText
                  primary={item.label}
                  primaryTypographyProps={{
                    fontSize: '0.875rem',
                    fontWeight: isActive ? 600 : 400,
                    color: isActive ? '#4F6CF7' : '#374151',
                    whiteSpace: 'nowrap',
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

      {/* ── Spacer ── */}
      <Box sx={{ flexGrow: 1 }} />

      {/* ── Footer ── */}
      {!collapsed && (
        <Box sx={{ p: 2, borderTop: '1px solid #F3F4F6' }}>
          <Typography sx={{ fontSize: '0.7rem', color: '#D1D5DB' }}>
            v1.0.0
          </Typography>
        </Box>
      )}
    </Drawer>
  );
};

export default Sidebar;
