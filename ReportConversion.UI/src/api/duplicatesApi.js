import axiosInstance from './axiosInstance';

/**
 * POST /api/duplicates/start
 * Returns { taskId }
 */
export const startDuplicateDetection = async () => {
  const { data } = await axiosInstance.post('/api/duplicates/start');
  return data.data ?? data;
};

/**
 * GET /api/duplicates
 * Returns paginated list of duplicate report pairs/groups
 */
export const getDuplicates = async ({ search, minSimilarity, detectionMethod, page = 1, pageSize = 20 } = {}) => {
  const params = { page, pageSize };
  if (search) params.search = search;
  if (minSimilarity != null) params.minSimilarity = minSimilarity;
  if (detectionMethod) params.detectionMethod = detectionMethod;
  const { data } = await axiosInstance.get('/api/duplicates/getduplicates', { params });
  return data;
};

/**
 * GET /api/duplicates/getduplicategroupreports/{groupId}
 * Returns all reports in a duplicate group with full detail.
 * @param {number} groupId
 * @param {number|undefined} similarityScore - optional minimum similarity score (0–1)
 */
export const getDuplicateGroupReports = async (groupId, similarityScore) => {
  const params = {};
  if (similarityScore != null) params.similarityScore = similarityScore;
  const { data } = await axiosInstance.get(`/api/duplicates/getduplicategroupreports/${groupId}`, { params });
  return data.data ?? data;
};
