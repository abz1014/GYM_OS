export interface CurrentUser {
  id: string
  email: string
  firstName: string
  lastName: string
  mfaEnabled: boolean
  roles: string[]
  permissions: string[]
  accessibleBranchIds: string[]
}

export interface AuthResult {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: CurrentUser
}
