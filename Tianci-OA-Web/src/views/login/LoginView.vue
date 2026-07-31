<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Lock, User } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const form = reactive({ username: 'admin', password: '' })
const error = ref('')

async function submit() {
  if (!form.username || !form.password) { error.value = '请输入用户名和密码'; return }
  loading.value = true; error.value = ''
  try {
    await auth.login(form.username, form.password)
    await router.replace(typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard')
  } catch (cause) { error.value = cause instanceof Error ? cause.message : '登录失败' }
  finally { loading.value = false }
}
</script>

<template>
  <div class="login-page">
    <section class="login-hero">
      <div class="hero-orb orb-one" /><div class="hero-orb orb-two" />
      <div class="hero-content">
        <div class="brand large"><span class="brand-mark">T</span><strong>Tianci OA</strong></div>
        <h1>让每一次人才流转<br />清晰、可靠、可追溯</h1>
        <p>招聘、入职、档案与合同的一体化协同工作台。</p>
        <div class="hero-stats"><span><strong>统一</strong>人员档案</span><span><strong>闭环</strong>招聘流程</span><span><strong>安全</strong>权限管理</span></div>
      </div>
    </section>
    <section class="login-panel">
      <el-card shadow="never" class="login-card">
        <div class="login-title"><small>欢迎回来</small><h2>登录 Tianci OA</h2><p>使用您的企业账号进入工作台</p></div>
        <el-alert v-if="error" :title="error" type="error" :closable="false" show-icon />
        <el-form size="large" @submit.prevent="submit">
          <el-form-item><el-input v-model="form.username" :prefix-icon="User" autocomplete="username" placeholder="用户名" /></el-form-item>
          <el-form-item><el-input v-model="form.password" :prefix-icon="Lock" type="password" show-password autocomplete="current-password" placeholder="密码" @keyup.enter="submit" /></el-form-item>
          <el-button type="primary" native-type="submit" :loading="loading" class="login-button">登录</el-button>
        </el-form>
        <p class="login-footnote">如无法登录，请联系系统管理员确认账号状态。</p>
      </el-card>
    </section>
  </div>
</template>
