using System.Collections.Generic;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    public class QuadTree
    {
        public Quad root;
        private readonly int maxLevel;
        public QuadTree(int _max) 
        {
            ProceduralObjectsLogic logic = ProceduralObjectsLogic.instance;
            List<int> poIndices = new List<int>();
            for (int i = 0; i < logic.proceduralObjects.Count; i++)
            {
                poIndices.Add(i);
            }
            maxLevel = _max;
            Bounds rootBounds = new Bounds(new Vector3(0f, 512f, 0f), new Vector3(32000f, 1024f, 32000f));
            root = new Quad(0, _max, null, rootBounds, poIndices);
        }

        public void DrawQuadBounds()
        {
            root.DrawQuadBounds();
        }

        public List<Quad> GetLeafQuads()
        {
            return root.GetLeaves();
        }

    }
}
