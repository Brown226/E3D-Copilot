export class TrackWorktreeViewOpenedRequest { static create(_data?: any): any { return {} } }
export class CreateWorktreeRequest { static create(_data?: any): any { return {} } }
export class SwitchWorktreeRequest { static create(_data?: any): any { return {} } }
export class DeleteWorktreeRequest { static create(_data?: any): any { return {} } }
export class MergeWorktreeRequest { static create(_data?: any): any { return {} } }
export class CreateWorktreeIncludeRequest { static create(_data?: any): any { return {} } }
export class CheckoutBranchRequest { static create(_data?: any): any { return {} } }

export interface Worktree {
  path: string
  branch: string
  commit_hash: string
  is_current: boolean
  is_bare: boolean
  is_detached: boolean
  is_locked: boolean
  lock_reason?: string
}

export interface WorktreeList {
  worktrees: Worktree[]
  is_git_repo: boolean
  error: string
  is_multi_root: boolean
  is_subfolder: boolean
  git_root_path: string
}

export interface WorktreeResult {
  success: boolean
  message: string
  worktree?: Worktree
}

export interface BranchList {
  local_branches: string[]
  remote_branches: string[]
  current_branch: string
}

export interface WorktreeDefaults {
  suggested_branch: string
  suggested_path: string
}

export interface WorktreeIncludeStatus {
  exists: boolean
  gitignore_content: string
  has_gitignore: boolean
}

export interface MergeWorktreeResult {
  success: boolean
  message: string
  has_conflicts: boolean
  conflicting_files: string[]
  source_branch: string
  target_branch: string
}
