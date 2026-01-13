using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    internal sealed class ChangeTracker
    {
        private static ChangeTracker _instance;
        public static ChangeTracker Tracker
        {
            get { return _instance ?? (_instance = new ChangeTracker()); }
        }

        private readonly HashSet<int> _dirtyMeshProperties = new HashSet<int>();
        private readonly HashSet<int> _dirtyMesh = new HashSet<int>();
        private readonly HashSet<int> _dirtyMaterial = new HashSet<int>();

        private ChangeTracker() { }

        public static void Reset()
        {
            if (_instance != null)
            {
                _instance._dirtyMeshProperties.Clear();
                _instance._dirtyMesh.Clear();
                _instance._dirtyMaterial.Clear();
            }
        }


        // Changes of transform of the object, obj.m_color, and obj.disableCastShadows should be processed through MarkMeshPropertiesDirty().
        public static void MarkMeshPropertiesDirty(int seqNo)
        {
            Tracker._dirtyMeshProperties.Add(seqNo);
        }

        public static void MarkMeshDirty(int seqNo)
        {
            Tracker._dirtyMesh.Add(seqNo);
        }

        public static void MarkMaterialDirty(int seqNo)
        {
            Tracker._dirtyMaterial.Add(seqNo);
        }
        public void Process()
        {
            foreach (int seqNo in _dirtyMeshProperties)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is processing new MeshProperties of seqNo {0}.", seqNo));
                if (seqNo < 0 || seqNo >= ProceduralObjectsLogic.instance.proceduralObjects.Count)
                {
                    Debug.Log(string.Format("[ProceduralObjects] ChangeTracker got an invalid seqNo: {0}.", seqNo));
                    continue;
                }
                Quad quad = ProceduralObjectsLogic.instance.proceduralObjects[seqNo].ownerQuad;
                quad.HandleObjectDirtyMeshProperties(seqNo);
            }
            _dirtyMeshProperties.Clear();

            foreach (int seqNo in _dirtyMesh)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is processing new Mesh of seqNo {0}.", seqNo));
                if (seqNo < 0 || seqNo >= ProceduralObjectsLogic.instance.proceduralObjects.Count)
                {
                    Debug.Log(string.Format("[ProceduralObjects] ChangeTracker got an invalid seqNo: {0}.", seqNo));
                    continue;
                }
            }
            _dirtyMesh.Clear();

            foreach (int seqNo in _dirtyMaterial)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is processing new Material of seqNo {0}.", seqNo));
                if (seqNo < 0 || seqNo >= ProceduralObjectsLogic.instance.proceduralObjects.Count)
                {
                    Debug.Log(string.Format("[ProceduralObjects] ChangeTracker got an invalid seqNo: {0}.", seqNo));
                    continue;
                }
            }
            _dirtyMaterial.Clear();
        }
    }
}
