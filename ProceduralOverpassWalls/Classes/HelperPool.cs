using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using static ProceduralObjects.ProceduralObjectsLogic;

namespace ProceduralObjects.Classes
{
    internal static class HelperPool
    {
        static readonly Stack<Dictionary<int, List<MeshProperties>>> dictionaryPool = new Stack<Dictionary<int, List<MeshProperties>>>(32);
        static readonly Stack<List<int>> intListPool = new Stack<List<int>>(32);
        static readonly Stack<HashSet<int>> visibilitySetPool = new Stack<HashSet<int>>(32);
        static readonly ThreadLocal<Stack<List<MeshProperties>>> localMeshPropsPool = new ThreadLocal<Stack<List<MeshProperties>>>(() => new Stack<List<MeshProperties>>(8));

        private static readonly object setLock = new object();
        private static readonly object dictLock = new object();
        private static readonly object customListLock = new object();

        public static Dictionary<int, List<MeshProperties>> GetIntMeshPropDict()
        {
            lock (dictLock)
            {
                return dictionaryPool.Count > 0 ? dictionaryPool.Pop() : new Dictionary<int, List<MeshProperties>>();
            }
        }

        public static void ReturnIntMeshPropDict(Dictionary<int, List<MeshProperties>> dict)
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

        public static List<MeshProperties> GetMeshPropsList()
        {
            return localMeshPropsPool.Value.Count > 0 ? localMeshPropsPool.Value.Pop() : new List<MeshProperties>();
        }

        public static void ReturnMeshPropsList(List<MeshProperties> list)
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
