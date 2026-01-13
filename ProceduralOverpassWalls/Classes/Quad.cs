using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static ProceduralObjects.Classes.ProceduralUtils;
using static ProceduralObjects.ProceduralObjectsLogic;
using static ProceduralObjects.ProceduralText.TextParameters;
using static System.Net.WebRequestMethods;

namespace ProceduralObjects.Classes
{
    public class Quad
    {
        public Quad parent;
        public Quad[] children;
        public readonly Bounds bounds;

        public List<int> allPoSeqList;
        public HashSet<int> allPoSeqSet;
        private Dictionary<int, BatchHandle> poSeqBatchHandleDict;
        // -------- All POs must be inside all collections above.    --------
        // -------- All POs must be inside exactly ONE option below. --------
        // -- Option A: Original Batchable
        public Dictionary<string, List<int>> batchOriginalPoIdDict;
        public Dictionary<string, List<MeshProperties>> batchOriginalArrayDict;
        // -- Option B: Custom Batchable
        public Dictionary<string, List<int>> batchCustomPoIdDict;
        public Dictionary<string, List<MeshProperties>> batchCustomArrayDict;
        // -- Option C: Unbatchable
        public List<int> unbatchableList;

        private readonly int _level;
        private readonly int _maxLevel;

        private int _maxPoCount = 4096;
        private int _repeateModifiedMeshCount = 0;
        private int _unmodifiedMeshCount = 0;
        private int _minimumBatchSize = 3;
        private Vector3[] corners;
        private Vector3[] line;
        private string defaultPropShaderStr = "Custom/Props/Prop/Default";
        private string batchedPropShaderStr = "Custom/ProceduralObject/Prop/testshaderind";

        // Debug line boxes
        GameObject lineObject;
        LineRenderer lineRenderer;
        private Material outlineMat;

        public Quad(int level, int maxLevel, Quad p, Bounds bounds, List<int> proceduralObjectIndexSeqs)
        {
            _level = level;
            _maxLevel = maxLevel;
            batchCustomArrayDict = new Dictionary<string, List<MeshProperties>>();
            batchOriginalArrayDict = new Dictionary<string, List<MeshProperties>>();
            batchCustomPoIdDict = new Dictionary<string, List<int>>();
            batchOriginalPoIdDict = new Dictionary<string, List<int>>();
            poSeqBatchHandleDict = new Dictionary<int, BatchHandle>();
            unbatchableList = new List<int>();
            allPoSeqList = new List<int>();
            allPoSeqSet = new HashSet<int>();
            this.bounds = bounds;
            children = null;
            parent = p;

            Debug.Log($"[ProceduralObjects] New Quad centered at {{{bounds.center}}} receives {proceduralObjectIndexSeqs.Count} POs.");

            if (proceduralObjectIndexSeqs.Count > _maxPoCount && _level + 1 < maxLevel)
            {
                children = Divide(proceduralObjectIndexSeqs);
            }

            // TODO: Temporary, should be seperated into a function to process POs at the leaves of the QuadTree when no further Divide() needed.
            if (children == null)  
            {
                allPoSeqList = proceduralObjectIndexSeqs;
                allPoSeqSet.UnionWith(allPoSeqList);
                List<ProceduralObject> proceduralObjects = ProceduralObjectsLogic.instance.proceduralObjects;
                using (SHA1 sha1 = SHA1.Create())
                {
                    foreach (int poSeq in allPoSeqList)
                    {
                        var obj = proceduralObjects[poSeq];
                        obj.ownerQuad = this;
                        if (obj.baseInfoType == "BUILDING" || obj.customTexture != null || !obj.m_material.shader.name.Equals(defaultPropShaderStr))
                        {
                            poSeqBatchHandleDict.Add(poSeq, new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatchable));
                            unbatchableList.Add(poSeq);
                            continue;
                        }
                        else if(obj.meshStatus == 1 && obj.baseInfoType == "PROP" && obj.m_textParameters == null)
                        {
                            if (!batchOriginalPoIdDict.ContainsKey(obj._baseProp.name))
                                batchOriginalPoIdDict[obj._baseProp.name] = new List<int>();
                            if (!batchOriginalArrayDict.ContainsKey(obj._baseProp.name))
                                batchOriginalArrayDict[obj._baseProp.name] = new List<MeshProperties>();

                            batchOriginalArrayDict[obj._baseProp.name].Add(new MeshProperties(obj));
                            batchOriginalPoIdDict[obj._baseProp.name].Add(poSeq);

                            poSeqBatchHandleDict.Add(poSeq, new BatchHandle(obj._baseProp.name, batchOriginalPoIdDict[obj._baseProp.name].Count, ObjectStatus.Original));
                            //_unmodifiedMeshCount++;
                            continue;
                        }
                        
                        string objHashStr = GetObjHashString(obj, sha1);

                        // Add listhead OR add followers, choose only one
                        if (!batchCustomArrayDict.ContainsKey(objHashStr) && !batchCustomPoIdDict.ContainsKey(objHashStr))
                        {
                            batchCustomArrayDict[objHashStr] = new List<MeshProperties> { new MeshProperties(obj) };
                            batchCustomPoIdDict[objHashStr] = new List<int> { poSeq };
                            poSeqBatchHandleDict.Add(poSeq, new BatchHandle(objHashStr, batchCustomPoIdDict[objHashStr].Count, ObjectStatus.CustomBatchable));
                        }
                        else
                        {
                            int listHead = batchCustomPoIdDict[objHashStr][0];

                            if (((obj.m_textParameters == null && proceduralObjects[listHead].m_textParameters == null) ||
                                (!IsDifference(obj.m_textParameters, proceduralObjects[listHead].m_textParameters))) &&
                                CheckMeshEquivalance(obj.m_mesh.vertices, proceduralObjects[listHead].m_mesh.vertices))
                            {
                                batchCustomArrayDict[objHashStr].Add(new MeshProperties(obj));
                                batchCustomPoIdDict[objHashStr].Add(poSeq);
                                poSeqBatchHandleDict.Add(poSeq, new BatchHandle(objHashStr, batchCustomPoIdDict[objHashStr].Count, ObjectStatus.CustomBatchable));
                            }
                            else
                                unbatchableList.Add(poSeq);
                        }
                    }
                }

                List<string> keysToRemove = new List<string>();
                foreach (var kv in batchCustomPoIdDict)
                {
                    if (kv.Value.Count >= _minimumBatchSize)
                    {
                        _repeateModifiedMeshCount += kv.Value.Count;
                        Debug.Log($"[ProceduralObjects] Quad {bounds.center} loaded repeated meshStatus 2 meshes, meshId: {kv.Key}, count: {kv.Value.Count}");
                    }
                    else
                    {
                        for (int i = 0; i < kv.Value.Count; i++)
                        {
                            int poSeq = kv.Value[i];
                            poSeqBatchHandleDict[poSeq] = new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatchable);
                            unbatchableList.Add(poSeq);
                        }
                        keysToRemove.Add(kv.Key);
                    }
                }
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    string keyToRemove = keysToRemove[i];
                    batchCustomArrayDict.Remove(keyToRemove);
                    batchCustomPoIdDict.Remove(keyToRemove);
                }

                keysToRemove.Clear();
                foreach (var kv in batchOriginalPoIdDict)
                {
                    if (kv.Value.Count >= _minimumBatchSize)
                    {
                        _unmodifiedMeshCount += kv.Value.Count;
                        Debug.Log($"[ProceduralObjects] Quad {bounds.center} loaded batchable unmodified meshStatus 1 meshes, meshId: {kv.Key}, count: {kv.Value.Count}");
                    }
                    else
                    {
                        for (int i = 0; i < kv.Value.Count; i++)
                        {
                            int poSeq = kv.Value[i];
                            poSeqBatchHandleDict[poSeq] = new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatchable);
                            unbatchableList.Add(poSeq);
                        }

                        keysToRemove.Add(kv.Key);

                    }
                }

                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    string keyToRemove = keysToRemove[i];
                    batchOriginalArrayDict.Remove(keyToRemove);
                    batchOriginalPoIdDict.Remove(keyToRemove);
                }

                if (_unmodifiedMeshCount + _repeateModifiedMeshCount > 0)
                {
                    Debug.Log($"[ProceduralObjects] In Quad {bounds.center}, total batchable unmodified meshStatus 1 meshes counting at: {_unmodifiedMeshCount}, " +
                        $"total batchable repeated meshStatus 2 meshes counting at: {_repeateModifiedMeshCount}");
                }

            }

            CreateBoundsAndLines();
            
        }

        private Quad[] Divide(List<int> proceduralObjectIndices)
        {
            Quad[] children = new Quad[4];
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            Vector3 halfSize = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            Bounds botLeft = new Bounds(new Vector3((min.x + center.x) * 0.5f, center.y, (min.z + center.z) * 0.5f), halfSize);
            Bounds topLeft = new Bounds(new Vector3((min.x + center.x) * 0.5f, center.y, (center.z + max.z) * 0.5f), halfSize);
            Bounds topRight = new Bounds(new Vector3((center.x + max.x) * 0.5f, center.y, (center.z + max.z) * 0.5f), halfSize);
            Bounds botRight = new Bounds(new Vector3((center.x + max.x) * 0.5f, center.y, (min.z + center.z) * 0.5f), halfSize);

            Debug.Log($"[ProceduralObjects] Initializing new Quads, bottom left: {{{botLeft.min}, {botLeft.max}, {botLeft.center}, {botLeft.size}}};" +
                $"top right: {{{topRight.min}, {topRight.max}, {topRight.center}, {topRight.size}}}");

            List<int> botLeftObjects = new List<int>();
            List<int> topLeftObjects = new List<int>();
            List<int> topRightObjects = new List<int>();
            List<int> botRightObjects = new List<int>();

            foreach (int poSeq in proceduralObjectIndices)
            {
                Vector3 poCoord = instance.proceduralObjects[poSeq].m_position;
                if (botLeft.Contains(poCoord)) { botLeftObjects.Add(poSeq); }
                else if (botRight.Contains(poCoord)) { botRightObjects.Add(poSeq); }
                else if (topRight.Contains(poCoord)) { topRightObjects.Add(poSeq); }
                else if (topLeft.Contains(poCoord)) { topLeftObjects.Add(poSeq); }
            }

            children[0] = new Quad(_level + 1, _maxLevel, this, topRight, topRightObjects);
            children[1] = new Quad(_level + 1, _maxLevel, this, topLeft, topLeftObjects);
            children[2] = new Quad(_level + 1, _maxLevel, this, botLeft, botLeftObjects);
            children[3] = new Quad(_level + 1, _maxLevel, this, botRight, botRightObjects);

            return children;
        }

        public List<Quad> GetLeaves()
        {
            List<Quad> result = new List<Quad>();
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                    result.AddRange(children[i].GetLeaves());
            }
            else
            {
                return new List<Quad>() { this };
            }
            return result;
        }

        // For debug use to visualize quad tree blocks
        public void DrawQuadBounds()
        {
            if (children == null)
            {
                if (lineObject == null)
                {
                    CreateBoundsAndLines();
                }
                if (lineObject != null && !lineObject.activeSelf)
                {
                    lineObject.SetActive(true);
                }
            }
            else
            {
                for (int i = 0; i < children.Length; i++)
                    children[i].DrawQuadBounds();
            }
        }

        public string GetObjHashString(ProceduralObject obj, SHA1 sha1)
        {
            Vector3[] vertices = obj.m_mesh.vertices;
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, SerializableVector3.ToSerializableArray(vertices));
            stream.Position = 0;
            byte[] meshHashBytes = sha1.ComputeHash(stream);
            stream.Close();

            StringBuilder sb = new StringBuilder(obj._baseProp.name);
            sb.Append("_");
            for (int i = 0; i < meshHashBytes.Length; i++)
                sb.Append(meshHashBytes[i].ToString("X2"));
            sb.Append("_");

            if (obj.m_textParameters != null)
            {
                stream = new MemoryStream();
                Serializer.Serialize(stream, obj.m_textParameters);
                stream.Position = 0;
                byte[] textParamHashBytes = sha1.ComputeHash(stream);
                stream.Close();
                for (int i = 0; i < textParamHashBytes.Length; i++)
                    sb.Append(textParamHashBytes[i].ToString("X2"));
            }
            else
                sb.Append("NO_TEXTPARAMETERS");

            return sb.ToString();
        }

        public void CreateBoundsAndLines()
        {
            if (children == null)
            {
                if (lineObject == null)
                {
                    if (outlineMat == null)
                    {
                        outlineMat = new Material(Shader.Find("GUI/Text Shader"))
                        {
                            color = new Color(1, 1, 1, .3f)
                        };
                    }
                    lineObject = new GameObject("POQuadCubicOutline");
                    lineRenderer = lineObject.AddComponent<LineRenderer>();
                    lineRenderer.material = outlineMat;
                    lineRenderer.startWidth = 4f;
                    lineRenderer.endWidth = 4f;
                }
                if (lineObject == null)
                {
                    Debug.Log($"[ProceduralObjects] Failed to initialize lineObject. lineObject of Quad {bounds.center} is null!");
                    return;
                }
                corners = new Vector3[8];
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                corners[0] = new Vector3(min.x, min.y, min.z); // bottom-front-left
                corners[1] = new Vector3(max.x, min.y, min.z); // bottom-front-right
                corners[2] = new Vector3(min.x, min.y, max.z); // bottom-back-left
                corners[3] = new Vector3(max.x, min.y, max.z); // bottom-back-right
                corners[4] = new Vector3(min.x, max.y, min.z); // top-front-left
                corners[5] = new Vector3(max.x, max.y, min.z); // top-front-right
                corners[6] = new Vector3(min.x, max.y, max.z); // top-back-left
                corners[7] = new Vector3(max.x, max.y, max.z); // top-back-right
                line = new Vector3[16]{corners[0], corners[1], corners[3], corners[2], corners[0], corners[4],
                    corners[6], corners[7], corners[5], corners[1], corners[3], corners[7], corners[5], corners[4],
                    corners[6], corners[2]};
                lineRenderer.positionCount = 16;
                lineRenderer.SetPositions(line);
                lineObject.SetActive(false);
            }
        }

        public void HandleObjectDirtyMeshProperties(int seqNo)
        {
            ProceduralObject obj = IsPoInQuadAndValid(seqNo);
            if (null == obj)
                return;
            if (bounds.Contains(obj.m_position))
            {
                List<MeshProperties> list = ResolveMeshPropertiesListByPoSeq(seqNo);
                if (list == null)  // which means the object is not batchable, i.e. tagged with ObjectStatus.Unbatchable
                {
                    return;
                }
                list[poSeqBatchHandleDict[seqNo].indexInList] = new MeshProperties(obj);

            }
            else
            {
                string poMeshIdentifierString = poSeqBatchHandleDict[seqNo].identifierString;
                RemoveFromQuad(seqNo);
                Quad newQuadToJoin = LocateQuadLeafFromPosition(obj.m_position);
                newQuadToJoin.TakeInNewPo(seqNo, poMeshIdentifierString);
                obj.ownerQuad = newQuadToJoin;
            }
        }

        /// <summary>
        /// The method to handle Procedural Objects with dirty mesh. Currently it only places object into an existing batch.
        /// </summary>
        public void HandleObjectDirtyMesh(int seqNo)
        {
            ProceduralObject obj = IsPoInQuadAndValid(seqNo);
            if (null == obj) 
                return;
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            if (handle.objectStatus == ObjectStatus.Unbatchable)
            {
                return;
            }

            List<int> objPrevBatchPoSeqList = ResolveIntListByPoSeq(seqNo);
            List<MeshProperties> objPrevBatchMeshPropertiesList = ResolveMeshPropertiesListByPoSeq(seqNo);

            // Move object to unbatchable list if they were batched but has become unbatchable.
            // TODO: There are more conditions that should be taken into consideration but didn't find the
            // corresponding field names or the default values in ProceduralObject instances, for example, the RecalculateNormal flags. 
            if (handle.objectStatus != ObjectStatus.Unbatchable && 
                (obj.baseInfoType == "BUILDING" || obj.customTexture != null || !obj.m_material.shader.name.Equals(batchedPropShaderStr)))
            {
                //poSeqBatchHandleDict.Add(seqNo, new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatchable));
                handle.identifierString = null;
                handle.indexInList = unbatchableList.Count;
                handle.objectStatus = ObjectStatus.Unbatchable;
                unbatchableList.Add(seqNo);
                if (objPrevBatchPoSeqList != null)
                {
                    objPrevBatchMeshPropertiesList.RemoveAtSwapBack(handle.indexInList);
                }
                if (objPrevBatchMeshPropertiesList != null)
                {
                    objPrevBatchPoSeqList.RemoveAtSwapBack(handle.indexInList);
                }
            }
            string propPrefabName = obj.basePrefabName;
            Mesh basePropMesh = PropInfoHelper.GetPropInfo(propPrefabName).m_mesh;
            // Process situation when obj.meshStatus become 1
            if (obj.meshStatus == 1)
            {
                if (basePropMesh != null && obj.m_mesh != null)
                {
                    bool isMeshSame = CheckMeshEquivalance(basePropMesh.vertices, obj.m_mesh.vertices);

                    if (isMeshSame && batchOriginalPoIdDict.ContainsKey(propPrefabName) && objPrevBatchPoSeqList != batchOriginalPoIdDict[propPrefabName])
                    {
                        // Remove from previous list if they are not null and not the same with the original one.
                        objPrevBatchPoSeqList?.RemoveAtSwapBack(handle.indexInList);
                        objPrevBatchMeshPropertiesList?.RemoveAtSwapBack(handle.indexInList);

                        // Update batchHandle and add to new list.
                        handle.identifierString = propPrefabName;
                        handle.indexInList = batchOriginalPoIdDict[propPrefabName].Count();
                        handle.objectStatus = ObjectStatus.Original;

                        batchOriginalPoIdDict[propPrefabName].Add(seqNo);
                        batchOriginalArrayDict[propPrefabName].Add(new MeshProperties(obj));

                        return;
                    }
                    else if (!isMeshSame && handle.objectStatus != ObjectStatus.Unbatchable)
                    {

                    }
                }
            }
            else if (obj.meshStatus == 2)
            {
                SHA1 sha1 = HelperPool.GetSHA1Instance();
                string objHashStr = GetObjHashString(obj, sha1);



                HelperPool.ReturnSHA1Instance(sha1);
            }
        }

        public List<MeshProperties> ResolveMeshPropertiesListByPoSeq(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            if (handle.objectStatus == ObjectStatus.Unbatchable)
            {
                return null;
            }
            else if (handle.objectStatus == ObjectStatus.Original)
            {
                return batchOriginalArrayDict[handle.identifierString];
            }
            else
            {
                return batchCustomArrayDict[handle.identifierString];
            }
        }

        public List<int> ResolveIntListByPoSeq(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            if (handle.objectStatus == ObjectStatus.Unbatchable)
            {
                return null;
            }
            else if (handle.objectStatus == ObjectStatus.Original)
            {
                return batchOriginalPoIdDict[handle.identifierString];
            }
            else
            {
                return batchCustomPoIdDict[handle.identifierString];
            }
        }

        private void RemoveFromQuad(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            allPoSeqSet.Remove(seqNo);
            int seqNoIndexInAllPoSeqList = allPoSeqList.IndexOf(seqNo);
            if (seqNoIndexInAllPoSeqList >= 0)
            {
                allPoSeqList.RemoveAtSwapBack(seqNoIndexInAllPoSeqList);
            }

            List<MeshProperties> batchMeshPropertiesList = ResolveMeshPropertiesListByPoSeq(seqNo);
            if (batchMeshPropertiesList == null)  // which means the object is not batchable, i.e. tagged with ObjectStatus.Unbatchable
            {
                return;
            }
            List<int> batchPoSeqList = ResolveIntListByPoSeq(seqNo);
            batchMeshPropertiesList.RemoveAtSwapBack(handle.indexInList);
            batchPoSeqList.RemoveAtSwapBack(handle.indexInList);
            poSeqBatchHandleDict.Remove(seqNo);
        }

        public void TakeInNewPo(int seqNo, string identifierString)
        {
            allPoSeqList.Add(seqNo);
            allPoSeqSet.Add(seqNo);
            ProceduralObject obj = ProceduralObjectsLogic.instance.proceduralObjects[seqNo];
            if (identifierString != null && batchOriginalPoIdDict.ContainsKey(identifierString))
            {
                poSeqBatchHandleDict.Add(seqNo, new BatchHandle(identifierString, batchOriginalPoIdDict[identifierString].Count, ObjectStatus.Original));
                batchOriginalPoIdDict[identifierString].Add(seqNo);
                batchOriginalArrayDict[identifierString].Add(new MeshProperties(obj));
            }
            else if (identifierString != null && batchCustomPoIdDict.ContainsKey(identifierString))
            {
                poSeqBatchHandleDict.Add(seqNo, new BatchHandle(identifierString, batchCustomPoIdDict[identifierString].Count, ObjectStatus.CustomBatchable));
                batchCustomPoIdDict[identifierString].Add(seqNo);
                batchCustomArrayDict[identifierString].Add(new MeshProperties(obj));
            }
            else
            {
                poSeqBatchHandleDict.Add(seqNo, new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatchable));
                unbatchableList.Add(seqNo);
            }
        }

        private Quad LocateQuadLeafFromPosition(Vector3 position)
        {
            Quad quad = this;
            while (!quad.bounds.Contains(position) && quad.parent != null)
            {
                quad = quad.parent;
            }
            if (!quad.bounds.Contains(position) && quad.parent == null)
            {
                return null;
            }
            else
            {
                while (quad.children != null)
                {
                    foreach (Quad child in quad.children)
                    {
                        if (child.bounds.Contains(position))
                        {
                            quad = child;
                            break;
                        }
                    }
                }
            }
            return quad;
        }

        private ProceduralObject IsPoInQuadAndValid(int seqNo)
        {
            if (!allPoSeqSet.Contains(seqNo) || seqNo < 0 || seqNo >= ProceduralObjectsLogic.instance.proceduralObjects.Count)
            {
                Debug.Log(string.Format("[ProceduralObjects] Quad {0} doesn't hold Procedural Object with Seq No {1}", bounds.center, seqNo));
                return null;
            }
            return ProceduralObjectsLogic.instance.proceduralObjects[seqNo];
        }
    }


    internal struct BatchHandle
    {
        public string identifierString;
        public int indexInList;
        public ObjectStatus objectStatus;

        public BatchHandle(string inHashString, int inIndex, ObjectStatus inStatus)
        {
            identifierString = inHashString;
            indexInList = inIndex;
            objectStatus = inStatus;
        }
    }

    internal enum ObjectStatus
    {
        Original, CustomBatchable, Unbatchable
    }
}
