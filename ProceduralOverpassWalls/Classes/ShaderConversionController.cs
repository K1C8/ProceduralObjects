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
        private Shader _blendDecalInstancedShader;
        //private Shader _animUvPropInstancedShader;
        //private Shader _rotorPropInstancedShader;

        private string _shadeCastingKeyword = "SHADOWS_SCREEN";
        private string _defaultPropOriginalShaderName = "Custom/Props/Prop/Default";
        private string _blendDecalOriginalShaderName = "Custom/Props/Decal/Blend";

        public Shader DefaultPropInstancedShader
        {
            get { return _defaultPropInstancedShader; }
            set { _defaultPropInstancedShader = value; }
        }

        public Shader BlendDecalInstancedShader
        {
            get { return _blendDecalInstancedShader; }
            set { _blendDecalInstancedShader = value; }
        }

        public void ConvertToOriginalShader(ProceduralObject obj)
        {
            if (obj == null) return;
            if (obj.m_material.shader.name.Equals(_defaultPropInstancedShader.name) || obj.m_material.shader.name.Equals(_blendDecalInstancedShader.name))
            {
                Debug.Log("[ProceduralObjects] ShaderConversionController is converting an instanced shader of a PO to the original shader.");
                Material originalPropMat = PropInfoHelper.GetPropInfo(obj.basePrefabName).m_material;
                obj.m_material.shader = originalPropMat.shader;
            }
        }

        public void ConvertToInstancedShader(ProceduralObject obj)
        {
            if (obj == null || _defaultPropInstancedShader == null || _blendDecalInstancedShader == null) { return; }
            if (obj.m_material.shader.name.Equals(_defaultPropOriginalShaderName))
            {
                //Debug.Log("[ProceduralObjects] ShaderConversionController is converting an original default prop shader of a PO to the instanced default prop shader");
                obj.m_material.shader = _defaultPropInstancedShader;
                obj.m_material.EnableKeyword(_shadeCastingKeyword);
                obj.m_material.enableInstancing = true;
            } 
            else if (obj.m_material.shader.name.Equals(_blendDecalOriginalShaderName))
            {
                //Debug.Log("[ProceduralObjects] ShaderConversionController is converting an original blend decal shader of a PO to the instanced blend decal shader");
                obj.m_material.shader = _blendDecalInstancedShader;
                obj.m_material.enableInstancing = true;
            }
        }

    }
}
