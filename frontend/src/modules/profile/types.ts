// GET /api/profile (cualquier usuario con sesión).

export interface ExternalLoginItem {
  provider: string
  email: string
  linkedAt: string
}

export interface Profile {
  userId: number
  username: string
  email: string
  firstName: string
  lastName: string
  roleName: string
  externalLogins: ExternalLoginItem[]
}
