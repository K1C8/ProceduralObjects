﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralObjects.Classes
{
    public class ProceduralObject
    {
        public ProceduralObject() { }
        public ProceduralObject(ProceduralObjectContainer container, List<Texture2D> textures)
        {
            if (container.objectType == "PROP")
            {
                PropInfo sourceProp = Resources.FindObjectsOfTypeAll<PropInfo>().FirstOrDefault(info => info.name == container.basePrefabName);
                this.id = container.id;
                this.basePrefabName = container.basePrefabName;
                this.baseInfoType = "PROP";
                this.isPloppableAsphalt = (sourceProp.m_mesh.name == "ploppableasphalt-prop");
                renderDistance = container.renderDistance;
                m_position = container.position.ToVector3();
                m_rotation = container.rotation.ToQuaternion();
              //  gameObject.transform.localScale = new Vector3(container.scale, container.scale, container.scale);
                this.m_mesh = sourceProp.m_mesh.InstantiateMesh();
                allVertices = SerializableVector3.ToStandardVector3Array(container.vertices);
                if (container.scale != 0)
                {
                    for (int i = 0; i < allVertices.Count(); i++)
                    {
                        allVertices[i] = new Vector3(allVertices[i].x * container.scale, allVertices[i].y * container.scale, allVertices[i].z * container.scale);
                    }
                }
                m_mesh.SetVertices(new List<Vector3>(allVertices));
                m_material = GameObject.Instantiate(sourceProp.m_material);
                if (container.hasCustomTexture && textures != null)
                {
                    if (!textures.Any(tex => tex.name == container.customTextureName))
                        Debug.LogError("[ProceduralObjects] An object was found with a texture that doesn't exist anymore with the name " + container.customTextureName + ", therefore loading the default object texture");
                    else
                    {
                        var customTex = textures.FirstOrDefault(tex => tex.name == container.customTextureName);
                        m_material.mainTexture = customTex as Texture;
                        customTexture = customTex;
                    }
                }
            }
            else if (container.objectType == "BUILDING")// building
            {
                BuildingInfo sourceProp = Resources.FindObjectsOfTypeAll<BuildingInfo>().FirstOrDefault(info => info.name == container.basePrefabName);
                this.id = container.id;
                this.basePrefabName = container.basePrefabName;
                this.baseInfoType = "BUILDING";
                this.isPloppableAsphalt = false;
                renderDistance = container.renderDistance;
                m_position = container.position.ToVector3();
                m_rotation = container.rotation.ToQuaternion();
              //  gameObject.transform.localScale = new Vector3(container.scale, container.scale, container.scale);
                m_mesh = sourceProp.m_mesh.InstantiateMesh();
                allVertices = SerializableVector3.ToStandardVector3Array(container.vertices);
                if (container.scale != 0)
                {
                    for (int i = 0; i < allVertices.Count(); i++)
                    {
                        allVertices[i] = new Vector3(allVertices[i].x * container.scale, allVertices[i].y * container.scale, allVertices[i].z * container.scale);
                    }
                }
                m_mesh.SetVertices(new List<Vector3>(allVertices));
                m_material = GameObject.Instantiate(sourceProp.m_material);
                if (container.hasCustomTexture && textures != null)
                {
                    if (!textures.Any(tex => tex.name == container.customTextureName))
                        Debug.LogError("[ProceduralObjects] An object was found with a texture that doesn't exist anymore at the specified path " + container.hasCustomTexture + ", therefore loading the default object texture");
                    else
                    {
                        var customTex = textures.FirstOrDefault(tex => tex.name == container.customTextureName);
                        m_material.mainTexture = customTex as Texture;
                        customTexture = customTex;
                    }
                }
            }
        }
        public ProceduralObject(CacheProceduralObject sourceCacheObj, int id, Vector3 position)
        {
            if (sourceCacheObj.baseInfoType == "PROP")
            {
                PropInfo sourceProp = Resources.FindObjectsOfTypeAll<PropInfo>().FirstOrDefault(info => info.name == sourceCacheObj.basePrefabName);
                this.id = id;
                this.basePrefabName = sourceCacheObj.basePrefabName;
                this.baseInfoType = "PROP";
                this.isPloppableAsphalt = (sourceProp.m_mesh.name == "ploppableasphalt-prop");
                renderDistance = sourceCacheObj.renderDistance;
                m_position = position;
                m_rotation = sourceCacheObj.m_rotation;
              //  gameObject.transform.localScale = new Vector3(sourceCacheObj.scale, sourceCacheObj.scale, sourceCacheObj.scale);
                m_mesh = sourceProp.m_mesh.InstantiateMesh();
                allVertices = sourceCacheObj.allVertices;
                m_mesh.SetVertices(new List<Vector3>(allVertices));
                m_material = GameObject.Instantiate(sourceProp.m_material);
                if (sourceCacheObj.customTexture != null)
                {
                    m_material.mainTexture = sourceCacheObj.customTexture as Texture;
                    customTexture = sourceCacheObj.customTexture;
                }
            }
            else if (sourceCacheObj.baseInfoType == "BUILDING")// building
            {
                BuildingInfo sourceProp = Resources.FindObjectsOfTypeAll<BuildingInfo>().FirstOrDefault(info => info.name == sourceCacheObj.basePrefabName);
                this.id = id;
                this.basePrefabName = sourceCacheObj.basePrefabName;
                this.baseInfoType = "BUILDING";
                this.isPloppableAsphalt = false;
                renderDistance = sourceCacheObj.renderDistance;
                m_position = position;
                m_rotation = sourceCacheObj.m_rotation;
              //  gameObject.transform.localScale = new Vector3(sourceCacheObj.scale, sourceCacheObj.scale, sourceCacheObj.scale);
                m_mesh = sourceProp.m_mesh.InstantiateMesh();
                allVertices = sourceCacheObj.allVertices;
                m_mesh.SetVertices(new List<Vector3>(allVertices));
                m_material = GameObject.Instantiate(sourceProp.m_material);
                if (sourceCacheObj.customTexture != null)
                {
                    m_material.mainTexture = sourceCacheObj.customTexture as Texture;
                    customTexture = sourceCacheObj.customTexture;
                }
            }
        }

        public void ConstructObject(PropInfo sourceProp, int id, Texture2D customTex = null)
        {
            this.id = id;
            this.basePrefabName = sourceProp.name;
            this.isPloppableAsphalt = (sourceProp.m_mesh.name == "ploppableasphalt-prop");
            this.baseInfoType = "PROP";
            this.renderDistance = ProceduralObjectsMod.PropRenderDistance.value;
            m_position = ToolsModifierControl.cameraController.m_currentPosition;
            m_rotation = Quaternion.identity;
            Mesh mesh = sourceProp.m_mesh.InstantiateMesh();
            m_mesh = mesh;
            allVertices = mesh.vertices;
            m_material = GameObject.Instantiate(sourceProp.m_material);
            if (customTex != null)
            {
                m_material.mainTexture = customTex as Texture;
                customTexture = customTex;
            }
        }
        public void ConstructObject(BuildingInfo sourceBuilding, int id, Texture2D customTex = null)
        {
            this.id = id;
            this.basePrefabName = sourceBuilding.name;
            this.isPloppableAsphalt = false;
            this.baseInfoType = "BUILDING";
            this.renderDistance = ProceduralObjectsMod.BuildingRenderDistance.value;
            m_position = ToolsModifierControl.cameraController.m_currentPosition;
            m_rotation = Quaternion.identity;
            Mesh mesh = sourceBuilding.m_mesh.InstantiateMesh();
            m_mesh = mesh;
            allVertices = mesh.vertices;
            m_material = GameObject.Instantiate(sourceBuilding.m_material);
            if (customTex != null)
            {
                m_material.mainTexture = customTex as Texture;
                customTexture = customTex;
            }
        }

        public Mesh m_mesh;
        public Material m_material;
        public Vector3 m_position;
        public Quaternion m_rotation;
        public Texture2D customTexture;
        public Vector3[] allVertices;
        public string basePrefabName, baseInfoType;
        public int id;
        public float renderDistance, m_scale;
        public bool isPloppableAsphalt;

        public GameObject tempObj;


        public bool RequiresUVRecalculation
        {
            get 
            {
                return (this.basePrefabName.Contains("NativeCube_Procedural") || this.basePrefabName.Contains("NativeSquare_Procedural"));
            }
        }
    }

    public class CacheProceduralObject
    {
        public CacheProceduralObject() { }
        public CacheProceduralObject(ProceduralObject sourceObj)
        {
            renderDistance = sourceObj.renderDistance;
            allVertices = sourceObj.m_mesh.vertices;
            customTexture = sourceObj.customTexture;
            m_rotation = sourceObj.m_rotation;
            basePrefabName = sourceObj.basePrefabName;
            baseInfoType = sourceObj.baseInfoType;
        }

        public float renderDistance;
        public bool isPloppableAsphalt;
        public Quaternion m_rotation;
        public Texture2D customTexture;
        public string basePrefabName, baseInfoType;
        public Vector3[] allVertices;
    }
    
    public class ProceduralInfo
    {
        public ProceduralInfo() { }
        public ProceduralInfo(PropInfo info, bool basic)
        {
            isBasicShape = basic;
            propPrefab = info;
            infoType = "PROP";
        }
        public ProceduralInfo(BuildingInfo info, bool basic)
        {
            isBasicShape = basic;
            buildingPrefab = info;
            infoType = "BUILDING";
        }
        public PropInfo propPrefab;
        public BuildingInfo buildingPrefab;
        public bool isBasicShape;
        public string infoType;

        public string GetShowName()
        {
            if (infoType == "PROP")
                return propPrefab.GetLocalizedTitle() + (isBasicShape ? " (Basic)" : string.Empty);
            else // if (infoType == "BUILDING")
                return buildingPrefab.GetLocalizedTitle() + (isBasicShape ? " (Basic)" : string.Empty);
        }
    }

    public static class ProceduralUtils
    {
        /* public static void FixBridgesAvailability()
        {
            var buildings = Resources.FindObjectsOfTypeAll<BuildingInfo>();
            if (buildings.Any(b => b.name.Contains("EuroLowStoneBridge03")))
                buildings.FirstOrDefault(b => b.name.Contains("EuroLowStoneBridge03")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("EuroLowStoneBridge01")))
                buildings.FirstOrDefault(b => b.name.Contains("EuroLowStoneBridge01")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("EuroLowStoneBridge02")))
                buildings.FirstOrDefault(b => b.name.Contains("EuroLowStoneBridge02")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("Railbridge04")))
                buildings.FirstOrDefault(b => b.name.Contains("Railbridge04")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("Railbridge02")))
                buildings.FirstOrDefault(b => b.name.Contains("Railbridge02")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("Large span bridge pillars")))
                buildings.FirstOrDefault(b => b.name.Contains("Large span bridge pillars")).m_availableIn = ItemClass.Availability.All;
            if (buildings.Any(b => b.name.Contains("Large span high arch bridge")))
                buildings.FirstOrDefault(b => b.name.Contains("Large span high arch bridge")).m_availableIn = ItemClass.Availability.All;
        } */
        public static int GetNextUnusedId(this List<ProceduralObject> list)
        {
            for (int i = 0; true; i++)
            {
                if (list.GetObjectWithId(i) == null)
                    return i;
            }
        }
        public static ProceduralObject GetObjectWithId(this List<ProceduralObject> list, int id)
        {
            if (list.Any(po => po.id == id))
            {
                return list.FirstOrDefault(po => po.id == id);
            }
            return null;
        }
        public static Vector2 WorldToGuiPoint(this Vector3 position)
        {
            var guiPosition = Camera.main.WorldToScreenPoint(position);
            guiPosition.y = Screen.height - guiPosition.y;
            return new Vector2(guiPosition.x, guiPosition.y);
        }

        public static ProceduralObjectContainer[] GetContainerList(this ProceduralObjectsLogic logic)
        {
            var list = new List<ProceduralObjectContainer>();
            if (logic.proceduralObjects == null)
                return null;
            foreach (ProceduralObject obj in logic.proceduralObjects)
            {
                list.Add(new ProceduralObjectContainer(obj));
            }
            return list.ToArray();
        }

        public static void LoadContainerData(this ProceduralObjectsLogic logic, ProceduralObjectContainer[] containerArray)
        {
            logic.proceduralObjects = new List<ProceduralObject>();
            logic.availableProceduralInfos = CreateProceduralInfosList();
            foreach (var c in containerArray)
            {
                var obj = new ProceduralObject(c, logic.basicTextures); 
                if (obj.RequiresUVRecalculation)
                {
                    try
                    {
                        obj.m_mesh.uv = Vertex.RecalculateUVMap(obj, Vertex.CreateVertexList(obj));
                    }
                    catch
                    {
                        Debug.LogError("[ProceduralObjects] Error on save loading : Couldn't recalculate UV map on a procedural object of type " + obj.basePrefabName + " (" + obj.baseInfoType + ")");
                    }
                }

                logic.proceduralObjects.Add(obj);
            }
        }
        public static Mesh InstantiateMesh(this Mesh source)
        {
            if (source == null)
                return null;
            var m = new Mesh();
            m.name = source.name;
            m.vertices = source.vertices;
            m.triangles = source.triangles;
            m.uv = source.uv;
            m.uv2 = source.uv2;
            m.uv3 = source.uv3;
            m.uv4 = source.uv4;
            m.tangents = source.tangents;
            m.bindposes = source.bindposes;
            m.colors = source.colors;
            m.colors32 = source.colors32;
            m.normals = source.normals;
            return m;
        }
        public static ProceduralInfo[] ToProceduralInfoArray(this IEnumerable<PropInfo> source)
        {
            var list = new List<ProceduralInfo>();
            foreach (PropInfo info in source)
            {
                list.Add(new ProceduralInfo(info, info.GetLocalizedDescription().ToLower().Contains("basic")));
            }
            return list.ToArray();
        }
        public static ProceduralInfo[] ToProceduralInfoArray(this IEnumerable<BuildingInfo> source)
        {
            var list = new List<ProceduralInfo>();
            foreach (BuildingInfo info in source)
            {
                list.Add(new ProceduralInfo(info, info.GetLocalizedDescription().ToLower().Contains("basic")));
            }
            return list.ToArray();
        }
        public static List<ProceduralInfo> CreateProceduralInfosList()
        {
            try
            {
                return new List<ProceduralInfo>(new List<ProceduralInfo>(Resources.FindObjectsOfTypeAll<PropInfo>().ToProceduralInfoArray())
                    .Concat(new List<ProceduralInfo>(Resources.FindObjectsOfTypeAll<BuildingInfo>().ToProceduralInfoArray())));
            }
            catch
            {
                Debug.LogError("[ProceduralObjects] Fatal Loading exception : couldn't find all assets and make them procedural objects !");
            }
            return new List<ProceduralInfo>();
        }
    }

    public class ProceduralObjRayCast : ToolBase
    {
        public static bool TerrainRaycast(RaycastInput raycastInput, out RaycastOutput raycastOutput)
        {
            return ToolBase.RayCast(raycastInput, out raycastOutput);
        }
    }
}
