import React, { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, TextField,
  Grid, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Pagination, Chip, CircularProgress, Alert, Stack, Divider,
  Accordion, AccordionSummary, AccordionDetails, Badge, Tooltip,
} from '@mui/material';
import CategoryIcon from '@mui/icons-material/Category';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import AssignmentIcon from '@mui/icons-material/Assignment';
import { useQuery, useMutation } from '@tanstack/react-query';
import { startKpiGrouping, getKpiGroups, getKpiGroupReports } from '../api/kpiGroupsApi';
import useTaskStore from '../store/taskStore';
import useTaskPolling from '../hooks/useTaskPolling';
import TaskProgressCard from '../components/common/TaskProgressCard';
import DetailPanel from '../components/common/DetailPanel';
import { ReportMetadataBlock } from '../components/common/DetailPanel';

const KpiGroupsPage = () => {
  const { setTaskStarted, clearTask } = useTaskStore();
  const task = useTaskPolling('kpi');

  const [filters, setFilters] = useState({ search: '', kpiLabel: '' });
  const [activeFilters, setActiveFilters] = useState({ search: '', kpiLabel: '' });
  const [page, setPage] = useState(1);

  const [selectedGroupId, setSelectedGroupId] = useState(null);
  const [panelOpen, setPanelOpen] = useState(false);

  const isTaskRunning = task.status === 'Pending' || task.status === 'Running';

  const triggerMutation = useMutation({
    mutationFn: startKpiGrouping,
    onSuccess: (result) => {
      const taskId = result?.taskId || result;
      setTaskStarted('kpi', taskId);
    },
  });

  const { data: groupsData, isLoading, isError } = useQuery({
    queryKey: ['kpi-groups', activeFilters, page],
    queryFn: () =>
      getKpiGroups({
        search: activeFilters.search || undefined,
        kpiLabel: activeFilters.kpiLabel || undefined,
        page,
        pageSize: 20,
      }),
    keepPreviousData: true,
  });

  const { data: groupReports, isLoading: reportsLoading } = useQuery({
    queryKey: ['kpi-group-reports', selectedGroupId],
    queryFn: () => getKpiGroupReports(selectedGroupId),
    enabled: !!selectedGroupId && panelOpen,
  });

  const handleSearch = () => { setActiveFilters({ ...filters }); setPage(1); };
  const handleClear = () => {
    const empty = { search: '', kpiLabel: '' };
    setFilters(empty); setActiveFilters(empty); setPage(1);
  };

  const handleGroupClick = (groupId) => {
    setSelectedGroupId(groupId);
    setPanelOpen(true);
  };

  const groups = groupsData?.data || [];
  const totalPages = groupsData?.totalPages || 1;
  const totalCount = groupsData?.totalCount || 0;

  const selectedGroup = groups.find((g) => g.id === selectedGroupId);

  return (
    <Box>
      {/* ─── Section A: Trigger ─── */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2}>
            <Box>
              <Typography variant="h6" fontWeight={700}>Group Reports by KPI</Typography>
              <Typography variant="body2" color="text.secondary">
                Uses Azure OpenAI GPT-4o to analyse SQL signals and classify reports into KPI domains.
              </Typography>
            </Box>
            <Button
              variant="contained"
              size="large"
              startIcon={isTaskRunning ? <CircularProgress size={18} color="inherit" /> : <CategoryIcon />}
              onClick={() => triggerMutation.mutate()}
              disabled={isTaskRunning || triggerMutation.isPending}
              sx={{ minWidth: 180 }}
            >
              {isTaskRunning ? 'Grouping...' : 'Run KPI Grouping'}
            </Button>
          </Box>
          {triggerMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Failed to start KPI grouping: {triggerMutation.error?.response?.data?.error || triggerMutation.error?.message}
            </Alert>
          )}
        </CardContent>
      </Card>

      <TaskProgressCard task={task} title="KPI Group Discovery" onClear={() => clearTask('kpi')} />

      <Divider sx={{ my: 3 }} />

      {/* ─── Section B: KPI Groups Table ─── */}
      <Typography variant="h6" fontWeight={700} mb={2}>KPI Groups</Typography>

      {/* Filters */}
      <Card sx={{ mb: 2 }}>
        <CardContent>
          <Grid container spacing={2} alignItems="flex-end">
            <Grid item xs={12} sm={5}>
              <TextField
                fullWidth label="Group Name"
                value={filters.search}
                onChange={(e) => setFilters((f) => ({ ...f, search: e.target.value }))}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </Grid>
            <Grid item xs={12} sm={5}>
              <TextField
                fullWidth label="KPI Label"
                value={filters.kpiLabel}
                onChange={(e) => setFilters((f) => ({ ...f, kpiLabel: e.target.value }))}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
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
            <Alert severity="error" sx={{ m: 2 }}>Failed to load KPI groups.</Alert>
          ) : groups.length === 0 ? (
            <Box textAlign="center" py={6}>
              <Typography color="text.secondary">No KPI groups found. Run grouping analysis first.</Typography>
            </Box>
          ) : (
            <>
              <Box px={2} pt={2} pb={1}>
                <Typography variant="caption" color="text.secondary">
                  {totalCount.toLocaleString()} groups
                </Typography>
              </Box>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Group Name</TableCell>
                      <TableCell>KPI Label</TableCell>
                      <TableCell>Report Count</TableCell>
                      <TableCell>Created Date</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {groups.map((g) => (
                      <TableRow key={g.id} hover onClick={() => handleGroupClick(g.id)}>
                        <TableCell>
                          <Box display="flex" alignItems="center" gap={1}>
                            <AssignmentIcon fontSize="small" color="primary" />
                            <Typography variant="body2" fontWeight={600}>{g.name || g.groupName}</Typography>
                          </Box>
                        </TableCell>
                        <TableCell sx={{ maxWidth: 220 }}>
                          {(() => {
                            const full = g.kpiLabel || g.label || '—';
                            const truncated = full.length > 35 ? `${full.slice(0, 35)}…` : full;
                            return (
                              <Tooltip title={full.length > 35 ? full : ''} placement="top" arrow>
                                <Chip
                                  label={truncated}
                                  size="small"
                                  color="secondary"
                                  variant="outlined"
                                  sx={{ maxWidth: '100%', '& .MuiChip-label': { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } }}
                                />
                              </Tooltip>
                            );
                          })()}
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={`${g.reportCount ?? g.totalReports ?? 0} reports`}
                            size="small"
                            color="primary"
                          />
                        </TableCell>
                        <TableCell>
                          <Typography variant="caption">
                            {g.createdAt ? new Date(g.createdAt).toLocaleDateString() : '—'}
                          </Typography>
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

      {/* Detail Panel — shows all reports in the group */}
      <DetailPanel
        open={panelOpen}
        onClose={() => setPanelOpen(false)}
        title={selectedGroup ? `${selectedGroup.name || selectedGroup.groupName} — Reports` : 'KPI Group Reports'}
        loading={reportsLoading}
        error={null}
      >
        {!reportsLoading && (
          <KpiGroupReportList
            reports={groupReports || []}
            kpiLabel={selectedGroup?.kpiLabel || selectedGroup?.label}
          />
        )}
      </DetailPanel>
    </Box>
  );
};

const KpiGroupReportList = ({ reports, kpiLabel }) => {
  if (!reports || reports.length === 0) {
    return (
      <Box textAlign="center" py={4}>
        <Typography color="text.secondary">No reports in this group.</Typography>
      </Box>
    );
  }

  return (
    <Box>
      {kpiLabel && (
        <Box sx={{ mb: 2, p: 1.5, bgcolor: '#F5F3FF', borderRadius: 2, border: '1px solid #DDD6FE' }}>
          <Typography variant="caption" fontWeight={700} color="secondary.main" display="block" mb={0.5}>
            KPI LABEL
          </Typography>
          <Typography variant="body2" color="text.primary" sx={{ wordBreak: 'break-word' }}>
            {kpiLabel}
          </Typography>
        </Box>
      )}
      <Typography variant="subtitle2" fontWeight={700} mb={2} color="text.secondary">
        {reports.length} REPORT{reports.length !== 1 ? 'S' : ''} IN THIS GROUP
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

export default KpiGroupsPage;
