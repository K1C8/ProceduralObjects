using System.Collections.Generic;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    internal sealed class ChangeTracker
    {
        private static ChangeTracker _instance;
        private static bool _isChanged = false;
        public static ChangeTracker Tracker
        {
            get { return _instance ?? (_instance = new ChangeTracker()); }
        }

        public static bool HasChangeLastFrame
        {
            get { return _isChanged; }
            set { _isChanged = value; }
        }

        private readonly HashSet<int> _dirtyMeshProperties = new HashSet<int>();
        private readonly HashSet<int> _dirtyMesh = new HashSet<int>();
        private readonly HashSet<int> _dirtyMaterial = new HashSet<int>();
        private readonly HashSet<int> _dirtyShader = new HashSet<int>();
        private readonly HashSet<int> _objectsToRemove = new HashSet<int>();
        private readonly HashSet<int> _objectsToAdd = new HashSet<int>();

        private ChangeTracker() { }

        public static void Reset()
        {
            if (_instance != null)
            {
                _instance._dirtyMeshProperties.Clear();
                _instance._dirtyMesh.Clear();
                _instance._dirtyMaterial.Clear();
                _instance._dirtyShader.Clear();
                _instance._objectsToRemove.Clear();
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

        public static void MarkShaderDirty(int seqNo)
        {
            Tracker._dirtyShader.Add(seqNo);
        }

        public static void RemoveObjectFromQuadTree(int seqNo)
        {
            Tracker._objectsToRemove.Add(seqNo);
        }

        public static void AddObjectToQuadTree(int seqNo)
        {
            Tracker._objectsToAdd.Add(seqNo);
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
                Quad quad = ProceduralObjectsLogic.instance.proceduralObjects[seqNo]?.ownerQuad;
                quad?.HandleObjectDirtyMeshProperties(seqNo);
            }
            if (_dirtyMeshProperties.Count > 0)
            {
                _isChanged = true;
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

                Quad quad = ProceduralObjectsLogic.instance.proceduralObjects[seqNo]?.ownerQuad;
                quad?.HandleObjectDirtyMeshAndMaterial(seqNo);
            }
            if (_dirtyMesh.Count > 0)
            {
                _isChanged = true;
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

                Quad quad = ProceduralObjectsLogic.instance.proceduralObjects[seqNo]?.ownerQuad;
                quad?.HandleObjectDirtyMeshAndMaterial(seqNo);
            }
            if (_dirtyMaterial.Count > 0)
            {
                _isChanged = true;
            }
            _dirtyMaterial.Clear();

            foreach (int seqNo in _dirtyShader)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is checking shader of seqNo {0}.", seqNo));
            }
            if (_dirtyShader.Count > 0)
            {
                _isChanged = true;
            }
            _dirtyShader.Clear();

            foreach (int seqNo in _objectsToRemove)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is removing PO seqNo {0} from the QuadTree.", seqNo));

                Quad quad = ProceduralObjectsLogic.instance.proceduralObjects[seqNo]?.ownerQuad;
                quad?.RemoveObjectFromQuad(seqNo);
                ProceduralObjectsLogic.instance.proceduralObjects[seqNo] = null;
            }
            if (_objectsToRemove.Count > 0)
            {
                _isChanged = true;
            }
            _objectsToRemove.Clear();

            foreach (int seqNo in _objectsToAdd)
            {
                Debug.Log(string.Format("[ProceduralObjects] ChangeTracker is adding PO seqNo {0} to the QuadTree.", seqNo));
                if (!ProceduralObjectsLogic.instance.QuadTree.AddObjectToQuadTree(seqNo))
                {
                    Debug.Log(string.Format("[ProceduralObjects] ChangeTracker encountered error when adding PO seqNo {0} to the QuadTree.", seqNo));
                }
            }
            if (_objectsToAdd.Count > 0)
            {
                _isChanged = true;
            }
            _objectsToAdd.Clear();
        }
    }
}
