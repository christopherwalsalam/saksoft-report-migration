import axiosInstance from './axiosInstance';

/**
 * POST /api/extraction/start
 * Returns { taskId }
 */
export const startExtraction = async () => {
  const { data } = await axiosInstance.post('/api/extraction/start');
  return data.data ?? data;
};

/**
 * GET /api/reports
 * Returns paginated list of reports
 */
export const getReports = async ({ search, type, folder, fromDate, toDate, page = 1, pageSize = 20 } = {}) => {
  const params = { page, pageSize };
  if (search) params.search = search;
  if (type && type !== 'All') params.type = type;
  if (folder) params.folder = folder;
  if (fromDate) params.fromDate = fromDate;
  if (toDate) params.toDate = toDate;
  const { data } = await axiosInstance.get('/api/reports', { params });
  return data;
};

/**
 * GET /api/reports/{id}
 * Returns full report detail with metadata and SQL
 */
export const getReportById = async (id) => {
  const { data } = await axiosInstance.get(`/api/reports/${id}`);
  return data.data ?? data;
};
