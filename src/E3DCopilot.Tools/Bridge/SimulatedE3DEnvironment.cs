using System.Collections.Generic;
using System.Linq;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 模拟 E3D 环境（用于 TestHost 独立测试，无需真实 E3D）
    /// 提供预设的管道/设备/结构数据
    /// </summary>
    public class SimulatedE3DEnvironment : IE3DEnvironment
    {
        private readonly Dictionary<string, Dictionary<string, string>> _elements;

        public SimulatedE3DEnvironment()
        {
            _elements = new Dictionary<string, Dictionary<string, string>>();

            // 预设管道数据
            AddElement("PIPE-001", "PIPE", new Dictionary<string, string>
            {
                { "NAME", "PIPE-001" },
                { "TYPE", "PIPE" },
                { "DIA", "DN100" },
                { "SPEC", "SCH40" },
                { "MATERIAL", "CS" },
                { "LENGTH", "15000" },
                { "INSULATION", "50mm Mineral Wool" }
            });

            AddElement("PIPE-002", "PIPE", new Dictionary<string, string>
            {
                { "NAME", "PIPE-002" },
                { "TYPE", "PIPE" },
                { "DIA", "DN200" },
                { "SPEC", "SCH80" },
                { "MATERIAL", "SS316" },
                { "LENGTH", "8500" }
            });

            AddElement("EQUI-001", "EQUI", new Dictionary<string, string>
            {
                { "NAME", "EQUI-001" },
                { "TYPE", "EQUIPMENT" },
                { "SUBTYPE", "PUMP" },
                { "DESCRIPTION", "Centrifugal Pump P-101A" },
                { "POSITION", "X=1000 Y=2000 Z=500" }
            });

            AddElement("STRU-001", "STRU", new Dictionary<string, string>
            {
                { "NAME", "STRU-001" },
                { "TYPE", "STRUCTURE" },
                { "SUBTYPE", "BEAM" },
                { "SECTION", "H200x200x8x12" },
                { "LENGTH", "6000" }
            });
        }

        private void AddElement(string name, string type, Dictionary<string, string> attrs)
        {
            _elements[name.ToUpper()] = attrs;
        }

        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            var results = new List<ElementInfo>();
            var queryType = (elementType ?? "").ToUpper();
            var pattern = (namePattern ?? "*").Replace("*", "");

            foreach (var kvp in _elements)
            {
                var attrs = kvp.Value;
                var elemType = attrs.ContainsKey("TYPE") ? attrs["TYPE"] : "";

                // 类型过滤
                if (!string.IsNullOrEmpty(queryType) && !elemType.ToUpper().Contains(queryType))
                    continue;

                // 名称模式过滤
                if (!string.IsNullOrEmpty(pattern) && !kvp.Key.Contains(pattern.ToUpper()))
                    continue;

                var info = new ElementInfo
                {
                    Name = kvp.Key,
                    Type = elemType,
                    DbUri = $"/{kvp.Key}",
                    Attributes = new Dictionary<string, string>(attrs)
                };
                results.Add(info);

                if (results.Count >= limit)
                    break;
            }

            return results;
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            var key = (elementName ?? "").ToUpper();
            if (!_elements.TryGetValue(key, out var attrs))
                return null;

            var attrKey = (attributeName ?? "").ToUpper();
            return attrs.ContainsKey(attrKey) ? attrs[attrKey] : null;
        }

        public void SetAttribute(string elementName, string attributeName, string value)
        {
            var key = (elementName ?? "").ToUpper();
            if (!_elements.ContainsKey(key))
                return;

            _elements[key][(attributeName ?? "").ToUpper()] = value;
        }

        public bool CheckExists(string elementName)
        {
            return _elements.ContainsKey((elementName ?? "").ToUpper());
        }

        public string ExecutePml(string pmlCommand)
        {
            return $"[模拟] PML 执行成功: {pmlCommand}";
        }

        public string GetCurrentElementName()
        {
            return "PIPE-001";
        }
    }
}
