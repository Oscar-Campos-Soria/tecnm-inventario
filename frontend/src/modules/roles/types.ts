// Roles y permisos (spec: README.md de este módulo).

export interface Role {
  roleId: number
  code: string
  name: string
  description: string | null
  permissions: string[]
}

export interface Permission {
  code: string
  name: string
  description: string | null
  module: string
}
