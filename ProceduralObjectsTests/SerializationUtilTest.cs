using ProceduralObjects;
using ProceduralObjects.Classes;
using UnityEngine;

// functional, data-driven, exception, async, and one integration test stub

namespace ProceduralObjectsTests
{
    [TestClass]
    public sealed class SerializationUtilTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestToVector3()
        {
            SerializableVector3 sv3 = new SerializableVector3(1f, 1f, 1f);
            Vector3 target = new Vector3(1f, 1f, 1f);
            Assert.AreEqual(target, SerializationUtils.ToVector3(sv3));
        }

        [TestMethod]
        public void TestToQuaternion()
        {
            Quaternion origin = Quaternion.identity;
            SerializableQuaternion sq = new SerializableQuaternion(origin);
            Quaternion target = SerializationUtils.ToQuaternion(sq);
            Assert.AreEqual(target, origin);
        }

        [DataTestMethod]
        [DataRow("Test-test.xml", "Test-test.xml")]
        [DataRow("a b", "a_b")]
        [DataRow("q<a>b", "qab")]
        [DataRow("foo\\bar/baz", "foobarbaz")]
        public void TestToFileNameCSV(string input, string expected)
        {
            Assert.AreEqual(expected, SerializationUtils.ToFileName(input));
        }
    }
}
