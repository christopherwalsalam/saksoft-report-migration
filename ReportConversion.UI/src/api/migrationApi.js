import axiosInstance from './axiosInstance';

/**
 * POST /api/migration/start
 * Returns { taskId }
 */
export const startMigration = async (reportIds = null) => {
  const body = reportIds && reportIds.length > 0 ? { reportIds } : {};
  const { data } = await axiosInstance.post('/api/migration/start', body);
  return data.data ?? data;
};

/**
 * GET /api/migration/summary
 * Returns migration summary after task completion
 */
export const getMigrationSummary = async () => {
  const { data } = await axiosInstance.get('/api/migration/summary');
  return data.data ?? data;
};
