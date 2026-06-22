export function convertProtoToClineMessage(msg: any): any {
  return msg
}
export function convertProtoMcpServersToMcpServers(servers: any[]): any[] {
  return servers || []
}
export function fromProtobufModels(models: any): Record<string, any> {
	if (models == null) {
		return {}
	}
	return models
}
export function convertApiConfigurationToProto(config: any): any {
  return config
}
