using System;
using System.Windows.Forms;

namespace E3DCopilot.Loader
{
    /// <summary>
    /// 开发插件基类
    /// Loader 通过反射调用 CreatePanel/Destroy，但仍保留抽象类定义供编译引用
    /// Control 由子域创建后在父域使用（同域加载后无远程调用问题）
    /// </summary>
    public abstract class DevAddinBase : MarshalByRefObject
    {
        /// <summary>
        /// 创建 Copilot 面板
        /// </summary>
        public abstract Control CreatePanel();

        /// <summary>
        /// 销毁面板和控制器
        /// </summary>
        public abstract void Destroy();

        /// <summary>
        /// 默认无限生命周期（防止跨域代理被回收）
        /// </summary>
        public override object InitializeLifetimeService() => null;
    }
}
