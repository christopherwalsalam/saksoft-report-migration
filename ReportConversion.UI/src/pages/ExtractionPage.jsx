import React, { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, TextField,
  Select, MenuItem, FormControl, InputLabel, Grid,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Pagination, Chip, CircularProgress, Alert, Stack, Divider,
} from '@mui/material';
import CloudDownloadIcon from '@mui/icons-material/CloudDownload';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { useQuery, useMutation } from '@tanstack/react-query';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import dayjs from 'dayjs';
import { startExtraction, getReports, getReportById } from '../api/extractionApi';
import useTaskStore from '../store/taskStore';
import useTaskPolling from '../hooks/useTaskPolling';
import TaskProgressCard from '../components/common/TaskProgressCard';
import DetailPanel from '../components/common/DetailPanel';

const REPORT_TYPES = ['All', 'Crystal', 'WebI'];

const ExtractionPage = () => {
  const { setTaskStarted, clearTask } = useTaskStore();
  const task = useTaskPolling('extraction');

  // Search filter state
  const [filters, setFilters] = useState({
    search: '', type: 'All', folder: '',
    fromDate: null, toDate: null,
  });
  const [activeFilters, setActiveFilters] = useState({
    search: '', type: 'All', folder: '',
    fromDate: null, toDate: null,
  });
  const [page, setPage] = useState(1);

  // Detail panel state
  const [selectedReportId, setSelectedReportId] = useState(null);
  const [panelOpen, setPanelOpen] = useState(false);

  const isTaskRunning = task.status === 'Pending' || task.status === 'Running';

  // Trigger extraction mutation
  const triggerMutation = useMutation({
    mutationFn: startExtraction,
    onSuccess: (result) => {
      const taskId = result?.taskId || result;
      setTaskStarted('extraction', taskId);
    },
  });

  // Fetch reports with active filters
  const { data: reportsData, isLoading: reportsLoading, isError: reportsError } = useQuery({
    queryKey: ['reports', activeFilters, page],
    queryFn: () =>
      getReports({
        search: activeFilters.search || undefined,
        type: activeFilters.type !== 'All' ? activeFilters.type : undefined,
        folder: activeFilters.folder || undefined,
        fromDate: activeFilters.fromDate ? dayjs(activeFilters.fromDate).format('YYYY-MM-DD') : undefined,
        toDate: activeFilters.toDate ? dayjs(activeFilters.toDate).format('YYYY-MM-DD') : undefined,
        page,
        pageSize: 20,
      }),
    keepPreviousData: true,
  });

  // Fetch detail for selected report
  const { data: reportDetail, isLoading: detailLoading, isError: detailError } = useQuery({
    queryKey: ['report-detail', selectedReportId],
    queryFn: () => getReportById(selectedReportId),
    enabled: !!selectedReportId && panelOpen,
  });

  const handleSearch = () => {
    setActiveFilters({ ...filters });
    setPage(1);
  };

  const handleClear = () => {
    const empty = { search: '', type: 'All', folder: '', fromDate: null, toDate: null };
    setFilters(empty);
    setActiveFilters(empty);
    setPage(1);
  };

  const handleRowClick = (id) => {
    setSelectedReportId(id);
    setPanelOpen(true);
  };

  const reports = reportsData?.data || [];
  const totalPages = reportsData?.totalPages || 1;
  const totalCount = reportsData?.totalCount || 0;

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <Box>
        {/* ─── Section A: Trigger ─── */}
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2}>
              <Box>
                <Typography variant="h6" fontWeight={700}>
                  Extract SAP BO Metadata
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Connects to SAP BusinessObjects and extracts report metadata, schedules, SQL queries, and data sources.
                </Typography>
              </Box>
              <Button
                variant="contained"
                size="large"
                startIcon={isTaskRunning ? <CircularProgress size={18} color="inherit" /> : <CloudDownloadIcon />}
                onClick={() => triggerMutation.mutate()}
                disabled={isTaskRunning || triggerMutation.isPending}
                sx={{ minWidth: 180 }}
              >
                {isTaskRunning ? 'Extracting...' : 'Start Extraction'}
              </Button>
            </Box>

            {triggerMutation.isError && (
              <Alert severity="error" sx={{ mt: 2 }}>
                Failed to start extraction: {triggerMutation.error?.response?.data?.error || triggerMutation.error?.message}
              </Alert>
            )}
          </CardContent>
        </Card>

        {/* Task progress */}
        <TaskProgressCard
          task={task}
          title="SAP BO Metadata Extraction"
          onClear={() => clearTask('extraction')}
        />

        <Divider sx={{ my: 3 }} />

        {/* ─── Section B: Reports Table ─── */}
        <Typography variant="h6" fontWeight={700} mb={2}>
          Extracted Reports
        </Typography>

        {/* Search filters */}
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
                  <InputLabel>Type</InputLabel>
                  <Select
                    label="Type"
                    value={filters.type}
                    onChange={(e) => setFilters((f) => ({ ...f, type: e.target.value }))}
                  >
                    {REPORT_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
                  </Select>
                </FormControl>
              </Grid>
              <Grid item xs={12} sm={3}>
                <TextField
                  fullWidth label="Folder Path"
                  value={filters.folder}
                  onChange={(e) => setFilters((f) => ({ ...f, folder: e.target.value }))}
                />
              </Grid>
              <Grid item xs={12} sm={3}>
                <DatePicker
                  label="Created From"
                  value={filters.fromDate}
                  onChange={(v) => setFilters((f) => ({ ...f, fromDate: v }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
              <Grid item xs={12} sm={3}>
                <DatePicker
                  label="Created To"
                  value={filters.toDate}
                  onChange={(v) => setFilters((f) => ({ ...f, toDate: v }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
              <Grid item xs={12} sm={9} />
              <Grid item xs="auto">
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
            {reportsLoading ? (
              <Box display="flex" justifyContent="center" py={6}>
                <CircularProgress />
              </Box>
            ) : reportsError ? (
              <Alert severity="error" sx={{ m: 2 }}>
                Failed to load reports.
              </Alert>
            ) : reports.length === 0 ? (
              <Box textAlign="center" py={6}>
                <Typography color="text.secondary">No reports found. Run extraction first.</Typography>
              </Box>
            ) : (
              <>
                <Box px={2} pt={2} pb={1} display="flex" justifyContent="space-between" alignItems="center">
                  <Typography variant="caption" color="text.secondary">
                    Showing {reports.length} of {totalCount.toLocaleString()} reports
                  </Typography>
                </Box>
                <TableContainer>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Report Name</TableCell>
                        <TableCell>Type</TableCell>
                        <TableCell>Folder</TableCell>
                        <TableCell>Created Date</TableCell>
                        <TableCell>Last Run Date</TableCell>
                        <TableCell>Status</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {reports.map((r) => (
                        <TableRow key={r.id} onClick={() => handleRowClick(r.id)} hover>
                          <TableCell>
                            <Typography variant="body2" fontWeight={500}>
                              {r.name || r.reportName}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Chip
                              label={r.type || r.reportType || '—'}
                              size="small"
                              color={r.type === 'Crystal' ? 'primary' : 'secondary'}
                              variant="outlined"
                            />
                          </TableCell>
                          <TableCell>
                            <Typography variant="caption" color="text.secondary">
                              {r.folderPath || r.folder || '—'}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="caption">
                              {r.createdDate ? new Date(r.createdDate).toLocaleDateString() : '—'}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="caption">
                              {r.lastRunDate ? new Date(r.lastRunDate).toLocaleDateString() : '—'}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <UsageStatusChip status={r.usageStatus || r.status} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
                <Box display="flex" justifyContent="center" py={2}>
                  <Pagination
                    count={totalPages}
                    page={page}
                    onChange={(_, v) => setPage(v)}
                    color="primary"
                    shape="rounded"
                  />
                </Box>
              </>
            )}
          </CardContent>
        </Card>

        {/* Detail panel */}
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

const UsageStatusChip = ({ status }) => {
  const map = {
    Active: { color: 'success', label: 'Active' },
    Stale: { color: 'warning', label: 'Stale' },
    NeverUsed: { color: 'default', label: 'Never Used' },
  };
  const cfg = map[status] || { color: 'default', label: status || '—' };
  return <Chip label={cfg.label} color={cfg.color} size="small" />;
};

export default ExtractionPage;
