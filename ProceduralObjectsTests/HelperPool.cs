using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProceduralObjectsTests
{
    internal static class HelperPool<T>
    {
        static readonly Stack<Dictionary<int, List<T>>> dictionaryPool = new Stack<Dictionary<int, List<T>>>(32);
        static readonly Stack<List<int>> intListPool = new Stack<List<int>>(32);
        static readonly Stack<HashSet<int>> visibilitySetPool = new Stack<HashSet<int>>(32);
        static readonly ThreadLocal<Stack<List<T>>> localMeshPropsPool = new ThreadLocal<Stack<List<T>>>(() => new Stack<List<T>>(8));

        private static readonly object setLock = new object();
        private static readonly object dictLock = new object();
        private static readonly object customListLock = new object();

        public static Dictionary<int, List<T>> GetIntMeshPropDict()
        {
            lock (dictLock)
            {
                return dictionaryPool.Count > 0 ? dictionaryPool.Pop() : new Dictionary<int, List<T>>();
            }
        }

        public static void ReturnIntMeshPropDict(Dictionary<int, List<T>> dict)
        {
            foreach (var pair in dict)
            {
                pair.Value.Clear();
                ReturnMeshPropsList(pair.Value);
            }

            dict.Clear();
            lock (dictLock)
            {
                dictionaryPool.Push(dict);
            }
        }

        public static List<T> GetMeshPropsList()
        {
            return localMeshPropsPool.Value.Count > 0 ? localMeshPropsPool.Value.Pop() : new List<T>();
        }

        public static void ReturnMeshPropsList(List<T> list)
        {
            list.Clear();
            localMeshPropsPool.Value.Push(list);
        }

        public static List<int> GetIntList()
        {
            lock (customListLock)
            {
                return intListPool.Count > 0 ? intListPool.Pop() : new List<int>();
            }
        }

        public static void ReturnIntList(List<int> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("ReturnIntList() got a null to return.");
            }
            list.Clear();
            lock (customListLock)
            {
                intListPool.Push(list);
            }
        }

        public static HashSet<int> GetVisibilitySet()
        {
            lock (setLock)
            {
                return visibilitySetPool.Count > 0 ? visibilitySetPool.Pop() : new HashSet<int>();
            }
        }

        public static void ReturnVisibilitySet(HashSet<int> set)
        {
            set.Clear();
            lock (setLock)
            {
                visibilitySetPool.Push(set);
            }
        }
    }
}
