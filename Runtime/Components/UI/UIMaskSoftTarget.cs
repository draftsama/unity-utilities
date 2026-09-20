using UnityEngine;
using UnityEngine.UI;

namespace Modules.Utilities
{
    /// <summary>
    /// Runtime companion added to each child masked by a <see cref="UIMaskSoft"/>.
    /// Routes the graphic through the mask via <see cref="IMaterialModifier"/>:
    /// standard graphics share the owner's UI mask material, while TextMeshPro graphics
    /// get a per-instance variant of their SDF material with the mask shader swapped in.
    /// Added and removed automatically, one per masked child; <see cref="IgnoreMask"/> is
    /// the only field a user should touch, so it stays visible and serialized.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIMaskSoftTarget : MonoBehaviour, IMaterialModifier
    {
        [Tooltip("Skip masking for this graphic, even though it is a child of the UIMaskSoft.")]
        [SerializeField] private bool m_IgnoreMask;

        private UIMaskSoft m_Owner;
        private Graphic m_Graphic;
        private Material m_TmpVariant;

        public Graphic Graphic => m_Graphic != null ? m_Graphic : (m_Graphic = GetComponent<Graphic>());

        public bool IgnoreMask
        {
            get => m_IgnoreMask;
            set
            {
                if (m_IgnoreMask == value) return;
                m_IgnoreMask = value;
                if (Graphic != null) Graphic.SetMaterialDirty();
            }
        }

        public void Bind(UIMaskSoft owner)
        {
            m_Owner = owner;
            m_Graphic = GetComponent<Graphic>();
        }

        /// <summary>Detach from the owner, restore the original material, and remove itself.</summary>
        public void Release()
        {
            m_Owner = null;
            DestroyVariant();
            if (m_Graphic != null) m_Graphic.SetMaterialDirty();

            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (m_IgnoreMask || m_Owner == null || !m_Owner.isActiveAndEnabled) return baseMaterial;

            if (IsTextMeshPro(baseMaterial))
                return GetTmpVariant(baseMaterial);

            // Standard UI graphic: the shared mask material replaces the base material.
            var maskMaterial = m_Owner.MaskMaterial;
            return maskMaterial != null ? maskMaterial : baseMaterial;
        }

        private Material GetTmpVariant(Material baseMaterial)
        {
            var shader = m_Owner.TmpMaskShader;
            if (shader == null) return baseMaterial; // TMP shader missing; leave text untouched

            if (m_TmpVariant == null)
            {
                m_TmpVariant = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
                m_Owner.RegisterDynamicMaterial(m_TmpVariant);
            }

            // Mirror the font atlas / SDF properties and keywords, then swap in the mask shader.
            m_TmpVariant.CopyPropertiesFromMaterial(baseMaterial);
            m_TmpVariant.shader = shader;
            m_Owner.ApplyTo(m_TmpVariant);
            return m_TmpVariant;
        }

        private void DestroyVariant()
        {
            if (m_TmpVariant == null) return;
            if (m_Owner != null) m_Owner.UnregisterDynamicMaterial(m_TmpVariant);
            if (Application.isPlaying) Destroy(m_TmpVariant);
            else DestroyImmediate(m_TmpVariant);
            m_TmpVariant = null;
        }

        private static bool IsTextMeshPro(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name.StartsWith("TextMeshPro");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Graphic != null) Graphic.SetMaterialDirty();
        }
#endif
    }
}
