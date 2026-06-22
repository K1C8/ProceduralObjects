using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static ProceduralObjects.Classes.ProceduralUtils;
using static ProceduralObjects.ProceduralObjectsLogic;
using static ProceduralObjects.ProceduralText.TextParameters;

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
        // -- Option A: Original Batchable. Disabled temporarily from '26/01/13
        //public Dictionary<string, List<int>> batchOriginalPoIdDict;
        //public Dictionary<string, List<MeshProperties>> batchOriginalArrayDict;
        // -- Option B: Custom Batchable
        public Dictionary<string, List<int>> batchCustomPoIdDict;
        public Dictionary<string, List<MeshProperties>> batchCustomArrayDict;
        // -- Option C: Unbatchable
        public List<int> unbatchableList;

        private readonly int _level;
        private readonly int _maxLevel;

        private int _maxPoCount = 4096;
        private int _batchedPropMeshCount = 0;
        //private int _unmodifiedMeshCount = 0;
        private int _minimumBatchSize = 3;
        private Vector3[] corners;
        private Vector3[] line;
        private string defaultPropShaderStr = "Custom/Props/Prop/Default";
        private string defaultDecalShaderStr = "Custom/Props/Decal/Blend";
        private string batchedPropShaderStr = "Custom/ProceduralObject/Prop/testshaderind";
        private string batchedDecalShaderStr = "Custom/ProceduralObject/Prop/testdecalindshader";

        // Debug line boxes
        GameObject lineObject;
        LineRenderer lineRenderer;
        private Material outlineMat;

        public Quad(int level, int maxLevel, Quad p, Bounds bounds, List<int> proceduralObjectIndexSeqs)
        {
            _level = level;
            _maxLevel = maxLevel;
            batchCustomArrayDict = new Dictionary<string, List<MeshProperties>>();
            //batchOriginalArrayDict = new Dictionary<string, List<MeshProperties>>();
            batchCustomPoIdDict = new Dictionary<string, List<int>>();
            //batchOriginalPoIdDict = new Dictionary<string, List<int>>();
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
                    foreach (int seqNo in allPoSeqList)
                    {
                        var obj = proceduralObjects[seqNo];
                        obj.ownerQuad = this;
                        string objHashStr;
                        if (!IsPoAbleToBeBatched(obj))
                        {
                            poSeqBatchHandleDict.Add(seqNo, new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatched));
                            unbatchableList.Add(seqNo);
                            continue;
                        }

                        objHashStr = GetObjHashString(obj, sha1);

                        // Add as the head of a batch OR as a new non-head instance of the batch, choose only one
                        if (!batchCustomArrayDict.ContainsKey(objHashStr) && !batchCustomPoIdDict.ContainsKey(objHashStr))
                        {
                            poSeqBatchHandleDict.Add(seqNo, new BatchHandle(objHashStr, 0, ObjectStatus.Batched));
                            batchCustomPoIdDict[objHashStr] = new List<int> { seqNo };
                            batchCustomArrayDict[objHashStr] = new List<MeshProperties> { new MeshProperties(obj) };
                        }
                        else
                        {
                            int listHead = batchCustomPoIdDict[objHashStr][0];

                            if (((obj.m_textParameters == null && proceduralObjects[listHead].m_textParameters == null) ||
                                (!IsDifference(obj.m_textParameters, proceduralObjects[listHead].m_textParameters))) &&
                                CheckMeshEquivalance(obj.m_mesh.vertices, proceduralObjects[listHead].m_mesh.vertices))
                            {
                                poSeqBatchHandleDict.Add(seqNo, new BatchHandle(objHashStr, batchCustomPoIdDict[objHashStr].Count, ObjectStatus.Batched));
                                batchCustomArrayDict[objHashStr].Add(new MeshProperties(obj));
                                batchCustomPoIdDict[objHashStr].Add(seqNo);
                            }
                            else
                            {
                                poSeqBatchHandleDict.Add(seqNo, new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatched));
                                unbatchableList.Add(seqNo);
                            }
                            //poSeqBatchHandleDict.Add(seqNo, AddToNewBatchingList(seqNo, objHashStr, null));
                        }
                    }
                }

                List<string> keysToRemove = new List<string>();
                foreach (var kv in batchCustomPoIdDict)
                {
                    if (kv.Value.Count >= _minimumBatchSize)
                    {
                        _batchedPropMeshCount += kv.Value.Count;
                        Debug.Log($"[ProceduralObjects] Quad {bounds.center} loaded batched meshes, meshId: {kv.Key}, count: {kv.Value.Count}");
                    }
                    else
                    {
                        Debug.Log($"[ProceduralObjects] Quad {bounds.center} loaded repeated but insufficient meshes, meshId: {kv.Key}, count: {kv.Value.Count}");
                        for (int i = 0; i < kv.Value.Count; i++)
                        {
                            int poSeq = kv.Value[i];
                            poSeqBatchHandleDict[poSeq] = new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatched);
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

                if (_batchedPropMeshCount > 0)
                {
                    Debug.Log($"[ProceduralObjects] In Quad {bounds.center}, total batchable meshes counting at: {_batchedPropMeshCount}.");
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

        public List<Quad> GetLeaves(ref List<Quad> inList)
        {
            //List<Quad> result = new List<Quad>();
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                    children[i].GetLeaves(ref inList);
            }
            else
            {
                inList.Add(this);
                return inList;
            }
            return inList;
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

        public void HideQuadBounds()
        {
            if (children == null)
            {
                if (lineObject != null && lineObject.activeSelf)
                {
                    lineObject.SetActive(false);
                }
            }
            else
            {
                for (int i = 0; i < children.Length; i++)
                    children[i].HideQuadBounds();
            }
        }

        public string GetObjHashString(ProceduralObject obj, SHA1 sha1)
        {
            StringBuilder sb = new StringBuilder(obj.basePrefabName);
            sb.Append("_");

            // NOTE: If later it needs to rollback to have meshStatus == 1 objects skipping calculating SHA1 hashes, just modify here.
            // By skipping serializing and hashing the mesh vertices and place a placeholder "NO_MODIFICATION" should be enough.
            Vector3[] vertices = obj.m_mesh.vertices;
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, SerializableVector3.ToSerializableArray(vertices));
            stream.Position = 0;
            byte[] meshHashBytes = sha1.ComputeHash(stream);
            stream.Close();
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
                RemoveObjectFromQuad(seqNo);
                Quad newQuadToJoin = LocateQuadLeafFromPosition(obj.m_position);
                if (newQuadToJoin == null)
                {
                    Debug.Log(string.Format("[ProceduralObjects] Failed to locate new quad for obj {0} now located at {1}", seqNo, obj.m_position));
                    return;
                }
                newQuadToJoin.TakeInNewPo(seqNo, poMeshIdentifierString);
            }
        }

        /// <summary>
        /// The method to handle Procedural Objects with dirty mesh and material. Currently it only places object into an existing batch or unbatch the object.
        /// </summary>
        public void HandleObjectDirtyMeshAndMaterial(int seqNo)
        {
            ProceduralObject obj = IsPoInQuadAndValid(seqNo);
            if (null == obj) 
                return;
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            // Exclude objects that are not batched and are not able to be batched
            if (handle.objectStatus == ObjectStatus.Unbatched && !IsPoAbleToBeBatched(obj))
            {
                return;
            }
            List<int> objPrevBatchPoSeqList = ResolveIntListByPoSeq(seqNo);
            List<MeshProperties> objPrevBatchMeshPropertiesList = ResolveMeshPropertiesListByPoSeq(seqNo);

            // Move object to unbatchable list if they were batched but has become unbatchable. Then put the object into the queue for checking its shader
            if (handle.objectStatus == ObjectStatus.Batched && !IsPoAbleToBeBatched(obj))
            {
                RemoveFromPreviousBatchingList(seqNo, handle);

                poSeqBatchHandleDict[seqNo] = AddToNewBatchingList(seqNo, null, handle);

                return;
            }

            // The remaining situations include unbatchable objects become batchable, and batchable objects still being batchable
            // Branching condition: only if a current batch with the same objHashStr exists should the object be batched.
            // Otherwise all objects that are batchable but no matching current batch found should be handled as unbatched in game.
            // They will be batched after next load cycle if they really are.
            // For those batched but become unbatched, and those unbatched but batched after the dirty mesh, send them to the shader checking queue.
            SHA1 sha1 = HelperPool.GetSHA1Instance();
            string objHashStr = GetObjHashString(obj, sha1);

            // Remove from old batch or unbatched list
            RemoveFromPreviousBatchingList(seqNo, handle);

            // If existing matching batch found.
            if (batchCustomPoIdDict.ContainsKey(objHashStr) && batchCustomArrayDict.ContainsKey(objHashStr))
            {
                // Move to the new batch
                poSeqBatchHandleDict[seqNo] = AddToNewBatchingList(seqNo, objHashStr, handle);

            }
            // No existing matching batch found.
            else
            {
                poSeqBatchHandleDict[seqNo] = AddToNewBatchingList(seqNo, null, handle);

            }

            HelperPool.ReturnSHA1Instance(sha1);
        }

        public List<MeshProperties> ResolveMeshPropertiesListByPoSeq(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            if (handle.objectStatus == ObjectStatus.Unbatched)
            {
                return null;
            }
            else
            {
                return batchCustomArrayDict[handle.identifierString];
            }
        }

        public List<int> ResolveIntListByPoSeq(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            if (handle.objectStatus == ObjectStatus.Unbatched)
            {
                return null;
            }
            else
            {
                return batchCustomPoIdDict[handle.identifierString];
            }
        }

        public void RemoveObjectFromQuad(int seqNo)
        {
            BatchHandle handle = poSeqBatchHandleDict[seqNo];
            allPoSeqSet.Remove(seqNo);

            RemoveFromPreviousBatchingList(seqNo, handle);
            poSeqBatchHandleDict.Remove(seqNo);
            int seqNoIndexInAllPoSeqList = allPoSeqList.IndexOf(seqNo);
            if (seqNoIndexInAllPoSeqList >= 0)
            {
                allPoSeqList.RemoveAtSwapBack(seqNoIndexInAllPoSeqList);
            }

        }

        public void TakeInNewPo(int seqNo, string objHashStr)
        {
            allPoSeqList.Add(seqNo);
            allPoSeqSet.Add(seqNo);
            ProceduralObject obj = ProceduralObjectsLogic.instance.proceduralObjects[seqNo];
            poSeqBatchHandleDict.Add(seqNo, AddToNewBatchingList(seqNo, objHashStr, null));
            obj.ownerQuad = this;
        }

        public bool TakeInPoAfterCreated(int seqNo)
        {
            ProceduralObject obj = ProceduralObjectsLogic.instance.proceduralObjects[seqNo];
            Quad newQuadToJoin = LocateQuadLeafFromPosition(obj.m_position);
            if (newQuadToJoin == null)
            {
                Debug.Log(string.Format("[ProceduralObjects] Failed to locate new quad for obj {0} now located at {1}", seqNo, obj.m_position));
                return false;
            }
            newQuadToJoin.TakeInNewPo(seqNo, GetObjHashString(obj, HelperPool.GetSHA1Instance()));
            return true;
        }

        private Quad LocateQuadLeafFromPosition(Vector3 position)
        {
            Quad quad = this;
            while (!quad.bounds.Contains(position) && quad.parent != null)
            {
                quad = quad.parent;
                Debug.Log(string.Format("[ProceduralObjects] Escalating request of LocateQuadLeafFromPosition(Vector3 {0}) to parent quad {1}.", position, quad.bounds.center));
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
                            Debug.Log(string.Format("[ProceduralObjects] Refining request of LocateQuadLeafFromPosition(Vector3 {0}) to child quad {1}.", position, quad.bounds.center));
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

        private bool IsPoAbleToBeBatched(ProceduralObject obj)
        {
            // TODO: Consider adding checks for environment compatibility. For example, if the host GPU
            // cannot handle DrawMeshInstancedIndirect, that should make ALL POs unbatchable.
            // TODO: There are more conditions that should be taken into consideration but didn't find the
            // corresponding field names or the default values in ProceduralObject instances, for example, the RecalculateNormal flags. 
            if (!SystemInfo.supportsInstancing)
            {
                return false;
            }
            // except default props and blend decals with no custom textures, all other props cannot be batched
            if (obj.baseInfoType == "BUILDING" || obj.customTexture != null || 
                (!obj.m_material.shader.name.Equals(defaultPropShaderStr) && !obj.m_material.shader.name.Equals(batchedPropShaderStr) &&
                !obj.m_material.shader.name.Equals(defaultDecalShaderStr) && !obj.m_material.shader.name.Equals(batchedDecalShaderStr)))
                return false;
            return true;
        }

        // Need to handle BatchHandles that swapped forward and have their indexInList updated to the new index in the unbatchable/batched lists.
        private void RemoveFromPreviousBatchingList(int seqNo, BatchHandle handle)
        {
            int indexToRemove = handle.indexInList;
            if (handle.identifierString == null && handle.objectStatus == ObjectStatus.Unbatched)
            {
                // If the object to remove from list is not the last item, then it means another item (the previous last item in list) is required to be swapped forward
                // to have the RemoveAtSwapBack to work.
                if (indexToRemove < unbatchableList.Count - 1 && 0 <= indexToRemove)
                {
                    int seqNoLastItemUnbatchable = unbatchableList[unbatchableList.Count - 1];
                    BatchHandle handleLastItem = poSeqBatchHandleDict[seqNoLastItemUnbatchable];
                    handleLastItem.indexInList = indexToRemove;
                }
                unbatchableList.RemoveAtSwapBack(handle.indexInList);
            }
            else
            {
                List<int> objPrevBatchPoSeqList = ResolveIntListByPoSeq(seqNo);
                List<MeshProperties> objPrevBatchMeshPropertiesList = ResolveMeshPropertiesListByPoSeq(seqNo);
                // If the object to remove from list is not the last item, then it means another item (the previous last item in list) is required to be swapped forward
                // to have the RemoveAtSwapBack to work.
                if (indexToRemove < objPrevBatchPoSeqList.Count - 1 && 0 <= indexToRemove)
                {
                    int seqNoLastItemBatched = objPrevBatchPoSeqList[objPrevBatchPoSeqList.Count - 1];
                    BatchHandle handleLastItem = poSeqBatchHandleDict[seqNoLastItemBatched];
                    handleLastItem.indexInList = indexToRemove;
                    //Debug.Log(string.Format("[ProceduralObjects] RemoveFromPreviousBatchingList() updated original last item index to {0}, index before update {1}.",
                    //    poSeqBatchHandleDict[seqNoLastItemBatched].indexInList, objPrevBatchPoSeqList.Count - 1));
                }
                objPrevBatchPoSeqList?.RemoveAtSwapBack(handle.indexInList);
                objPrevBatchMeshPropertiesList?.RemoveAtSwapBack(handle.indexInList);
            }
        }

        private BatchHandle AddToNewBatchingList(int seqNo, string objHashStr, BatchHandle handle)
        {
            if (handle == null)
                handle = new BatchHandle(null, unbatchableList.Count, ObjectStatus.Unbatched);
            ProceduralObject obj = ProceduralObjectsLogic.instance.proceduralObjects[seqNo];
            if (obj == null)
            {
                Debug.Log(string.Format("[ProceduralObjects] ProceduralObjectsLogic.instance.proceduralObjects[{0}] is null!", seqNo));
                return handle;
            }
            ShaderConversionController conversionController = ProceduralObjectsLogic.instance.shaderConversionController;
            if (objHashStr != null && batchCustomPoIdDict.ContainsKey(objHashStr) && 
                batchCustomArrayDict.ContainsKey(objHashStr) && IsPoAbleToBeBatched(obj))
            {
                ProceduralObject listHead = ProceduralObjectsLogic.instance.proceduralObjects[batchCustomPoIdDict[objHashStr][0]];
                if (((obj.m_textParameters == null && listHead.m_textParameters == null) ||
                    (!IsDifference(obj.m_textParameters, listHead.m_textParameters))) &&
                    CheckMeshEquivalance(obj.m_mesh.vertices, listHead.m_mesh.vertices))
                {
                    // Move to the new batch
                    handle.identifierString = objHashStr;
                    handle.indexInList = batchCustomPoIdDict[objHashStr].Count;
                    handle.objectStatus = ObjectStatus.Batched;
                    batchCustomPoIdDict[objHashStr].Add(seqNo);
                    batchCustomArrayDict[objHashStr].Add(new MeshProperties(obj));
                    // TODO: Add the object to the shader checking queue, change the shader to batched shader.
                    if (conversionController != null)
                    {
                        conversionController.ConvertToInstancedShader(obj);
                    }

                    return handle;
                }
            }
            // No existing matching batch found, or hash match found but indeed different with listHead.
            handle.identifierString = null;
            handle.indexInList = unbatchableList.Count;
            handle.objectStatus = ObjectStatus.Unbatched;
            unbatchableList.Add(seqNo);
            // TODO: Add the object to the shader checking queue, change the shader to the original shader.
            if (conversionController != null)
            {
                conversionController.ConvertToOriginalShader(obj);
            }

            return handle;
        }
    }


    internal class BatchHandle
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
        Batched, Unbatched
    }
}
