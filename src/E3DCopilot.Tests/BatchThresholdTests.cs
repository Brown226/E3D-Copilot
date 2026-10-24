/**
 * 批量阈值配置回归测试
 *
 * 历史缺陷：CommandPermissionController.IsBatchOperation 里写死 array.Count > 5，
 * 而 config.Safety.ConfirmBatchThreshold 的默认值是 10 且从未被读取 ——
 * 界面上写 5 个以上要确认，实际判断却和配置毫无关系。
 */
using NUnit.Framework;
using E3DCopilot.Core.Security;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class BatchThresholdTests
    {
        private static string Args(int arrLen)
        {
            var items = string.Join(",", System.Linq.Enumerable.Repeat("\"e\"", arrLen));
            return "{\"elements\":[" + items + "]}";
        }

        [Test]
        public void 默认阈值5_数组6个判为批量()
        {
            var ctrl = new CommandPermissionController();
            Assert.IsTrue(ctrl.IsBatchOperation(Args(6)));
        }

        [Test]
        public void 默认阈值5_数组5个不算批量()
        {
            var ctrl = new CommandPermissionController();
            Assert.IsFalse(ctrl.IsBatchOperation(Args(5)));
        }

        [Test]
        public void 阈值10_数组10个不算批量_不在旧写死的5上误判()
        {
            // 这是配置生效的核心证据：10 个元素在旧代码（写死 >5）里会被判为批量
            var ctrl = new CommandPermissionController(10);
            Assert.IsFalse(ctrl.IsBatchOperation(Args(10)));
        }

        [Test]
        public void 阈值10_数组11个判为批量()
        {
            var ctrl = new CommandPermissionController(10);
            Assert.IsTrue(ctrl.IsBatchOperation(Args(11)));
        }

        [Test]
        public void CreateDefault_支持传入阈值()
        {
            Assert.IsFalse(CommandPermissionController.CreateDefault(20).IsBatchOperation(Args(10)));
            Assert.IsTrue(CommandPermissionController.CreateDefault(2).IsBatchOperation(Args(10)));
        }

        [Test]
        public void 非法阈值回落到5()
        {
            var ctrl = new CommandPermissionController(0);
            Assert.IsTrue(ctrl.IsBatchOperation(Args(6)));
            Assert.IsFalse(ctrl.IsBatchOperation(Args(5)));
        }
    }
}
