using System.Collections.Generic;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    public class QuadTree
    {
        public Quad root;
        private readonly int maxLevel;
        private List<Quad> leafQuads;
        public QuadTree(int _max) 
        {
            ProceduralObjectsLogic logic = ProceduralObjectsLogic.instance;
            List<int> poIndices = new List<int>();
            for (int i = 0; i < logic.proceduralObjects.Count; i++)
            {
                if (logic.proceduralObjects[i] == null)
                {
                    continue;
                }
                poIndices.Add(i);
            }
            maxLevel = _max;
            Bounds rootBounds = new Bounds(new Vector3(0f, 5000f, 0f), new Vector3(32000f, 10240f, 32000f));
            root = new Quad(0, _max, null, rootBounds, poIndices);

            leafQuads = new List<Quad>();
        }

        public void DrawQuadBounds()
        {
            root.DrawQuadBounds();
        }

        public void HideQuadBounds()
        {
            root.HideQuadBounds();
        }

        public List<Quad> GetLeafQuads()
        {
            leafQuads.Clear();
            return root.GetLeaves(ref leafQuads);
        }

        public bool AddObjectToQuadTree(int seqNo)
        {
            return root.TakeInPoAfterCreated(seqNo);
        }
    }
}
