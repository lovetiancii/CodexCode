<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useAppStore } from '@/stores/app'
import {
  ArrowDown, Briefcase, Calendar, Connection, DataAnalysis, Document, Fold, HomeFilled,
  Menu as MenuIcon, OfficeBuilding, Operation, Position, Postcard, Setting, Tickets, User, UserFilled,
} from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const app = useAppStore()
const active = computed(() => route.path)
const title = computed(() => String(route.meta.title || '首页'))

function closeTab(path: string) {
  app.closeTab(path)
  if (route.fullPath === path) router.push(app.tabs.at(-1)?.path || '/dashboard')
}
function signOut() {
  auth.logout()
  router.replace('/login')
}
</script>

<template>
  <div class="app-shell" :class="{ collapsed: app.collapsed }">
    <aside class="sidebar" :class="{ mobileOpen: app.mobileMenu }">
      <div class="brand"><span class="brand-mark">T</span><strong v-show="!app.collapsed">Tianci OA</strong></div>
      <el-scrollbar>
        <el-menu :default-active="active" router unique-opened background-color="transparent" text-color="#9fb1c7" active-text-color="#fff" :collapse="app.collapsed">
          <el-menu-item index="/dashboard"><el-icon><HomeFilled /></el-icon><template #title>首页</template></el-menu-item>
          <el-sub-menu index="employee">
            <template #title><el-icon><UserFilled /></el-icon><span>人员管理</span></template>
            <el-menu-item v-if="auth.has('employee:view')" index="/employees"><el-icon><Postcard /></el-icon>员工档案</el-menu-item>
            <el-menu-item v-if="auth.has('organization:view')" index="/departments"><el-icon><OfficeBuilding /></el-icon>组织架构</el-menu-item>
            <el-menu-item v-if="auth.has('organization:view')" index="/positions"><el-icon><Position /></el-icon>岗位管理</el-menu-item>
          </el-sub-menu>
          <el-sub-menu index="recruitment">
            <template #title><el-icon><Briefcase /></el-icon><span>招聘管理</span></template>
            <el-menu-item v-if="auth.has('resume:view')" index="/recruitment/board"><el-icon><DataAnalysis /></el-icon>招聘看板</el-menu-item>
            <el-menu-item v-if="auth.has('resume:view')" index="/recruitment/resumes"><el-icon><Tickets /></el-icon>简历管理</el-menu-item>
            <el-menu-item v-if="auth.has('resume:view')" index="/recruitment/interviews"><el-icon><Calendar /></el-icon>面试安排</el-menu-item>
            <el-menu-item v-if="auth.has('resume:hire')" index="/recruitment/entry"><el-icon><Connection /></el-icon>入职办理</el-menu-item>
          </el-sub-menu>
          <el-menu-item v-if="auth.has('contract:view')" index="/contracts"><el-icon><Document /></el-icon><template #title>合同管理</template></el-menu-item>
          <el-sub-menu v-if="auth.has('system:user') || auth.has('system:role') || auth.has('system:menu') || auth.has('audit:view')" index="system">
            <template #title><el-icon><Setting /></el-icon><span>系统管理</span></template>
            <el-menu-item v-if="auth.has('system:user')" index="/system/users"><el-icon><User /></el-icon>用户管理</el-menu-item>
            <el-menu-item v-if="auth.has('system:role')" index="/system/roles"><el-icon><Operation /></el-icon>角色管理</el-menu-item>
            <el-menu-item v-if="auth.has('system:menu')" index="/system/menus"><el-icon><MenuIcon /></el-icon>菜单权限</el-menu-item>
            <el-menu-item v-if="auth.has('audit:view')" index="/system/audit"><el-icon><Tickets /></el-icon>操作日志</el-menu-item>
          </el-sub-menu>
        </el-menu>
      </el-scrollbar>
    </aside>
    <div class="main-shell">
      <header class="topbar">
        <button class="icon-button desktop-toggle" type="button" aria-label="折叠菜单" @click="app.toggleSidebar"><el-icon><Fold /></el-icon></button>
        <button class="icon-button mobile-toggle" type="button" aria-label="打开菜单" @click="app.mobileMenu = !app.mobileMenu"><el-icon><MenuIcon /></el-icon></button>
        <el-breadcrumb separator="/">
          <el-breadcrumb-item>Tianci OA</el-breadcrumb-item><el-breadcrumb-item>{{ title }}</el-breadcrumb-item>
        </el-breadcrumb>
        <div class="topbar-spacer" />
        <div class="global-search"><el-icon><Search /></el-icon><span>搜索功能 /</span></div>
        <el-dropdown trigger="click">
          <div class="user-chip"><el-avatar :size="32">{{ auth.user?.displayName?.slice(0, 1) || 'T' }}</el-avatar><span>{{ auth.user?.displayName }}</span><el-icon><ArrowDown /></el-icon></div>
          <template #dropdown><el-dropdown-menu><el-dropdown-item @click="signOut">退出登录</el-dropdown-item></el-dropdown-menu></template>
        </el-dropdown>
      </header>
      <div class="tabs-bar">
        <router-link v-for="tab in app.tabs" :key="tab.path" :to="tab.path" class="route-tab" :class="{ active: route.fullPath === tab.path }">
          {{ tab.title }}<span v-if="tab.closable" class="tab-close" @click.prevent.stop="closeTab(tab.path)">×</span>
        </router-link>
      </div>
      <main class="content"><router-view v-slot="{ Component }"><keep-alive><component :is="Component" /></keep-alive></router-view></main>
    </div>
    <div v-if="app.mobileMenu" class="mobile-mask" @click="app.mobileMenu = false" />
  </div>
</template>
