// 浏览器开发模式简化版：安全透传，undefined/null 返回空对象
export function fromProtobufModels(models: any): Record<string, any> {
	if (models == null) {
		return {}
	}
	return models
}
