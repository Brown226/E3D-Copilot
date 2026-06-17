export class ClineError extends Error {
  constructor(message: string) { super(message); this.name = "ClineError" }
}
export const ClineErrorType = {};
