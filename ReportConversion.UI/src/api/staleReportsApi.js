import axiosInstance from './axiosInstance';

/**
 * POST /api/stale-reports/start
 * Returns { taskId }
 */
export const startStaleReportAnalysis = async () => {
  const { data } = await axiosInstance.post('/api/stale-reports/start');
  return data.data ?? data;
};

/**
 * GET /api/stale-reports
 * Returns paginated list of stale reports
 */
export const getStaleReports = async ({ search, staleReason, fromDate, toDate, page = 1, pageSize = 20 } = {}) => {
  const params = { page, pageSize };
  if (search) params.search = search;
  if (staleReason) params.staleReason = staleReason;
  if (fromDate) params.fromDate = fromDate;
  if (toDate) params.toDate = toDate;
  const { data } = await axiosInstance.get('/api/stale-reports', { params });
  return data;
};

/**
 * GET /api/stale-reports/{id}
 * Returns full stale report detail
 */
export const getStaleReportById = async (id) => {
  const { data } = await axiosInstance.get(`/api/stale-reports/${id}`);
  return data.data ?? data;
};
