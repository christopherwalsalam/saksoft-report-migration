import React, { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, TextField,
  Select, MenuItem, FormControl, InputLabel, Grid,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Pagination, Chip, CircularProgress, Alert, Stack, Divider,
} from '@mui/material';
import ReportProblemIcon from '@mui/icons-material/ReportProblem';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { useQuery, useMutation } from '@tanstack/react-query';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import { startStaleReportAnalysis, getStaleReports, getStaleReportById } from '../api/staleReportsApi';
import useTaskStore from '../store/taskStore';
import useTaskPolling from '../hooks/useTaskPolling';
import TaskProgressCard from '../components/common/TaskProgressCard';
import DetailPanel from '../components/common/DetailPanel';

const STALE_REASONS = ['All', 'NeverUsed', 'Stale'];

const StaleReportsPage = () => {
  const { setTaskStarted, clearTask } = useTaskStore();
  const task = useTaskPolling('stale');

  const [filters, setFilters] = useState({
    search: '', staleReason: 'All', fromDate: null, toDate: null,
  });
  const [activeFilters, setActiveFilters] = useState({
    search: '', staleReason: 'All', fromDate: null, toDate: null,
  });
  const [page, setPage] = useState(1);
  const [selectedReportId, setSelectedReportId] = useState(null);
  const [panelOpen, setPanelOpen] = useState(false);

  const isTaskRunning = task.status === 'Pending' || task.status === 'Running';

  const triggerMutation = useMutation({
    mutationFn: startStaleReportAnalysis,
    onSuccess: (result) => {
      const taskId = result?.taskId || result;
      setTaskStarted('stale', taskId);
    },
  });

  const { data: reportsData, isLoading, isError } = useQuery({
    queryKey: ['stale-reports', activeFilters, page],
    queryFn: () =>
      getStaleReports({
        search: activeFilters.search || undefined,
        staleReason: activeFilters.staleReason !== 'All' ? activeFilters.staleReason : undefined,
        fromDate: activeFilters.fromDate ? dayjs(activeFilters.fromDate).format('YYYY-MM-DD') : undefined,
        toDate: activeFilters.toDate ? dayjs(activeFilters.toDate).format('YYYY-MM-DD') : undefined,
        page,
        pageSize: 20,
      }),
    keepPreviousData: true,
  });

  const { data: reportDetail, isLoading: detailLoading, isError: detailError } = useQuery({
    queryKey: ['stale-report-detail', selectedReportId],
    queryFn: () => getStaleReportById(selectedReportId),
    enabled: !!selectedReportId && panelOpen,
  });

  const handleSearch = () => {
    setActiveFilters({ ...filters });
    setPage(1);
  };

  const handleClear = () => {
    const empty = { search: '', staleReason: 'All', fromDate: null, toDate: null };
    setFilters(empty);
    setActiveFilters(empty);
    setPage(1);
  };

  const reports = reportsData?.data || [];
  const totalPages = reportsData?.totalPages || 1;
  const totalCount = reportsData?.totalCount || 0;

  const daysSince = (dateStr) => {
    if (!dateStr) return null;
    const diff = (new Date() - new Date(dateStr)) / (1000 * 60 * 60 * 24);
    return Math.floor(diff);
  };

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <Box>
        {/* ─── Section A: Trigger ─── */}
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2}>
              <Box>
                <Typography variant="h6" fontWeight={700}>
                  Identify Stale Reports
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Classifies reports as NeverUsed or Stale based on last run date, scheduling, and active subscriptions.
                </Typography>
              </Box>
              <Button
                variant="contained"
                size="large"
                color="warning"
                startIcon={isTaskRunning ? <CircularProgress size={18} color="inherit" /> : <ReportProblemIcon />}
                onClick={() => triggerMutation.mutate()}
                disabled={isTaskRunning || triggerMutation.isPending}
                sx={{ minWidth: 210 }}
              >
                {isTaskRunning ? 'Analysing...' : 'Run Stale Report Analysis'}
              </Button>
            </Box>
            {triggerMutation.isError && (
              <Alert severity="error" sx={{ mt: 2 }}>
                Failed to start analysis: {triggerMutation.error?.response?.data?.error || triggerMutation.error?.message}
              </Alert>
            )}
          </CardContent>
        </Card>

        <TaskProgressCard
          task={task}
          title="Stale Report Analysis"
          onClear={() => clearTask('stale')}
        />

        <Divider sx={{ my: 3 }} />

        {/* ─── Section B: Stale Reports Table ─── */}
        <Typography variant="h6" fontWeight={700} mb={2}>
          Stale &amp; Unused Reports
        </Typography>

        {/* Filters */}
        <Card sx={{ mb: 2 }}>
          <CardContent>
            <Grid container spacing={2} alignItems="flex-end">
              <Grid item xs={12} sm={4}>
                <TextField
                  fullWidth label="Report Name"
                  value={filters.search}
                  onChange={(e) => setFilters((f) => ({ ...f, search: e.target.value }))}
                  onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                />
              </Grid>
              <Grid item xs={12} sm={2}>
                <FormControl fullWidth>
                  <InputLabel>Stale Reason</InputLabel>
                  <Select
                    label="Stale Reason"
                    value={filters.staleReason}
                    onChange={(e) => setFilters((f) => ({ ...f, staleReason: e.target.value }))}
                  >
                    {STALE_REASONS.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
                  </Select>
                </FormControl>
              </Grid>
              <Grid item xs={12} sm={3}>
                <DatePicker
                  label="Last Accessed From"
                  value={filters.fromDate}
                  onChange={(v) => setFilters((f) => ({ ...f, fromDate: v }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
              <Grid item xs={12} sm={3}>
                <DatePicker
                  label="Last Accessed To"
                  value={filters.toDate}
                  onChange={(v) => setFilters((f) => ({ ...f, toDate: v }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
              <Grid item xs={12} display="flex" justifyContent="flex-end">
                <Stack direction="row" spacing={1}>
                  <Button variant="contained" startIcon={<SearchIcon />} onClick={handleSearch}>
                    Search
                  </Button>
                  <Button variant="outlined" startIcon={<ClearIcon />} onClick={handleClear}>
                    Clear
                  </Button>
                </Stack>
              </Grid>
            </Grid>
          </CardContent>
        </Card>

        {/* Table */}
        <Card>
          <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
            {isLoading ? (
              <Box display="flex" justifyContent="center" py={6}><CircularProgress /></Box>
            ) : isError ? (
              <Alert severity="error" sx={{ m: 2 }}>Failed to load stale reports.</Alert>
            ) : reports.length === 0 ? (
              <Box textAlign="center" py={6}>
                <Typography color="text.secondary">No stale reports found. Run the analysis first.</Typography>
              </Box>
            ) : (
              <>
                <Box px={2} pt={2} pb={1}>
                  <Typography variant="caption" color="text.secondary">
                    {totalCount.toLocaleString()} reports identified
                  </Typography>
                </Box>
                <TableContainer>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Report Name</TableCell>
                        <TableCell>Type</TableCell>
                        <TableCell>Last Accessed</TableCell>
                        <TableCell>Days Since Access</TableCell>
                        <TableCell>Stale Reason</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {reports.map((r) => {
                        const days = daysSince(r.lastRunDate || r.lastAccessedDate);
                        return (
                          <TableRow key={r.id} hover onClick={() => { setSelectedReportId(r.id); setPanelOpen(true); }}>
                            <TableCell>
                              <Typography variant="body2" fontWeight={500}>
                                {r.name || r.reportName}
                              </Typography>
                            </TableCell>
                            <TableCell>
                              <Chip label={r.type || r.reportType || '—'} size="small" variant="outlined"
                                color={r.type === 'Crystal' ? 'primary' : 'secondary'} />
                            </TableCell>
                            <TableCell>
                              <Typography variant="caption">
                                {r.lastRunDate ? new Date(r.lastRunDate).toLocaleDateString() : 'Never'}
                              </Typography>
                            </TableCell>
                            <TableCell>
                              {days != null ? (
                                <Chip
                                  label={`${days} days`}
                                  size="small"
                                  color={days > 365 ? 'error' : days > 180 ? 'warning' : 'default'}
                                />
                              ) : (
                                <Typography variant="caption" color="text.secondary">N/A</Typography>
                              )}
                            </TableCell>
                            <TableCell>
                              <StaleReasonChip reason={r.staleReason || r.usageStatus} />
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </TableContainer>
                <Box display="flex" justifyContent="center" py={2}>
                  <Pagination count={totalPages} page={page} onChange={(_, v) => setPage(v)} color="primary" shape="rounded" />
                </Box>
              </>
            )}
          </CardContent>
        </Card>

        <DetailPanel
          open={panelOpen}
          onClose={() => setPanelOpen(false)}
          title={reportDetail?.name || reportDetail?.reportName || 'Report Detail'}
          loading={detailLoading}
          error={detailError ? 'Failed to load report detail.' : null}
          report={reportDetail}
        />
      </Box>
    </LocalizationProvider>
  );
};

const StaleReasonChip = ({ reason }) => {
  const map = {
    NeverUsed: { color: 'default', label: 'Never Used' },
    Stale: { color: 'warning', label: 'Stale' },
  };
  const cfg = map[reason] || { color: 'default', label: reason || '—' };
  return <Chip label={cfg.label} color={cfg.color} size="small" />;
};

export default StaleReportsPage;
