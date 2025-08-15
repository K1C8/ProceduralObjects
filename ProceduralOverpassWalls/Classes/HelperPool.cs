using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static ProceduralObjects.ProceduralObjectsLogic;

namespace ProceduralObjects.Classes
{
    internal static class HelperPool
    {
        static readonly Stack<Dictionary<Mesh, List<MeshProperties>>> dictionaryPool = new Stack<Dictionary<Mesh, List<MeshProperties>>>(50);
        //static readonly Stack<List<MeshProperties>> meshPropsPool = new Stack<List<MeshProperties>>(200);

        public static Dictionary<Mesh, List<MeshProperties>> GetMeshMeshPropDict()
        {
            return dictionaryPool.Count > 0 ? dictionaryPool.Pop() : new Dictionary<Mesh, List<MeshProperties>>();
        }

        public static void ReturnMeshMeshPropDict(Dictionary<Mesh, List<MeshProperties>> dict)
        {
            dict.Clear();
            dictionaryPool.Push(dict);
        }

        //public static List<MeshProperties> GetMeshPropsList()
        //{
        //    return meshPropsPool.Count > 0 ? meshPropsPool.Pop() : new List<MeshProperties>(64);
        //}

        //public static void ReturnMeshPropsList(List<MeshProperties> list)
        //{
        //    list.Clear();
        //    meshPropsPool.Push(list);
        //}
    }
}
