export class AskResponseRequest {
  static create(data?: any): AskResponseRequest { return new AskResponseRequest() }
}
export class NewTaskRequest {
  static create(data?: any): NewTaskRequest { return new NewTaskRequest() }
}
export class GetTaskHistoryRequest { static create(_data?: any): GetTaskHistoryRequest { return new GetTaskHistoryRequest() } }
export class TaskFavoriteRequest { static create(_data?: any): TaskFavoriteRequest { return new TaskFavoriteRequest() } }
