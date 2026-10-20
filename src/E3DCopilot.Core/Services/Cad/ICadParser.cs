using E3DCopilot.Core.Models.Building;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// CAD 图纸解析器抽象。
    ///
    /// 存在两个实现：
    /// - TeighaCadParserService：进程内 Teigha 解析，按图层名关键词筛选，受 x86 位数限制
    /// - SidecarCadParserService：调用独立进程 dwg-sidecar，读取全量真实几何
    ///
    /// 抽象的目的是让 CadImportHandler 不再直接 new 具体实现，
    /// 换解析引擎时上层业务代码不动。
    /// </summary>
    public interface ICadParser
    {
        /// <summary>
        /// 解析 DWG/DXF 文件，返回其中的线段。
        /// </summary>
        /// <param name="filePath">图纸绝对路径</param>
        /// <returns>解析结果；失败时 Success=false 且 Error 有值</returns>
        CadParseResult ParseFile(string filePath);
    }
}
