﻿using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

using ProceduralObjects.Classes;
using ProceduralObjects.ProceduralText;

namespace ProceduralObjects
{
    public class ProceduralObjectsSerializer : SerializableDataExtensionBase
    {
        private readonly string dataKey = "ProceduralObjectsDataKey", layerKey = "ProceduralObjectsLayerData";

        public override void OnSaveData()
        {
            base.OnSaveData();
            Debug.Log("[ProceduralObjects] Data saving started.");
            MemoryStream proceduralObjStream = new MemoryStream(), layerStream = new MemoryStream();
            if (ProceduralObjectsMod.gameLogicObject == null)
                return;
            ProceduralObjectsLogic logic = ProceduralObjectsMod.gameLogicObject.GetComponent<ProceduralObjectsLogic>();
            if (logic == null)
                return;
            BinaryFormatter bFormatter = new BinaryFormatter();
            ProceduralObjectContainer[] dataContainer = logic.GetContainerList();
            Layer[] layerContainer = logic.layerManager.m_layers.ToArray();
            try
            {
                if (dataContainer != null)
                {
                    bFormatter.Serialize(proceduralObjStream, dataContainer);
                    var splittedDict = SplitArray(proceduralObjStream.ToArray());
                    foreach (string key in serializableDataManager.EnumerateData())
                    {
                        if (key.Contains(dataKey) && !splittedDict.ContainsKey(key))
                        {
                            serializableDataManager.EraseData(key);
                            Debug.Log("[ProceduralObjects] Erased data array " + key + " because it wasn't used anymore");
                        }
                    }
                    Debug.Log("[ProceduralObjects] Data saving : saving " + splittedDict.Count.ToString() + " splited data array(s).");
                    foreach (KeyValuePair<string, byte[]> kvp in splittedDict)
                    {
                        serializableDataManager.SaveData(kvp.Key, kvp.Value);
                    }
                    Debug.Log("[ProceduralObjects] Data was serialized and saved. Saved " + dataContainer.Count() + " procedural objects.");
                }
                if (layerContainer != null)
                {
                    bFormatter.Serialize(layerStream, layerContainer);
                    serializableDataManager.SaveData(layerKey, layerStream.ToArray());
                } 
                // logic.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError("[ProceduralObjects] Data failed to save completely due to " + e.GetType().ToString() + " : \"" + e.Message + "\"");
            }
            finally
            {
                proceduralObjStream.Close();
                layerStream.Close();
                Debug.Log("[ProceduralObjects] Data saving ended.");
            }
        }
        public override void OnLoadData()
        {
            /*  Debug.Log("[ProceduralObjects] Data loading started.");
                var keys = serializableDataManager.EnumerateData();
                string s = " data keys :";
                foreach (string str in keys)
                    s += " " + str;
                Debug.Log(s); */
            var keys = serializableDataManager.EnumerateData().Where(key => key.Contains(dataKey)).Count();
            List<byte[]> arrays = new List<byte[]>();
            for (int i = 0; i < keys; i++)
            {
                if (i == 0)
                    arrays.Add(serializableDataManager.LoadData(dataKey));
                else
                    arrays.Add(serializableDataManager.LoadData(dataKey + i.ToString()));
            }
            Debug.Log("[ProceduralObjects] Data loading : found " + arrays.Count.ToString() + " splited data arrays.");
            var startTime = DateTime.Now;
            long length = 0;
            for (int i = 0; i < arrays.Count; i++)
                length += arrays[i].Length;
            byte[] byteProceduralObjectsArray = new byte[length];
            long currentLength = 0;
            for (int i = 0; i < arrays.Count; i++)
            {
                Array.Copy(arrays[i], 0, byteProceduralObjectsArray, currentLength, arrays[i].Length);
                currentLength += arrays[i].Length;
            }
            var byteArrayCopyTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 2);
            Debug.Log("[ProceduralObjects] byteProceduralObjectsArray finished in " + byteArrayCopyTime + " seconds");

            if (byteProceduralObjectsArray.Length > 0)
            {
                MemoryStream proceduralObjStream = new MemoryStream();
                proceduralObjStream.Write(byteProceduralObjectsArray, 0, byteProceduralObjectsArray.Length);
                var memStreamWriteTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 2);
                Debug.Log("[ProceduralObjects] memStreamWriteTime finished in " + (memStreamWriteTime - byteArrayCopyTime) + " seconds");

                proceduralObjStream.Position = 0;
                try
                {
                    ProceduralObjectContainer[] data = new BinaryFormatter().Deserialize(proceduralObjStream) as ProceduralObjectContainer[];
                    if (data.Count() > 0)
                    {
                        ProceduralObjectsMod.tempContainerData = data;
                        Debug.Log("[ProceduralObjects] Data Loading : transfered " + data.Count() + " ProceduralObjectContainer instances to ProceduralObjectsLogic.");

                        var memStreamDeserializeTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 2);
                        Debug.Log("[ProceduralObjects] memStreamDeserializeTime finished in " + (memStreamDeserializeTime - memStreamWriteTime) + " seconds");
                    }
                    else
                        Debug.LogWarning("[ProceduralObjects] No procedural object found while loading the map.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[ProceduralObjects] Data failed to load due to " + e.GetType().ToString() + " : \"" + e.Message + "\"");
                }
                finally
                {
                    proceduralObjStream.Close();
                    Debug.Log("[ProceduralObjects] Objects data loading ended.");
                }
            }
            else
            {
                Debug.Log("[ProceduralObjects] No objects data was found to load!");
            }

            var byteObjectDeserializeTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 2);
            Debug.Log("[ProceduralObjects] byteObjectDeserialize finished in " + (byteObjectDeserializeTime - byteArrayCopyTime) + " seconds");

            byte[] layerData = serializableDataManager.LoadData(layerKey);
            if (layerData != null)
            {
                MemoryStream layerStream = new MemoryStream();
                layerStream.Write(layerData, 0, layerData.Length);
                layerStream.Position = 0;
                try
                {
                    Layer[] data = new BinaryFormatter().Deserialize(layerStream) as Layer[];
                    if (data.Count() > 0)
                    {
                        ProceduralObjectsMod.tempLayerData = data;
                        Debug.Log("[ProceduralObjects] Data Loading : transfered " + data.Count() + " Layer instances to ProceduralObjectsLogic.");
                    }
                    else
                        Debug.LogWarning("[ProceduralObjects] No layer found while loading the map.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[ProceduralObjects] Layer data failed to load due to " + e.GetType().ToString() + " : \"" + e.Message + "\"");
                }
                finally
                {
                    layerStream.Close();
                }
            }

            var layerLoadTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 2);
            Debug.Log("[ProceduralObjects] layerLoad finished in " + (layerLoadTime - byteObjectDeserializeTime) + " seconds");

            Debug.Log("[ProceduralObjects] Data loading ended.");
        }

        private Dictionary<string, byte[]> SplitArray(byte[] sourceArray)
        {
            var dict = new Dictionary<string, byte[]>();
            var list = new List<byte>();
            int dictNumber = 0;
            uint currentCount = 0;
            for (uint i = 0; i < sourceArray.Length; i++)
            {
                if (currentCount < 16711679)
                {
                    list.Add(sourceArray[i]);
                    currentCount += 1;
                }
                else
                {
                    string id = dataKey + ((dictNumber == 0) ? "" : dictNumber.ToString());
                    dict[id] = list.ToArray();
                    dictNumber += 1;
                    list = new List<byte>();
                    list.Add(sourceArray[i]);
                    currentCount = 1;
                }
            }
            if (list.Count > 0)
            {
                string id = dataKey + ((dictNumber == 0) ? "" : dictNumber.ToString());
                dict[id] = list.ToArray();
            }
            return dict;
        }
    }

    [Serializable]
    public class ProceduralObjectContainer
    {
        public int id, tilingFactor;
        public byte meshStatus;
        public string basePrefabName, objectType, customTextureName;
        public float renderDistance;
        public float scale;
        public bool hasCustomTexture, disableRecalculation, flipFaces, disableCastShadows, renderDistLocked;
        public uint layerId;
        public int groupRootId;
        public bool belongsToGroup;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public SerializableVector3[] vertices;
        public POSerializableMeshData serializedMeshData;
        public SerializableColor color;
        public ProceduralObjectVisibility visibility;
        public NormalsRecalculation normalsRecalculation;
        public TextParameters textParam;
        public List<Dictionary<string, string>> modulesData;

        public ProceduralObjectContainer() { }
        public ProceduralObjectContainer(ProceduralObject baseObject)
        {
            id = baseObject.id;
            basePrefabName = baseObject.basePrefabName;
            objectType = baseObject.baseInfoType;
            renderDistance = baseObject.renderDistance;
            renderDistLocked = baseObject.renderDistLocked;
            position = new SerializableVector3(baseObject.m_position);
            rotation = new SerializableQuaternion(baseObject.m_rotation);
            scale = 1f;
            if (baseObject.group != null)
            {
                belongsToGroup = true;
                if (baseObject.isRootOfGroup)
                    groupRootId = -1; // = we are a group root 
                else
                    groupRootId = baseObject.group.root.id;
            }
            else
            {
                belongsToGroup = false; // we don't have a group
            }
            color = baseObject.m_color;
            // if (meshStatus > 0)
            meshStatus = baseObject.meshStatus;
            if (meshStatus != 1)
            {
                /*
                try
                {
                    serializedMeshData = new POSerializableMeshData(baseObject);
                    vertices = null;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ProceduralObjects] Warning : error while compressing Mesh data for object #" + baseObject.id + " (" + baseObject.baseInfoType + ", " + baseObject.basePrefabName + "). Saving uncompressed raw data instead\n" + e);
                 
                 * 
                 * */
                vertices = SerializableVector3.ToSerializableArray(baseObject.m_mesh.vertices);
                    serializedMeshData = null;
             //   }
            }
            else
            {
                serializedMeshData = null;
                vertices = null;
            }
            hasCustomTexture = baseObject.customTexture != null;
            visibility = baseObject.m_visibility;
            disableRecalculation = baseObject.disableRecalculation;
            normalsRecalculation = baseObject.normalsRecalcMode;
            layerId = (baseObject.layer == null) ? 0 : baseObject.layer.m_id;
            flipFaces = baseObject.flipFaces;
            disableCastShadows = baseObject.disableCastShadows;
            // recalculateNormals = baseObject.recalculateNormals;
            tilingFactor = baseObject.tilingFactor;
            if (baseObject.m_textParameters != null)
            {
                textParam = TextParameters.Clone(baseObject.m_textParameters, false);
                for (int i = 0; i < textParam.Count(); i++)
                {
                    textParam[i].serializableColor = null;
                }
            }
            if (hasCustomTexture == true)
                customTextureName = baseObject.customTexture.name;
            else
                customTextureName = string.Empty;
            modulesData = new List<Dictionary<string, string>>();
            if (baseObject.m_modules != null)
            {
                foreach (POModule m in baseObject.m_modules)
                    modulesData.Add(m._get_data(true));
            }
        }
    }
    [Serializable]
    public class SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3() { }
        public SerializableVector3(Vector3 source)
        {
            x = source.x;
            y = source.y;
            z = source.z;
        }
        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public static SerializableVector3[] ToSerializableArray(Vector3[] source)
        {
            int count = source.Count();
            SerializableVector3[] list = new SerializableVector3[count];
            for (int i = 0; i < count; i++)
                list[i] = new SerializableVector3(source[i]);
            return list;
        }
        public static Vector3[] ToStandardVector3Array(SerializableVector3[] source)
        {
            int count = source.Count();
            Vector3[] list = new Vector3[count];
            for (int i = 0; i < count; i++)
                list[i] = new Vector3(source[i].x, source[i].y, source[i].z);
            return list;
        }
        public static implicit operator SerializableVector3(Vector3 value)
        {
            return new SerializableVector3(value);
        }
        public static implicit operator Vector3(SerializableVector3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
    [Serializable]
    public class SerializableQuaternion
    {
        public float x, y, z, w;
        public SerializableQuaternion() { }
        public SerializableQuaternion(Quaternion source)
        {
            x = source.x;
            y = source.y;
            z = source.z;
            w = source.w;
        }
    }

    [Serializable]
    public class SerializableColor
    {
        public float r, g, b, a;
        public SerializableColor() { }
        public SerializableColor(SerializableColor c)
        {
            if (c == null)
            {
                this.r = 1f;
                this.g = 1f;
                this.b = 1f;
                this.a = 1f;
            }
            else
            {
                this.r = c.r;
                this.g = c.g;
                this.b = c.b;
                this.a = c.a;
            }
        }
        public SerializableColor(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = 1f;
        }
        public SerializableColor(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public override string ToString()
        {
            return "(" + r + ", " + g + ", " + b + ", " + a + ")";
        }
        public static SerializableColor Parse(string s)
        {
            string[] str = s.Trim().Replace("RGBA", "").Replace("(", "").Replace(")", "").Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            if (str.Length == 3)
                return new SerializableColor(float.Parse(str[0]), float.Parse(str[1]), float.Parse(str[2]));
            if (str.Length == 4)
                return new SerializableColor(float.Parse(str[0]), float.Parse(str[1]), float.Parse(str[2]), float.Parse(str[3]));
            return Color.white;
        }

        public static implicit operator Color(SerializableColor c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
        public static implicit operator SerializableColor(Color c)
        {
            return new SerializableColor(c.r, c.g, c.b, c.a);
        }

        public static bool Different(SerializableColor A, SerializableColor B)
        {
            return A.a != B.a || A.r != B.r || A.g != B.g || A.b != B.b;
        }

    }

    [Serializable]
    public class POSerializableMeshData
    {
        public POSerializableMeshData(ProceduralObject obj)
        {
            if (obj.meshStatus == 1)
                data = null;
            else
                this.BuildData(obj);
        }

        public Dictionary<SerializableVector3, List<int>> data;
        public void BuildData(ProceduralObject obj)
        {
            data = new Dictionary<SerializableVector3, List<int>>();
            var original = obj.baseInfoType == "PROP" ? obj._baseProp.m_mesh.vertices : obj._baseBuilding.m_mesh.vertices;
            for (int i = 0; i < obj.vertices.Length; i++)
            {
                var v = obj.vertices[i];
                if (v.Position == original[v.Index])
                    continue;
                List<int> list;
                if (data.TryGetValue(v.Position, out list))
                {
                    list.Add(v.Index);
                }
                else
                {
                    list = new List<int>();
                    list.Add(v.Index);
                    data.Add(v.Position, list);
                }
            }
        }
        public void ApplyDataToObject(ProceduralObject obj)
        {
            if (data == null) return;
            if (data.Count == 0) return;
            var vertices = obj.m_mesh.vertices;
            foreach (var kvp in data)
            {
                if (kvp.Value == null) return;
                if (kvp.Value.Count == 0) return;
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    vertices[kvp.Value[i]] = kvp.Key;
                }
            }
            obj.m_mesh.SetVertices(new List<Vector3>(vertices));
        }
    }
} 