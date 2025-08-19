using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static ProceduralObjects.ProceduralObjectsLogic;
using static ProceduralObjects.Classes.ProceduralUtils;
using static ProceduralObjects.ProceduralText.TextParameters;
using System.Linq;

namespace ProceduralObjects.Classes
{
    public class Quad
    {
        public Quad parent;
        public Quad[] children;
        public readonly Bounds bounds;

        public List<int> allPoIdList;
        public Dictionary<string, List<int>> batchCustomPoIdDict;
        public Dictionary<string, List<int>> batchOriginalPoIdDict;
        public Dictionary<string, List<MeshProperties>> batchCustomArrayDict;
        public Dictionary<string, List<MeshProperties>> batchOriginalArrayDict;
        public List<int> unbatchableList;
        private int _level;
        private int _maxLevel;

        private int _maxPoCount = 4096;
        private int _repeateModifiedMeshCount = 0;
        private int _unmodifiedMeshCount = 0;

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
            unbatchableList = new List<int>();
            allPoIdList = new List<int>();
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
                allPoIdList = proceduralObjectIndexSeqs;
                List<ProceduralObject> proceduralObjects = ProceduralObjectsLogic.instance.proceduralObjects;
                using (SHA1 sha1 = SHA1.Create())
                {
                    foreach (int poSeq in proceduralObjectIndexSeqs)
                    {
                        var obj = proceduralObjects[poSeq];
                        if (obj.meshStatus == 1 && obj.baseInfoType == "PROP" && obj.customTexture == null && obj.m_textParameters == null)
                        {
                            if (!batchOriginalPoIdDict.ContainsKey(obj._baseProp.name))
                                batchOriginalPoIdDict[obj._baseProp.name] = new List<int>();
                            if (!batchOriginalArrayDict.ContainsKey(obj._baseProp.name))
                                batchOriginalArrayDict[obj._baseProp.name] = new List<MeshProperties>();

                            batchOriginalArrayDict[obj._baseProp.name].Add(
                                new MeshProperties(Matrix4x4.TRS(obj.m_position, obj.m_rotation, Vector3.one),
                                obj.disableCastShadows ? new Vector4(1, 0, 0, 0) : new Vector4(0, 0, 0, 0),
                                obj.m_color)
                                );

                            batchOriginalPoIdDict[obj._baseProp.name].Add(poSeq);
                            _unmodifiedMeshCount++;
                            continue;
                        }
                        else if (obj.baseInfoType == "BUILDING" || obj.customTexture != null)
                        {
                            unbatchableList.Add(poSeq);
                            continue;
                        }
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

                        //string meshHash = sb.ToString();
                        //string propName = obj._baseProp.name;
                        string objHashStr = sb.ToString();
                        if (!batchCustomArrayDict.ContainsKey(objHashStr))
                        {
                            batchCustomArrayDict[objHashStr] = new List<MeshProperties>
                        {
                            new MeshProperties(Matrix4x4.TRS(obj.m_position, obj.m_rotation, Vector3.one),
                                obj.disableCastShadows ? new Vector4(1, 0, 0, 0) : new Vector4(0, 0, 0, 0),
                                obj.m_color)
                        };
                        }
                        if (!batchCustomPoIdDict.ContainsKey(objHashStr))
                        {
                            batchCustomPoIdDict[objHashStr] = new List<int> { poSeq };
                        }
                        else
                        {
                            int listHead = batchCustomPoIdDict[objHashStr][0];

                            if ((obj.m_textParameters == null && proceduralObjects[listHead].m_textParameters == null) ||
                                (!IsDifference(obj.m_textParameters, proceduralObjects[listHead].m_textParameters)) &&
                                CheckMeshEquivalance(obj.m_mesh.vertices, proceduralObjects[listHead].m_mesh.vertices))
                            {
                                batchCustomArrayDict[objHashStr].Add(
                                    new MeshProperties(Matrix4x4.TRS(obj.m_position, obj.m_rotation, Vector3.one),
                                    obj.disableCastShadows ? new Vector4(1, 0, 0, 0) : new Vector4(0, 0, 0, 0),
                                    obj.m_color)
                                    );
                                batchCustomPoIdDict[objHashStr].Add(poSeq);
                            }
                            else
                                unbatchableList.Add(poSeq);
                        }
                    }
                }

                foreach (var kv in batchCustomArrayDict)
                {
                    if (kv.Value.Count > 1)
                    {
                        _repeateModifiedMeshCount += kv.Value.Count;
                        Debug.Log($"[ProceduralObjects] Quad {bounds.center} loaded repeated meshStatus 2 meshes, meshId: {kv.Key}, count: {kv.Value.Count}");
                    }
                }

                Debug.Log($"[ProceduralObjects] In Quad {bounds.center}, total unmodified meshStatus 1 meshes count: {_unmodifiedMeshCount}, " +
                    $"total repeated meshStatus 2 meshes count: {_repeateModifiedMeshCount}");

            }
            
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

            children[0] = new Quad(_level + 1, _maxLevel, this, botLeft, botLeftObjects);
            children[1] = new Quad(_level + 1, _maxLevel, this, topLeft, topLeftObjects);
            children[2] = new Quad(_level + 1, _maxLevel, this, topRight, topRightObjects);
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
                if (lineRenderer == null)
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
                if (lineRenderer == null)
                {
                    Debug.Log($"[ProceduralObjects] LineRenderer of Quad {bounds.center} is null!");
                    return;
                }
                Vector3[] corners = new Vector3[8];
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
                Vector3[] line = new Vector3[16] { corners[0], corners[1], corners[3], corners[2], corners[0], corners[4],
                    corners[6], corners[7], corners[5], corners[1], corners[3], corners[7], corners[5], corners[4], 
                    corners[6], corners[2]};
                lineRenderer.positionCount = 16;
                lineRenderer.SetPositions(line);
            }
            else
            {
                for (int i = 0; i < children.Length; i++)
                    children[i].DrawQuadBounds();
            }
        }
    }
}
