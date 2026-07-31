import type { Directive } from 'vue'
import { useAuthStore } from '@/stores/auth'

export const permissionDirective: Directive<HTMLElement, string | string[]> = {
  mounted(el, binding) {
    const required = Array.isArray(binding.value) ? binding.value : [binding.value]
    const auth = useAuthStore()
    if (required.length && !required.some((permission) => auth.has(permission))) el.remove()
  },
}
