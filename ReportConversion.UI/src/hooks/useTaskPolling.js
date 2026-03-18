import { useEffect, useRef } from 'react';
import { useSnackbar } from 'notistack';
import { getTaskStatus } from '../api/tasksApi';
import useTaskStore from '../store/taskStore';

const POLL_INTERVAL_MS = 3000;
const TERMINAL_STATUSES = ['Completed', 'Failed'];

/**
 * Polls GET /api/tasks/{taskId}/status every 3 seconds.
 * Automatically resumes on mount if taskId is in localStorage and not terminal.
 *
 * @param {string} taskKey  - key into taskStore (extraction | stale | kpi | duplicates | migration)
 * @param {Function} [onComplete] - optional callback when task reaches Completed
 */
const useTaskPolling = (taskKey, onComplete) => {
  const { enqueueSnackbar } = useSnackbar();
  const { updateTaskStatus } = useTaskStore();
  const task = useTaskStore((s) => s[taskKey]);
  const intervalRef = useRef(null);
  const lastStatusRef = useRef(null);

  const stopPolling = () => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  };

  const poll = async (taskId) => {
    try {
      const dto = await getTaskStatus(taskId);
      updateTaskStatus(taskKey, dto);
      lastStatusRef.current = dto.status;

      if (TERMINAL_STATUSES.includes(dto.status)) {
        stopPolling();
        if (dto.status === 'Completed') {
          enqueueSnackbar(`Task completed successfully.`, {
            variant: 'success',
            autoHideDuration: 4000,
          });
          if (onComplete) onComplete(dto);
        } else if (dto.status === 'Failed') {
          enqueueSnackbar(`Task failed: ${dto.errorMessage || 'Unknown error'}`, {
            variant: 'error',
            autoHideDuration: 6000,
          });
        }
      }
    } catch (err) {
      // Network hiccup — keep polling, don't blow up
      console.warn(`[useTaskPolling] ${taskKey} poll error:`, err.message);
    }
  };

  useEffect(() => {
    const { taskId, status } = task;

    // Nothing to poll
    if (!taskId) return;

    // Already in terminal state — don't restart
    if (TERMINAL_STATUSES.includes(status)) return;

    // Start polling immediately, then every 3s
    poll(taskId);
    intervalRef.current = setInterval(() => poll(taskId), POLL_INTERVAL_MS);

    return () => stopPolling();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [task.taskId]);

  return task;
};

export default useTaskPolling;
