using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ProceduralObjectsTests
{
    public sealed class WorkObject 
    { 
        public int Id; 
        public string Name;
    }

    [TestClass]
    public sealed class HelperPoolTest
    {
        [TestMethod]
        public async Task TestGetMeshPropsListLocaliness() 
        {
            List<int>? listMeshAlpha = null;
            List<int>? listMeshBeta = null;

            await Task.WhenAll(
                Task.Run(() => listMeshAlpha = HelperPool<int>.GetGenericList()),
                Task.Run(() => listMeshBeta = HelperPool<int>.GetGenericList())
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
            List<int> nullList = null;
            HelperPool<int>.ReturnIntList(nullList);
        }
    }

    [TestClass]
    public class StubTests
    {
        [TestMethod]
        public void PipelineE2ESingleThreadSmokeTest()
        {
            List<WorkObject> produced = Enumerable.Range(0, 200)
                .Select(i => new WorkObject { Id = i, Name = $"w{i}" })
                .ToList();

            List<String> sink = new List<string>();

            List<WorkObject> batched = HelperPool<WorkObject>.GetGenericList();
            int batchSize = 32;

            try
            {
                foreach (WorkObject wo in produced)
                {
                    batched.Add(wo);
                    if (batched.Count == batchSize)
                    {
                        SaveBatch(batched, sink);
                        HelperPool<WorkObject>.ReturnGenericList(batched);
                        batched = HelperPool<WorkObject>.GetGenericList();
                    }
                }
                if (batched.Count > 0)
                {
                    SaveBatch(batched, sink);
                }
            }
            finally
            {
                HelperPool<WorkObject>.ReturnGenericList(batched);
            }

            Assert.AreEqual(200, sink.Count);
            Assert.IsTrue(sink[0].StartsWith("w0->"));

            List<WorkObject> test = HelperPool<WorkObject>.GetGenericList();
            Assert.AreEqual(0, test.Count);
            HelperPool<WorkObject>.ReturnGenericList(test);
        }

        [TestMethod]
        public void PipelineE2EParallelSmokeTest()
        {
            ConcurrentBag<String> parallelSink = new ConcurrentBag<string>();
            Parallel.For(0, 8, _ =>
            {
                List<WorkObject> localBatch = HelperPool<WorkObject>.GetGenericList();
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        localBatch.Add(new WorkObject { Id = i, Name = $"t{Task.CurrentId ?? 0}-{i}" });
                    }
                    SaveBatchMultiThread(localBatch, parallelSink);
                }
                finally
                {
                    HelperPool<WorkObject>.ReturnGenericList(localBatch);
                }
            });

            Assert.AreEqual(parallelSink.Count, 800);
            List<WorkObject> test = HelperPool<WorkObject>.GetGenericList();
            Assert.AreEqual(0, test.Count);
            HelperPool<WorkObject>.ReturnGenericList(test);
        }


        private static void SaveBatch(IEnumerable<WorkObject> batch, ICollection<String> sink)
        {
            foreach (WorkObject wo in batch)
            {
                sink.Add($"{wo.Name}->{wo.Id}");
            }
        }

        private static void SaveBatchMultiThread(IEnumerable<WorkObject> batch, IProducerConsumerCollection<String> sink)
        {
            foreach (WorkObject wo in batch)
            {
                sink.TryAdd($"{wo.Name}->{wo.Id}");
            }
        }
    }
}
