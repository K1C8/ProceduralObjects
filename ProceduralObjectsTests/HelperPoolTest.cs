using System.Threading;
using System.Threading.Tasks;

namespace ProceduralObjectsTests
{
    [TestClass]
    public sealed class HelperPoolTest
    {
        [TestMethod]
        public async Task TestGetMeshPropsListLocaliness() 
        {
            List<int>? listMeshAlpha = null;
            List<int>? listMeshBeta = null;

            await Task.WhenAll(
                Task.Run(() => listMeshAlpha = HelperPool<int>.GetMeshPropsList()),
                Task.Run(() => listMeshBeta = HelperPool<int>.GetMeshPropsList())
            );
            
            
            Assert.IsNotNull(listMeshAlpha);
            Assert.IsNotNull(listMeshBeta);
            Assert.AreNotSame(listMeshBeta, listMeshAlpha);  

        }

        [TestMethod]
        public void TestGetIntList_Concurrent()
        {
            Parallel.For(0, 1000, i =>
            {
                List<int>? list = HelperPool<int>.GetIntList();
                list.Add(i);
                if (i > 0)
                {
                    Assert.IsFalse(list.Contains(i - 1));
                }
                HelperPool<int>.ReturnIntList(list);
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestReturnIntList_Null()
        {
            HelperPool<int>.ReturnIntList(null);
        }
    }
}
