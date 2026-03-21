import React, { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, TextField,
  Grid, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Pagination, Chip, CircularProgress, Alert, Stack, Divider,
  Accordion, AccordionSummary, AccordionDetails, InputAdornment,
  Select, MenuItem, FormControl, InputLabel,
} from '@mui/material';
import FileCopyIcon from '@mui/icons-material/FileCopy';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { useQuery, useMutation } from '@tanstack/react-query';
import { startDuplicateDetection, getDuplicates, getDuplicateGroupReports } from '../api/duplicatesApi';
import useTaskStore from '../store/taskStore';
import useTaskPolling from '../hooks/useTaskPolling';
import TaskProgressCard from '../components/common/TaskProgressCard';
import DetailPanel from '../components/common/DetailPanel';
import { ReportMetadataBlock } from '../components/common/DetailPanel';

const DuplicatesPage = () => {
  const { setTaskStarted, clearTask } = useTaskStore();
  const task = useTaskPolling('duplicates');

  const [filters, setFilters] = useState({ search: '', minSimilarity: '', detectionMethod: '' });
  const [activeFilters, setActiveFilters] = useState({ search: '', minSimilarity: '', detectionMethod: '' });
  const [page, setPage] = useState(1);
  const [selectedGroupId, setSelectedGroupId] = useState(null);
  const [panelOpen, setPanelOpen] = useState(false);

  const isTaskRunning = task.status === 'Pending' || task.status === 'Running';

  const triggerMutation = useMutation({
    mutationFn: startDuplicateDetection,
    onSuccess: (result) => {
      const taskId = result?.taskId || result;
      setTaskStarted('duplicates', taskId);
    },
  });

  const { data: dupData, isLoading, isError } = useQuery({
    queryKey: ['duplicates', activeFilters.search, activeFilters.minSimilarity, activeFilters.detectionMethod, page],
    queryFn: () =>
      getDuplicates({
        search: activeFilters.search || undefined,
        minSimilarity: activeFilters.minSimilarity ? Number(activeFilters.minSimilarity) / 100 : undefined,
        detectionMethod: activeFilters.detectionMethod || undefined,
        page,
        pageSize: 20,
      }),
    keepPreviousData: true,
  });

  const { data: groupReports, isLoading: reportsLoading } = useQuery({
    queryKey: ['duplicate-group-reports', selectedGroupId],
    queryFn: () => getDuplicateGroupReports(selectedGroupId),
    enabled: !!selectedGroupId && panelOpen,
  });

  const handleSearch = () => { setActiveFilters({ ...filters }); setPage(1); };
  const handleClear = () => {
    const empty = { search: '', minSimilarity: '', detectionMethod: '' };
    setFilters(empty); setActiveFilters(empty); setPage(1);
  };

  const handleRowClick = (groupId) => {
    setSelectedGroupId(groupId);
    setPanelOpen(true);
  };

  const duplicates = dupData?.data || [];
  const totalPages = dupData?.totalPages || 1;
  const totalCount = dupData?.totalCount || 0;

  return (
    <Box>
      {/* ─── Section A: Trigger ─── */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2}>
            <Box>
              <Typography variant="h6" fontWeight={700}>Find Duplicate Reports</Typography>
              <Typography variant="body2" color="text.secondary">
                Three-tier detection: SQL fingerprint (Jaccard) → SQL embedding (cosine) → metadata embedding.
              </Typography>
            </Box>
            <Button
              variant="contained"
              size="large"
              color="secondary"
              startIcon={isTaskRunning ? <CircularProgress size={18} color="inherit" /> : <FileCopyIcon />}
              onClick={() => triggerMutation.mutate()}
              disabled={isTaskRunning || triggerMutation.isPending}
              sx={{ minWidth: 200 }}
            >
              {isTaskRunning ? 'Detecting...' : 'Run Duplicate Detection'}
            </Button>
          </Box>
          {triggerMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Failed to start detection: {triggerMutation.error?.response?.data?.error || triggerMutation.error?.message}
            </Alert>
          )}
        </CardContent>
      </Card>

      <TaskProgressCard task={task} title="Duplicate Detection" onClear={() => clearTask('duplicates')} />

      <Divider sx={{ my: 3 }} />

      {/* ─── Section B: Duplicates Table ─── */}
      <Typography variant="h6" fontWeight={700} mb={2}>Duplicate Report Groups</Typography>

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
            <Grid item xs={12} sm={3}>
              <TextField
                fullWidth label="Min Similarity"
                type="number"
                inputProps={{ min: 0, max: 100, step: 1 }}
                value={filters.minSimilarity}
                onChange={(e) => setFilters((f) => ({ ...f, minSimilarity: e.target.value }))}
                InputProps={{ endAdornment: <InputAdornment position="end">%</InputAdornment> }}
              />
            </Grid>
            <Grid item xs={12} sm={3}>
              <FormControl fullWidth>
                <InputLabel>Detection Method</InputLabel>
                <Select
                  label="Detection Method"
                  value={filters.detectionMethod}
                  onChange={(e) => setFilters((f) => ({ ...f, detectionMethod: e.target.value }))}
                >
                  <MenuItem value="">All</MenuItem>
                  <MenuItem value="MetadataEmbedding">Metadata Embedding</MenuItem>
                  <MenuItem value="SqlFingerprint">Sql Fingerprint</MenuItem>
                  <MenuItem value="SqlEmbedding">Sql Embedding</MenuItem>
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} sm={2} display="flex" gap={1}>
              <Button variant="contained" startIcon={<SearchIcon />} onClick={handleSearch} fullWidth>
                Search
              </Button>
              <Button variant="outlined" startIcon={<ClearIcon />} onClick={handleClear}>
                Clear
              </Button>
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
            <Alert severity="error" sx={{ m: 2 }}>Failed to load duplicates.</Alert>
          ) : duplicates.length === 0 ? (
            <Box textAlign="center" py={6}>
              <Typography color="text.secondary">No duplicates found. Run detection first.</Typography>
            </Box>
          ) : (
            <>
              <Box px={2} pt={2} pb={1}>
                <Typography variant="caption" color="text.secondary">
                  {totalCount.toLocaleString()} duplicate records
                </Typography>
              </Box>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Report Name</TableCell>
                      <TableCell>Type</TableCell>
                      <TableCell>Duplicate Group ID</TableCell>
                      <TableCell>Similarity Score</TableCell>
                      <TableCell>Match Count</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {duplicates.map((d) => (
                      <TableRow key={d.id || d.reportId} hover onClick={() => handleRowClick(d.duplicateGroupId || d.groupId)}>
                        <TableCell>
                          <Typography variant="body2" fontWeight={500}>
                            {d.reportName || d.name}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Chip label={d.reportType || d.type || '—'} size="small" variant="outlined"
                            color={d.reportType === 'Crystal' ? 'primary' : 'secondary'} />
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={`#${d.duplicateGroupId || d.groupId}`}
                            size="small"
                            variant="outlined"
                          />
                        </TableCell>
                        <TableCell>
                          <SimilarityChip score={d.similarityScore ?? d.similarity} />
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={`${d.matchCount ?? d.duplicateCount ?? 0} matches`}
                            size="small"
                            color="info"
                          />
                        </TableCell>
                      </TableRow>
                    ))}
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

      {/* Detail Panel */}
      <DetailPanel
        open={panelOpen}
        onClose={() => setPanelOpen(false)}
        title={`Duplicate Group #${selectedGroupId} — Reports`}
        loading={reportsLoading}
        error={null}
      >
        {!reportsLoading && (
          <DuplicateGroupReportList reports={groupReports || []} />
        )}
      </DetailPanel>
    </Box>
  );
};

const SimilarityChip = ({ score }) => {
  if (score == null) return <Typography variant="caption" color="text.secondary">—</Typography>;
  const pct = Math.round(score * 100);
  const color = pct >= 95 ? 'error' : pct >= 90 ? 'warning' : 'info';
  return <Chip label={`${pct}%`} size="small" color={color} />;
};

const DuplicateGroupReportList = ({ reports }) => {
  if (!reports || reports.length === 0) {
    return (
      <Box textAlign="center" py={4}>
        <Typography color="text.secondary">No reports in this group.</Typography>
      </Box>
    );
  }
  return (
    <Box>
      <Typography variant="subtitle2" fontWeight={700} mb={2} color="text.secondary">
        {reports.length} REPORT{reports.length !== 1 ? 'S' : ''} IN THIS DUPLICATE GROUP
      </Typography>
      {reports.map((r, idx) => (
        <Accordion key={r.id || idx} disableGutters sx={{ mb: 1, border: '1px solid #F0F2F5', borderRadius: '8px !important', '&:before': { display: 'none' } }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Box display="flex" alignItems="center" gap={1} overflow="hidden">
              <Chip
                label={r.type || r.reportType || 'Report'}
                size="small"
                color={r.type === 'Crystal' ? 'primary' : 'secondary'}
                variant="outlined"
                sx={{ flexShrink: 0 }}
              />
              <Typography variant="body2" fontWeight={600} noWrap>
                {r.name || r.reportName}
              </Typography>
              {r.similarityScore != null && (
                <SimilarityChip score={r.similarityScore} />
              )}
            </Box>
          </AccordionSummary>
          <AccordionDetails sx={{ pt: 0 }}>
            <ReportMetadataBlock report={r} />
          </AccordionDetails>
        </Accordion>
      ))}
    </Box>
  );
};

export default DuplicatesPage;
