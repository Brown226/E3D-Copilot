/**
 * E小智 v2.0 Toast 通知状态管理
 */

import { create } from 'zustand'

export type ToastType = 'success' | 'info' | 'warning' | 'error'

export interface Toast {
  id: string
  type: ToastType
  message: string
  duration?: number
}

interface ToastStore {
  toasts: Toast[]
  addToast: (type: ToastType, message: string, duration?: number) => void
  removeToast: (id: string) => void
  clearAll: () => void
}

let counter = 0

export const useToastStore = create<ToastStore>((set, get) => ({
  toasts: [],

  addToast: (type, message, duration = 4000) => {
    const id = `toast_${++counter}_${Date.now()}`
    set((s) => ({ toasts: [...s.toasts, { id, type, message, duration }] }))
    if (duration > 0) {
      setTimeout(() => { get().removeToast(id) }, duration)
    }
  },

  removeToast: (id) => {
    set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }))
  },

  clearAll: () => set({ toasts: [] }),
}))
