using E3DCopilot.Core.Security;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class SecretRedactorTests
    {
        [Test]
        public void Redact_OpenAiKey_Masked()
        {
            var text = "key is sk-abcdefghijklmnopqrstuvwxyz123456";
            var r = SecretRedactor.Redact(text);
            Assert.IsFalse(r.Contains("sk-abcdefghijklmnopqrstuvwxyz123456"));
            Assert.IsTrue(r.Contains("sk-"));
        }

        [Test]
        public void Redact_AwsKey_Masked()
        {
            var r = SecretRedactor.Redact("akiaABCDEFGHIJKLMNOP");
            Assert.IsFalse(r.Contains("AKIAABCDEFGHIJKLMNOP"));
        }

        [Test]
        public void Redact_ConnectionString_Masked()
        {
            var r = SecretRedactor.Redact("postgres://admin:pass123@db.host:5432/app");
            Assert.IsTrue(r.Contains("[REDACTED CONNECTION STRING]"));
        }

        [Test]
        public void Redact_PasswordAssignment_Masked()
        {
            var r = SecretRedactor.Redact("password=SuperSecret123");
            Assert.IsFalse(r.Contains("SuperSecret123"));
            Assert.IsTrue(r.Contains("password="));
        }

        [Test]
        public void Redact_PrivateKey_Masked()
        {
            var block = "-----BEGIN RSA PRIVATE KEY-----\nMIIabc\n-----END RSA PRIVATE KEY-----";
            var r = SecretRedactor.Redact(block);
            Assert.IsTrue(r.Contains("[REDACTED PRIVATE KEY]"));
        }

        [Test]
        public void Redact_PlainText_Unchanged()
        {
            var text = "请在 E3D 中创建一个 PIPE 元件";
            Assert.AreEqual(text, SecretRedactor.Redact(text));
        }

        [Test]
        public void ContainsSecret_Detects()
        {
            Assert.IsTrue(SecretRedactor.ContainsSecret("token=abc123def456ghi789"));
            Assert.IsFalse(SecretRedactor.ContainsSecret("正常文本无密钥"));
        }
    }
}
