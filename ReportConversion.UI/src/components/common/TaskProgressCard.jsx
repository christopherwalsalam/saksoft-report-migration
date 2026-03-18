import React from 'react';
import {
  Box, Card, CardContent, Typography, LinearProgress,
  Chip, Grid, Divider, IconButton, Tooltip,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import SyncIcon from '@mui/icons-material/Sync';
import ClearIcon from '@mui/icons-material/Clear';

const STATUS_CONFIG = {
  Pending:   { color: 'warning',   label: 'Pending',   icon: <HourglassEmptyIcon fontSize="small" /> },
  Running:   { color: 'info',      label: 'Running',   icon: <SyncIcon fontSize="small" className="spin" /> },
  Completed: { color: 'success',   label: 'Completed', icon: <CheckCircleIcon fontSize="small" /> },
  Failed:    { color: 'error',     label: 'Failed',    icon: <ErrorIcon fontSize="small" /> },
};

const formatDateTime = (iso) => {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
};

const getElapsed = (startedAt, completedAt) => {
  if (!startedAt) return '—';
  const end = completedAt ? new Date(completedAt) : new Date();
  const diffMs = end - new Date(startedAt);
  if (diffMs < 0) return '—';
  const s = Math.floor(diffMs / 1000) % 60;
  const m = Math.floor(diffMs / 60000) % 60;
  const h = Math.floor(diffMs / 3600000);
  if (h > 0) return `${h}h ${m}m ${s}s`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
};

/**
 * Displays a card showing the progress of a long-running background task.
 *
 * Props:
 *  task        — task object from useTaskStore / useTaskPolling
 *  title       — e.g. "SAP BO Metadata Extraction"
 *  onClear     — called when user clicks the X to dismiss/reset
 */
const TaskProgressCard = ({ task, title, onClear }) => {
  if (!task || !task.taskId) return null;

  const cfg = STATUS_CONFIG[task.status] || STATUS_CONFIG['Pending'];
  const isTerminal = task.status === 'Completed' || task.status === 'Failed';
  const progress = Math.min(Math.max(task.progressPercent ?? 0, 0), 100);

  return (
    <Card
      sx={{
        mb: 3,
        border: '1px solid',
        borderColor:
          task.status === 'Completed' ? 'success.light'
          : task.status === 'Failed'  ? 'error.light'
          : task.status === 'Running' ? 'primary.light'
          : 'warning.light',
        background:
          task.status === 'Completed' ? 'linear-gradient(135deg,#F1F8E9 0%,#FFFFFF 100%)'
          : task.status === 'Failed'  ? 'linear-gradient(135deg,#FFEBEE 0%,#FFFFFF 100%)'
          : 'linear-gradient(135deg,#E3F2FD 0%,#FFFFFF 100%)',
      }}
    >
      <CardContent sx={{ pb: '16px !important' }}>
        {/* Header row */}
        <Box display="flex" alignItems="center" justifyContent="space-between" mb={1.5}>
          <Box display="flex" alignItems="center" gap={1}>
            <Typography variant="subtitle1" fontWeight={600}>
              {title}
            </Typography>
            <Chip
              icon={cfg.icon}
              label={cfg.label}
              color={cfg.color}
              size="small"
              sx={{ fontWeight: 700 }}
            />
          </Box>
          {isTerminal && onClear && (
            <Tooltip title="Dismiss">
              <IconButton size="small" onClick={onClear}>
                <ClearIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Box>

        {/* Progress bar */}
        <Box mb={0.5}>
          <Box display="flex" justifyContent="space-between" mb={0.5}>
            <Typography variant="caption" color="text.secondary">
              {task.currentStep || (task.status === 'Pending' ? 'Waiting to start...' : '')}
            </Typography>
            <Typography variant="caption" fontWeight={700} color="primary.main">
              {progress}%
            </Typography>
          </Box>
          <LinearProgress
            variant={task.status === 'Pending' ? 'indeterminate' : 'determinate'}
            value={progress}
            color={task.status === 'Failed' ? 'error' : task.status === 'Completed' ? 'success' : 'primary'}
          />
        </Box>

        {/* Error message */}
        {task.status === 'Failed' && task.errorMessage && (
          <Typography variant="caption" color="error.main" sx={{ display: 'block', mt: 1 }}>
            {task.errorMessage}
          </Typography>
        )}

        <Divider sx={{ my: 1.5 }} />

        {/* Metadata row */}
        <Grid container spacing={2}>
          <Grid item xs={12} sm={4}>
            <Typography variant="caption" color="text.secondary" display="block">
              Started
            </Typography>
            <Typography variant="caption" fontWeight={500}>
              {formatDateTime(task.startedAt)}
            </Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="caption" color="text.secondary" display="block">
              {isTerminal ? 'Completed' : 'Elapsed'}
            </Typography>
            <Typography variant="caption" fontWeight={500}>
              {isTerminal ? formatDateTime(task.completedAt) : getElapsed(task.startedAt, null)}
            </Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="caption" color="text.secondary" display="block">
              Task ID
            </Typography>
            <Typography
              variant="caption"
              fontWeight={500}
              sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}
            >
              {task.taskId}
            </Typography>
          </Grid>
        </Grid>
      </CardContent>

      <style>{`
        .spin { animation: spin 1.5s linear infinite; }
        @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
      `}</style>
    </Card>
  );
};

export default TaskProgressCard;
