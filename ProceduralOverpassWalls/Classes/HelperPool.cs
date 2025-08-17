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
        static readonly Stack<Dictionary<Mesh, List<MeshProperties>>> dictionaryPool = new Stack<Dictionary<Mesh, List<MeshProperties>>>(50);
        static readonly Stack<List<int>> customListPool = new Stack<List<int>>(50);
        static readonly ThreadLocal<Stack<List<MeshProperties>>> localMeshPropsPool = new ThreadLocal<Stack<List<MeshProperties>>>(() => new Stack<List<MeshProperties>>(8));

        private static readonly object dictLock = new object();
        private static readonly object customListLock = new object();

        public static Dictionary<Mesh, List<MeshProperties>> GetMeshMeshPropDict()
        {
            lock (dictLock)
            {
                return dictionaryPool.Count > 0 ? dictionaryPool.Pop() : new Dictionary<Mesh, List<MeshProperties>>();
            }
        }

        public static void ReturnMeshMeshPropDict(Dictionary<Mesh, List<MeshProperties>> dict)
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

        public static List<int> GetCustomList()
        {
            lock (customListLock)
            {
                return customListPool.Count > 0 ? customListPool.Pop() : new List<int>();
            }
        }

        public static void ReturnCustomList(List<int> list)
        {
            lock (customListLock)
            {
                list.Clear();
                customListPool.Push(list);
            }
        }
    }
}
