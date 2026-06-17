// Protobuf type stubs for E3DCopilot WebView2

export class EmptyRequest {
  static create(_data?: any): EmptyRequest { return new EmptyRequest() }
}
export class StringRequest {
  value: string
  static create(data?: { value: string }): StringRequest {
    const r = new StringRequest()
    if (data) r.value = data.value
    return r
  }
}
export class BooleanRequest {
  value: boolean
  static create(data?: { value: boolean }): BooleanRequest {
    const r = new BooleanRequest()
    if (data) r.value = data.value
    return r
  }
}
export class Boolean {
  value: boolean
  static create(data?: { value: boolean }): Boolean {
    const r = new Boolean()
    if (data) r.value = data.value
    return r
  }
}
export class Int64Request {
  value: number
  static create(data?: { value: number }): Int64Request {
    const r = new Int64Request()
    if (data) r.value = data.value
    return r
  }
}
export class StringArrayRequest {
  value: string[]
  static create(data?: { value: string[] }): StringArrayRequest {
    const r = new StringArrayRequest()
    if (data) r.value = data.value
    return r
  }
}
