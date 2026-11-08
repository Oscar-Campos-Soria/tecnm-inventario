<script setup lang="ts">
import { useRoute } from 'vue-router'
import AuthLayout from './layouts/AuthLayout.vue'
import MainLayout from './layouts/MainLayout.vue'

const route = useRoute()
</script>

<template>
  <AuthLayout v-if="route.meta.layout === 'auth'">
    <RouterView />
  </AuthLayout>
  <MainLayout v-else>
    <RouterView />
  </MainLayout>
</template>
