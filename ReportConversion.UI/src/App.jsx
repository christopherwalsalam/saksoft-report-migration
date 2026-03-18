import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { SnackbarProvider } from 'notistack';

import theme from './theme';
import AppLayout from './components/layout/AppLayout';
import ExtractionPage from './pages/ExtractionPage';
import StaleReportsPage from './pages/StaleReportsPage';
import KpiGroupsPage from './pages/KpiGroupsPage';
import DuplicatesPage from './pages/DuplicatesPage';
import MigrationPage from './pages/MigrationPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30000,
      refetchOnWindowFocus: false,
    },
  },
});

const App = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <SnackbarProvider
          maxSnack={4}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          autoHideDuration={4000}
        >
          <BrowserRouter>
            <Routes>
              <Route path="/" element={<AppLayout />}>
                <Route index element={<Navigate to="/extraction" replace />} />
                <Route path="extraction" element={<ExtractionPage />} />
                <Route path="stale-reports" element={<StaleReportsPage />} />
                <Route path="kpi-groups" element={<KpiGroupsPage />} />
                <Route path="duplicates" element={<DuplicatesPage />} />
                <Route path="migration" element={<MigrationPage />} />
              </Route>
            </Routes>
          </BrowserRouter>
        </SnackbarProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
};

export default App;
