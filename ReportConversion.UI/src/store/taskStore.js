import { create } from 'zustand';

// localStorage key prefix for persisting taskIds
const TASK_KEYS = {
  extraction: 'taskId_extraction',
  stale: 'taskId_stale',
  kpi: 'taskId_kpi',
  duplicates: 'taskId_duplicates',
  migration: 'taskId_migration',
};

const loadPersistedTaskId = (key) => localStorage.getItem(TASK_KEYS[key]) || null;

const initialTaskState = (key) => ({
  taskId: loadPersistedTaskId(key),
  status: null,       // Pending | Running | Completed | Failed | null
  progressPercent: 0,
  currentStep: '',
  startedAt: null,
  completedAt: null,
  errorMessage: null,
});

const useTaskStore = create((set, get) => ({
  extraction: initialTaskState('extraction'),
  stale: initialTaskState('stale'),
  kpi: initialTaskState('kpi'),
  duplicates: initialTaskState('duplicates'),
  migration: initialTaskState('migration'),

  /**
   * Called when a task is triggered: persist taskId and reset progress fields
   */
  setTaskStarted: (taskKey, taskId) => {
    localStorage.setItem(TASK_KEYS[taskKey], taskId);
    set((state) => ({
      [taskKey]: {
        ...state[taskKey],
        taskId,
        status: 'Pending',
        progressPercent: 0,
        currentStep: 'Queued...',
        startedAt: new Date().toISOString(),
        completedAt: null,
        errorMessage: null,
      },
    }));
  },

  /**
   * Called on each polling tick: merge in the latest TaskStatusDto
   */
  updateTaskStatus: (taskKey, dto) => {
    set((state) => ({
      [taskKey]: {
        ...state[taskKey],
        status: dto.status,
        progressPercent: dto.progressPercent ?? 0,
        currentStep: dto.currentStep ?? '',
        startedAt: dto.startedAt ?? state[taskKey].startedAt,
        completedAt: dto.completedAt ?? null,
        errorMessage: dto.errorMessage ?? null,
      },
    }));
  },

  /**
   * Clear taskId for a given key (allows re-trigger)
   */
  clearTask: (taskKey) => {
    localStorage.removeItem(TASK_KEYS[taskKey]);
    set({
      [taskKey]: {
        taskId: null,
        status: null,
        progressPercent: 0,
        currentStep: '',
        startedAt: null,
        completedAt: null,
        errorMessage: null,
      },
    });
  },

  getTask: (taskKey) => get()[taskKey],
}));

export default useTaskStore;
export { TASK_KEYS };
