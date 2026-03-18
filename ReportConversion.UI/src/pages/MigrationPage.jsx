import React, { useState } from 'react';
import {
  Box, Button, Card, CardContent, Typography, Grid,
  Chip, CircularProgress, Alert, Divider, LinearProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
} from '@mui/material';
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import DownloadIcon from '@mui/icons-material/Download';
import AssessmentIcon from '@mui/icons-material/Assessment';
import { useQuery, useMutation } from '@tanstack/react-query';
import { startMigration, getMigrationSummary } from '../api/migrationApi';
import useTaskStore from '../store/taskStore';
import useTaskPolling from '../hooks/useTaskPolling';
import TaskProgressCard from '../components/common/TaskProgressCard';

const MigrationPage = () => {
  const { setTaskStarted, clearTask } = useTaskStore();
  const [summaryEnabled, setSummaryEnabled] = useState(false);

  const task = useTaskPolling('migration', () => {
    // On completion, enable the summary query
    setSummaryEnabled(true);
  });

  const isTaskRunning = task.status === 'Pending' || task.status === 'Running';
  const isCompleted = task.status === 'Completed';

  const triggerMutation = useMutation({
    mutationFn: () => startMigration(null),
    onSuccess: (result) => {
      const taskId = result?.taskId || result;
      setTaskStarted('migration', taskId);
      setSummaryEnabled(false);
    },
  });

  // Fetch summary after completion
  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ['migration-summary'],
    queryFn: getMigrationSummary,
    enabled: summaryEnabled || isCompleted,
    staleTime: 30000,
  });

  const successRate =
    summary && summary.totalAttempted > 0
      ? Math.round((summary.successCount / summary.totalAttempted) * 100)
      : 0;

  return (
    <Box>
      {/* ─── Section: Trigger ─── */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Box display="flex" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={2}>
            <Box>
              <Typography variant="h6" fontWeight={700}>Power BI Migration</Typography>
              <Typography variant="body2" color="text.secondary">
                Generates .pbix template files for all Active reports and uploads them to Azure Blob Storage.
                Long-running operation — typically 4–8 hours for 10,000 reports.
              </Typography>
            </Box>
            <Button
              variant="contained"
              size="large"
              startIcon={isTaskRunning ? <CircularProgress size={18} color="inherit" /> : <RocketLaunchIcon />}
              onClick={() => triggerMutation.mutate()}
              disabled={isTaskRunning || triggerMutation.isPending}
              sx={{ minWidth: 220 }}
            >
              {isTaskRunning ? 'Migrating...' : 'Start Power BI Migration'}
            </Button>
          </Box>
          {triggerMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Failed to start migration: {triggerMutation.error?.response?.data?.error || triggerMutation.error?.message}
            </Alert>
          )}
        </CardContent>
      </Card>

      {/* Task Progress */}
      <TaskProgressCard
        task={task}
        title="Power BI Migration"
        onClear={() => { clearTask('migration'); setSummaryEnabled(false); }}
      />

      {/* ─── Migration Summary Card (shown on completion) ─── */}
      {(isCompleted || summary) && (
        <>
          <Divider sx={{ my: 3 }} />
          <Typography variant="h6" fontWeight={700} mb={2}>
            Migration Summary
          </Typography>

          {summaryLoading ? (
            <Box display="flex" justifyContent="center" py={4}><CircularProgress /></Box>
          ) : summary ? (
            <Grid container spacing={2}>
              {/* Stats cards */}
              <Grid item xs={12} sm={4}>
                <StatCard
                  icon={<AssessmentIcon sx={{ color: '#1565C0', fontSize: 32 }} />}
                  label="Total Attempted"
                  value={summary.totalAttempted ?? 0}
                  color="#E3F2FD"
                />
              </Grid>
              <Grid item xs={12} sm={4}>
                <StatCard
                  icon={<CheckCircleIcon sx={{ color: '#2E7D32', fontSize: 32 }} />}
                  label="Successfully Migrated"
                  value={summary.successCount ?? 0}
                  color="#F1F8E9"
                />
              </Grid>
              <Grid item xs={12} sm={4}>
                <StatCard
                  icon={<ErrorIcon sx={{ color: '#C62828', fontSize: 32 }} />}
                  label="Failed"
                  value={summary.failureCount ?? 0}
                  color="#FFEBEE"
                />
              </Grid>

              {/* Success rate bar */}
              <Grid item xs={12}>
                <Card>
                  <CardContent>
                    <Box display="flex" justifyContent="space-between" mb={1}>
                      <Typography variant="subtitle2" fontWeight={600}>
                        Migration Success Rate
                      </Typography>
                      <Typography variant="subtitle2" fontWeight={700} color="success.main">
                        {successRate}%
                      </Typography>
                    </Box>
                    <LinearProgress
                      variant="determinate"
                      value={successRate}
                      color={successRate >= 90 ? 'success' : successRate >= 70 ? 'warning' : 'error'}
                      sx={{ height: 10, borderRadius: 5 }}
                    />
                    <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
                      {summary.successCount ?? 0} of {summary.totalAttempted ?? 0} reports migrated successfully
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>

              {/* Per-KPI breakdown if present */}
              {summary.kpiBreakdown && summary.kpiBreakdown.length > 0 && (
                <Grid item xs={12}>
                  <Card>
                    <CardContent>
                      <Typography variant="subtitle2" fontWeight={700} mb={2}>
                        Breakdown by KPI Group
                      </Typography>
                      <TableContainer>
                        <Table size="small">
                          <TableHead>
                            <TableRow>
                              <TableCell>KPI Group</TableCell>
                              <TableCell>Attempted</TableCell>
                              <TableCell>Succeeded</TableCell>
                              <TableCell>Failed</TableCell>
                              <TableCell>Rate</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {summary.kpiBreakdown.map((row, i) => (
                              <TableRow key={i}>
                                <TableCell>{row.kpiGroup}</TableCell>
                                <TableCell>{row.attempted}</TableCell>
                                <TableCell>
                                  <Chip label={row.succeeded} size="small" color="success" />
                                </TableCell>
                                <TableCell>
                                  <Chip label={row.failed} size="small" color={row.failed > 0 ? 'error' : 'default'} />
                                </TableCell>
                                <TableCell>
                                  <Typography variant="body2" fontWeight={600}
                                    color={row.attempted > 0 && row.succeeded / row.attempted >= 0.9 ? 'success.main' : 'warning.main'}>
                                    {row.attempted > 0 ? Math.round((row.succeeded / row.attempted) * 100) : 0}%
                                  </Typography>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </CardContent>
                  </Card>
                </Grid>
              )}

              {/* Download log link */}
              {summary.logDownloadUrl && (
                <Grid item xs={12}>
                  <Button
                    variant="outlined"
                    startIcon={<DownloadIcon />}
                    href={summary.logDownloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Download Migration Log
                  </Button>
                </Grid>
              )}
            </Grid>
          ) : (
            <Alert severity="info">Summary not yet available. Please wait for the migration to complete.</Alert>
          )}
        </>
      )}
    </Box>
  );
};

const StatCard = ({ icon, label, value, color }) => (
  <Card sx={{ bgcolor: color, border: 'none', boxShadow: 'none' }}>
    <CardContent>
      <Box display="flex" alignItems="center" gap={2}>
        {icon}
        <Box>
          <Typography variant="caption" color="text.secondary" display="block">
            {label}
          </Typography>
          <Typography variant="h4" fontWeight={700}>
            {value.toLocaleString()}
          </Typography>
        </Box>
      </Box>
    </CardContent>
  </Card>
);

export default MigrationPage;
