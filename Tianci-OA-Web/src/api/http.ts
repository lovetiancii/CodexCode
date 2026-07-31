import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import router from '@/router'
import type { ApiResponse } from '@/types/contracts'

const TOKEN_KEY = 'tianci_oa_access_token'
const baseURL = import.meta.env.VITE_API_BASE_URL || '/api/v1'

export const http = axios.create({ baseURL, timeout: 15_000 })

http.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) config.headers.Authorization = `Bearer ${token}`
  config.headers['X-Trace-Id'] = crypto.randomUUID().replaceAll('-', '')
  return config
})

http.interceptors.response.use(
  (response) => {
    const envelope = response.data as ApiResponse<unknown>
    if (envelope && typeof envelope === 'object' && 'success' in envelope) {
      if (!envelope.success) return Promise.reject(new Error(envelope.message))
      response.data = envelope.data
    }
    return response
  },
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const message = error.response?.data?.message || (error.code === 'ECONNABORTED' ? '请求超时，请稍后重试' : '服务暂时不可用')
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY)
      if (router.currentRoute.value.name !== 'login') await router.replace({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
    }
    ElMessage.error(message)
    return Promise.reject(new Error(message))
  },
)

export const request = {
  get: <T>(url: string, config?: AxiosRequestConfig) => http.get<T>(url, config).then((r) => r.data),
  post: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) => http.post<T>(url, data, config).then((r) => r.data),
  put: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) => http.put<T>(url, data, config).then((r) => r.data),
  delete: <T>(url: string, config?: AxiosRequestConfig) => http.delete<T>(url, config).then((r) => r.data),
}

export { TOKEN_KEY }
