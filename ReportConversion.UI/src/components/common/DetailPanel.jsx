import React from 'react';
import {
  Drawer, Box, Typography, IconButton, Divider,
  CircularProgress, Alert, Chip, Grid, Stack,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import SqlCodeBlock from './SqlCodeBlock';

const DRAWER_WIDTH = 600;

/**
 * Right-side slide-in panel for showing full report / group detail.
 *
 * Props:
 *  open       — boolean
 *  onClose    — () => void
 *  title      — panel heading
 *  loading    — boolean
 *  error      — string | null
 *  report     — full report object (for single-report view)
 *  children   — additional content (e.g. group report list)
 */
const DetailPanel = ({ open, onClose, title, loading, error, report, children }) => {
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{
        sx: {
          width: { xs: '100vw', sm: DRAWER_WIDTH },
          bgcolor: '#FAFBFF',
          borderLeft: '1px solid #E0E0E0',
        },
      }}
    >
      {/* Header */}
      <Box
        display="flex"
        alignItems="center"
        justifyContent="space-between"
        px={3}
        py={2}
        sx={{ bgcolor: '#FFFFFF', borderBottom: '1px solid #E0E0E0', position: 'sticky', top: 0, zIndex: 1 }}
      >
        <Typography variant="h6" fontWeight={700} noWrap sx={{ maxWidth: '85%' }}>
          {title || 'Detail'}
        </Typography>
        <IconButton onClick={onClose} size="small">
          <CloseIcon />
        </IconButton>
      </Box>

      {/* Body */}
      <Box sx={{ overflowY: 'auto', flex: 1, p: 3 }}>
        {loading && (
          <Box display="flex" justifyContent="center" py={6}>
            <CircularProgress />
          </Box>
        )}
        {error && !loading && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {!loading && !error && report && <ReportMetadataBlock report={report} />}

        {!loading && !error && children}
      </Box>
    </Drawer>
  );
};

/**
 * Renders full metadata fields + SQL for a single report.
 */
export const ReportMetadataBlock = ({ report }) => {
  if (!report) return null;

  const sqlList = [
    ...(report.sqlQueries || []),
    ...(report.sqls || []),
    report.sqlQuery,
    report.sql,
  ]
    .filter(Boolean)
    .map((s) => (typeof s === 'string' ? s : s.queryText || s.sql || ''));

  const metaFields = [
    { label: 'Report ID',       value: report.id },
    { label: 'Name',            value: report.name || report.reportName },
    { label: 'Type',            value: report.type || report.reportType },
    { label: 'Folder',          value: report.folderPath || report.folder },
    { label: 'Created Date',    value: formatDate(report.createdDate || report.createdAt) },
    { label: 'Last Run Date',   value: formatDate(report.lastRunDate) },
    { label: 'Last Accessed',   value: formatDate(report.lastAccessedDate || report.lastRunDate) },
    { label: 'Status',          value: report.usageStatus || report.status },
    { label: 'KPI Group',       value: report.kpiGroupName },
    { label: 'Stale Reason',    value: report.staleReason },
    { label: 'Is Scheduled',    value: report.isScheduled != null ? (report.isScheduled ? 'Yes' : 'No') : undefined },
    { label: 'Subscriptions',   value: report.hasActiveSubscriptions != null ? (report.hasActiveSubscriptions ? 'Yes' : 'No') : undefined },
    { label: 'Description',     value: report.description },
    { label: 'Data Source',     value: report.dataSourceName || report.dataSource },
    { label: 'Owner',           value: report.owner },
  ].filter((f) => f.value != null && f.value !== '' && f.value !== undefined);

  return (
    <Box>
      <Typography variant="subtitle2" fontWeight={700} mb={1.5} color="text.secondary">
        METADATA
      </Typography>
      <Grid container spacing={1.5} mb={3}>
        {metaFields.map((f) => (
          <Grid item xs={12} sm={6} key={f.label}>
            <Box
              sx={{
                bgcolor: '#FFFFFF',
                border: '1px solid #F0F2F5',
                borderRadius: 2,
                p: 1.5,
              }}
            >
              <Typography variant="caption" color="text.secondary" display="block">
                {f.label}
              </Typography>
              {f.label === 'Status' ? (
                <StatusChip value={f.value} />
              ) : (
                <Typography variant="body2" fontWeight={500}>
                  {f.value}
                </Typography>
              )}
            </Box>
          </Grid>
        ))}
      </Grid>

      <Divider sx={{ mb: 2 }} />

      <Typography variant="subtitle2" fontWeight={700} mb={1.5} color="text.secondary">
        SQL QUERIES
      </Typography>
      <SqlCodeBlock sqls={sqlList} />
    </Box>
  );
};

const StatusChip = ({ value }) => {
  const colorMap = {
    active: 'success', Active: 'success',
    stale: 'warning', Stale: 'warning',
    neverused: 'default', NeverUsed: 'default',
    completed: 'success', Completed: 'success',
    failed: 'error', Failed: 'error',
    running: 'info', Running: 'info',
  };
  const color = colorMap[value] || 'default';
  return <Chip label={value} color={color} size="small" sx={{ mt: 0.5, fontWeight: 600 }} />;
};

const formatDate = (val) => {
  if (!val) return undefined;
  try {
    return new Date(val).toLocaleDateString();
  } catch {
    return val;
  }
};

export default DetailPanel;
