import { ref } from 'vue'
import { defineStore } from 'pinia'

export interface TabItem { path: string; title: string; closable: boolean }

export const useAppStore = defineStore('app', () => {
  const collapsed = ref(false)
  const mobileMenu = ref(false)
  const tabs = ref<TabItem[]>([{ path: '/dashboard', title: '首页', closable: false }])
  function toggleSidebar() { collapsed.value = !collapsed.value }
  function addTab(tab: TabItem) {
    if (!tabs.value.some((item) => item.path === tab.path)) tabs.value.push(tab)
  }
  function closeTab(path: string) { tabs.value = tabs.value.filter((item) => item.path !== path || !item.closable) }
  return { collapsed, mobileMenu, tabs, toggleSidebar, addTab, closeTab }
})
