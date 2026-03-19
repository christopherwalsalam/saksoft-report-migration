import axiosInstance from './axiosInstance';

/**
 * GET /api/tasks/{taskId}/status
 * Returns TaskStatusDto
 */
export const getTaskStatus = async (taskId) => {
  const { data } = await axiosInstance.get(`/api/tasks/gettaskstatus/${taskId}`);
  return data.data ?? data;
};
