import React, { useState } from 'react';
import { Box } from '@mui/material';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import Header from './Header';

const SIDEBAR_FULL_WIDTH = 240;
const SIDEBAR_MINI_WIDTH = 64;

const AppLayout = () => {
  const [collapsed, setCollapsed] = useState(false);

  const sidebarWidth = collapsed ? SIDEBAR_MINI_WIDTH : SIDEBAR_FULL_WIDTH;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed((v) => !v)} />

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          ml: 0,
          display: 'flex',
          flexDirection: 'column',
          minHeight: '100vh',
        }}
      >
        <Header sidebarWidth={sidebarWidth} />

        {/* Page content — pushed below fixed AppBar */}
        <Box sx={{ mt: '64px', p: 3, flexGrow: 1 }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
};

export default AppLayout;
