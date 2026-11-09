// Tipos de administración de usuarios (spec: specs/users.spec.md).

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type UserStatus = 'activo' | 'inactivo' | 'suspendido'

export interface ManagedUser {
  userId: number
  username: string
  email: string
  firstName: string
  lastName: string
  roleCode: string
  roleName: string
  status: UserStatus
  createdAt: string
  version: number
}

export interface RoleOption {
  roleId: number
  code: string
  name: string
}

export interface UserFilters {
  search?: string
  role?: string
  status?: UserStatus
}

export interface NewUserInput {
  username: string
  email: string
  password: string
  firstName: string
  lastName: string
  roleCode: string
}

export interface EditUserInput {
  email: string
  firstName: string
  lastName: string
  roleCode: string
}
