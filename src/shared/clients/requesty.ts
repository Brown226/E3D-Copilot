export class RequestyModelClient {
  static fetchModels(): Promise<any[]> { return Promise.resolve([]) }
}
export function toRequestyServiceUrl(_baseUrl: string): string { return _baseUrl }
