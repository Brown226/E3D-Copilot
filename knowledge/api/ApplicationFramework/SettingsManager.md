# SettingsManager — 设置管理器

**命名空间**: `Aveva.ApplicationFramework`
**用途**: 管理插件和应用的持久化设置

## 获取实例

```csharp
var settings = SettingsManager.Instance;  // 单例
```

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| GetValue | `string GetValue(string group, string key, string defaultValue)` | 读取设置 |
| SetValue | `void SetValue(string group, string key, string value)` | 保存设置 |
| Save | `void Save()` | 持久化到磁盘 |

## 典型用法

```csharp
using Aveva.ApplicationFramework;

// 读取设置
string apiUrl = SettingsManager.Instance.GetValue(
    "E3DCopilot", "ApiUrl", "http://localhost:8000/v1");

string modelName = SettingsManager.Instance.GetValue(
    "E3DCopilot", "Model", "Qwen");

// 保存设置
SettingsManager.Instance.SetValue("E3DCopilot", "ApiUrl", "http://new-url:8000/v1");
SettingsManager.Instance.Save();
```

## 最佳实践

- 用分组的命名空间方式组织 Key（如 `"E3DCopilot/Server/Url"`）
- 务必提供默认值
- 修改后调用 `Save()` 确保持久化
