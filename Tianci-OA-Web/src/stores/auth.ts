import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authApi } from '@/api/modules'
import { TOKEN_KEY } from '@/api/http'
import type { UserDto } from '@/types/contracts'

const USER_KEY = 'tianci_oa_user'
const PERMISSION_KEY = 'tianci_oa_permissions'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem(TOKEN_KEY) || '')
  const user = ref<UserDto | null>(JSON.parse(localStorage.getItem(USER_KEY) || 'null') as UserDto | null)
  const permissions = ref<string[]>(JSON.parse(localStorage.getItem(PERMISSION_KEY) || '[]') as string[])
  const authenticated = computed(() => Boolean(token.value))

  async function login(username: string, password: string) {
    const result = await authApi.login({ username, password })
    token.value = result.accessToken
    user.value = result.user
    permissions.value = result.permissions
    localStorage.setItem(TOKEN_KEY, result.accessToken)
    localStorage.setItem(USER_KEY, JSON.stringify(result.user))
    localStorage.setItem(PERMISSION_KEY, JSON.stringify(result.permissions))
  }

  function has(permission?: string) {
    return !permission || permissions.value.includes('*') || permissions.value.includes(permission)
  }

  function logout() {
    token.value = ''
    user.value = null
    permissions.value = []
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    localStorage.removeItem(PERMISSION_KEY)
  }

  return { token, user, permissions, authenticated, login, has, logout }
})
