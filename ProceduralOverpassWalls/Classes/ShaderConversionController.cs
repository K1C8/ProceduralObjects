using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProceduralObjects.Classes
{
    public class ShaderConversionController
    {
        private Shader _defaultPropInstancedShader;
        //private Shader _animUvPropInstancedShader;
        //private Shader _rotorPropInstancedShader;

        private string _shadeCastingKeyword = "SHADOWS_SCREEN";
        private string _defaultPropOriginalShaderName = "Custom/Props/Prop/Default";

        public Shader DefaultPropInstancedShader 
        { 
            get { return _defaultPropInstancedShader; }
            set { _defaultPropInstancedShader = value; }
        }

        public void DefaultPropConvertToOriginalShader(ProceduralObject obj)
        {
            if (obj == null) return;
            if (obj.m_material.shader.name.Equals(_defaultPropInstancedShader.name))
            {
                Debug.Log("[ProceduralObjects] ShaderConversionController is converting an instanced default prop shader of a PO to the original default prop shader");
                Material originalPropMat = PropInfoHelper.GetPropInfo(obj.basePrefabName).m_material;
                obj.m_material.shader = originalPropMat.shader;
            }
        }

        public void DefaultPropConvertToInstancedShader(ProceduralObject obj)
        {
            if (obj == null || _defaultPropInstancedShader == null) return;
            if (obj.m_material.shader.name.Equals(_defaultPropOriginalShaderName))
            {
                //Debug.Log("[ProceduralObjects] ShaderConversionController is converting an original default prop shader of a PO to the instanced default prop shader");
                obj.m_material.shader = _defaultPropInstancedShader;
                obj.m_material.EnableKeyword(_shadeCastingKeyword);
                obj.m_material.enableInstancing = true;
            }
        }
    }
}
