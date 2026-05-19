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
                Material originalPropMat = PropInfoHelper.GetPropInfo(obj.basePrefabName).m_material;
                obj.m_material.shader = originalPropMat.shader;
                Debug.Log(string.Format("[ProceduralObjects] ShaderConversionController is converting an instanced shader of a PO to the original shader {0}.",
                   originalPropMat.shader.name));
            }
        }

        public void ConvertToInstancedShader(ProceduralObject obj)
        {
            if (obj == null || _defaultPropInstancedShader == null || _blendDecalInstancedShader == null) { return; }
            if (obj.m_material.shader.name.Equals(_defaultPropOriginalShaderName))
            {
                obj.m_material.shader = _defaultPropInstancedShader;
                obj.m_material.EnableKeyword(_shadeCastingKeyword);
                obj.m_material.enableInstancing = true;
            } 
            else if (obj.m_material.shader.name.Equals(_blendDecalOriginalShaderName))
            {
                obj.m_material.shader = _blendDecalInstancedShader;
                obj.m_material.enableInstancing = true;
            }
            Debug.Log(string.Format("[ProceduralObjects] ShaderConversionController is converting the original shader of a PO to the instanced shader {0}.",
                obj.m_material.shader.name));
        }

    }
}
