using System.IO;
using E3DCopilot.Core.Memory;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class MemoryManagerTests
    {
        private string _dbPath;
        private MemoryManager _mgr;

        [SetUp]
        public void SetUp()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"e3dcopilot_test_{Path.GetRandomFileName()}.db");
            _mgr = new MemoryManager(_dbPath);
        }

        [TearDown]
        public void TearDown()
        {
            _mgr?.Dispose();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }

        [Test]
        public void Count_InitiallyZero()
        {
            Assert.AreEqual(0, _mgr.Count());
        }

        [Test]
        public void Save_NewEntry_AssignsId()
        {
            var entry = new MemoryEntry
            {
                Title = "Test Memory",
                Content = "Some content",
                Kind = "project_context"
            };

            var saved = _mgr.Save(entry);

            Assert.IsNotNull(saved.Id);
            Assert.IsNotEmpty(saved.Id);
            Assert.IsTrue(saved.Id.StartsWith("mem_"));
        }

        [Test]
        public void Save_NewEntry_SetsTimestamps()
        {
            var entry = new MemoryEntry
            {
                Title = "Test",
                Content = "Content"
            };

            var saved = _mgr.Save(entry);

            Assert.IsNotNull(saved.CreatedAt);
            Assert.IsNotNull(saved.UpdatedAt);
            Assert.IsNotEmpty(saved.CreatedAt);
        }

        [Test]
        public void Save_And_Count_Increases()
        {
            _mgr.Save(new MemoryEntry { Title = "M1", Content = "C1" });
            _mgr.Save(new MemoryEntry { Title = "M2", Content = "C2" });

            Assert.AreEqual(2, _mgr.Count());
        }

        [Test]
        public void Save_UpdateExisting_KeepsSameId()
        {
            var entry = _mgr.Save(new MemoryEntry { Title = "Original", Content = "V1" });
            string originalId = entry.Id;

            entry.Content = "V2";
            var updated = _mgr.Save(entry);

            Assert.AreEqual(originalId, updated.Id);
            Assert.AreEqual(1, _mgr.Count());
        }

        [Test]
        public void List_ReturnsAllEntries()
        {
            _mgr.Save(new MemoryEntry { Title = "A", Content = "CA", Kind = "k1" });
            _mgr.Save(new MemoryEntry { Title = "B", Content = "CB", Kind = "k2" });
            _mgr.Save(new MemoryEntry { Title = "C", Content = "CC", Kind = "k1" });

            var all = _mgr.List();
            Assert.AreEqual(3, all.Count);
        }

        [Test]
        public void List_WithKindFilter_ReturnsFilteredEntries()
        {
            _mgr.Save(new MemoryEntry { Title = "A", Content = "CA", Kind = "project_context" });
            _mgr.Save(new MemoryEntry { Title = "B", Content = "CB", Kind = "user_preference" });
            _mgr.Save(new MemoryEntry { Title = "C", Content = "CC", Kind = "project_context" });

            var filtered = _mgr.List("project_context");
            Assert.AreEqual(2, filtered.Count);
        }

        [Test]
        public void List_WithAllFilter_ReturnsAllEntries()
        {
            _mgr.Save(new MemoryEntry { Title = "A", Content = "CA", Kind = "k1" });
            _mgr.Save(new MemoryEntry { Title = "B", Content = "CB", Kind = "k2" });

            var all = _mgr.List("all");
            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void Delete_ExistingEntry_ReturnsTrue()
        {
            var entry = _mgr.Save(new MemoryEntry { Title = "To Delete", Content = "X" });

            bool result = _mgr.Delete(entry.Id);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _mgr.Count());
        }

        [Test]
        public void Delete_NonExistentEntry_ReturnsFalse()
        {
            bool result = _mgr.Delete("nonexistent_id");
            Assert.IsFalse(result);
        }

        [Test]
        public void Save_WithTags_PersistsTags()
        {
            var entry = new MemoryEntry
            {
                Title = "Tagged",
                Content = "C",
                Tags = new[] { "e3d", "piping" }
            };

            _mgr.Save(entry);
            var list = _mgr.List();

            Assert.AreEqual(2, list[0].Tags.Length);
            Assert.Contains("e3d", list[0].Tags);
            Assert.Contains("piping", list[0].Tags);
        }

        [Test]
        public void Save_WithScore_PersistsScore()
        {
            var entry = new MemoryEntry
            {
                Title = "Scored",
                Content = "C",
                Score = 0.95
            };

            _mgr.Save(entry);
            var list = _mgr.List();

            Assert.AreEqual(0.95, list[0].Score, 0.001);
        }

        [Test]
        public void Save_WithCustomId_UsesProvidedId()
        {
            var entry = new MemoryEntry
            {
                Id = "custom_id_123",
                Title = "Custom",
                Content = "C"
            };

            var saved = _mgr.Save(entry);

            Assert.AreEqual("custom_id_123", saved.Id);
        }

        [Test]
        public void List_OrderedByCreatedAtDesc()
        {
            _mgr.Save(new MemoryEntry { Title = "First", Content = "C1" });
            _mgr.Save(new MemoryEntry { Title = "Second", Content = "C2" });

            var list = _mgr.List();

            // Most recent first
            Assert.AreEqual("Second", list[0].Title);
            Assert.AreEqual("First", list[1].Title);
        }
    }
}
