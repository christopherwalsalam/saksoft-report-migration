import axiosInstance from './axiosInstance';

/**
 * POST /api/kpi-groups/start
 * Returns { taskId }
 */
export const startKpiGrouping = async () => {
  const { data } = await axiosInstance.post('/api/kpi-groups/start');
  return data.data ?? data;
};

/**
 * GET /api/kpi-groups
 * Returns paginated list of KPI groups
 */
export const getKpiGroups = async ({ search, kpiLabel, page = 1, pageSize = 20 } = {}) => {
  const params = { page, pageSize };
  if (search) params.search = search;
  if (kpiLabel) params.kpiLabel = kpiLabel;
  const { data } = await axiosInstance.get('/api/kpi-groups/getkpigroups', { params });
  return data;
};

/**
 * GET /api/kpi-groups/getkpigroupreports/{groupId}
 * Returns all reports in a KPI group with full detail
 */
export const getKpiGroupReports = async (groupId) => {
  const { data } = await axiosInstance.get(`/api/kpi-groups/getkpigroupreports/${groupId}`);
  return data.data ?? data;
};
