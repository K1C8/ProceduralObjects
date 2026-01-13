using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    public sealed class PropInfoHelper
    {
        private static PropInfoHelper _instance = new PropInfoHelper();

        private PropInfo[] _propInfos;
        private Dictionary<string, PropInfo> _propInfoCache;
        private int _propCountTest;

        private PropInfoHelper() 
        {
            
        }

        private PropInfo GetPropInfoPrivate(string inName)
        {
            if (_propInfos == null)
            {
                _propInfos = Resources.FindObjectsOfTypeAll<PropInfo>();
                _propCountTest = PrefabCollection<PropInfo>.LoadedCount();
                Debug.Log(string.Format("[ProceduralObjects] PropInfoHelper loading, Unity returned all PropInfo in Resources counted as {0}, PropInfo from PrefabCollection provided by CO counted as {1}.", _propInfos.Count(), _propCountTest));
                _propInfoCache = new Dictionary<string, PropInfo>();
            }

            if (_propInfoCache.ContainsKey(inName) && _propInfoCache[inName].name.Equals(inName))
            {
                return _propInfoCache[inName];
            }
            else if (_propInfoCache.ContainsKey(inName) && !_propInfoCache[inName].name.Equals(inName))
            {
                _propInfoCache.Remove(inName);
            }
            PropInfo result = null;
            result = _propInfos.FirstOrDefault(info => info.name.Equals(inName));
            if (result != null)
            {
                Debug.Log(string.Format("[ProceduralObjects] Caching PropInfo, input basePropName: {0}, matched prop object name in PropInfo[]: {1}", inName, result.name));
                _propInfoCache[inName] = result;
            }
            //int i;
            //for (i = 0; i < _propInfos.Count(); i++)
            //{
            //    PropInfo curr = _propInfos[i];
            //    if (curr != null && curr.name.Equals(inName))
            //    {
            //        Debug.Log(string.Format("[ProceduralObjects] Caching PropInfo, name: {0}, sequence number in provided PropInfo[]: {1}", inName, i));
            //        _propInfoCache[inName] = curr;
            //        return curr;
            //    }
            //}
            if (result == null)
            {
                Debug.Log(string.Format("[ProceduralObjects] Caching PropInfo failed. input basePropName: {0}", inName));
            }
            return result;
        }

        public static PropInfo GetPropInfo(string name)
        {
            return _instance.GetPropInfoPrivate(name);
        }

        private void OnDestroyPrivate()
        {
            if (_propInfoCache != null)
            {
                _propInfoCache.Clear();
            }
        }

        public static void OnDestroy()
        {
            if (_instance != null)
            {
                _instance.OnDestroyPrivate();
            }
        }

    }
}
