using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Modules.Utilities
{
    /// <summary>
    /// Soft UI mask, modeled on Unity's built-in <see cref="Mask"/>: put it on a UI
    /// object that has an <see cref="Image"/> or <see cref="RawImage"/>, and that graphic's
    /// texture becomes a black-white mask for every child <see cref="MaskableGraphic"/>
    /// (Image, RawImage, Text and TextMeshPro). White = visible, black = hidden; gradients
    /// give soft edges. Content outside the mask rect is hidden.
    /// <see cref="ShowMask"/> toggles whether the mask graphic itself is drawn (like
    /// Mask.showMaskGraphic); <see cref="Invert"/> flips the mask.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/UI Mask Soft")]
    public class UIMaskSoft : MonoBehaviour, IMaterialModifier
    {
        [Tooltip("Flip the mask: black becomes visible, white becomes hidden.")]
        [SerializeField] private bool m_Invert;

        [Tooltip("Draw the mask graphic itself. Off = the mask texture is used but not shown (like Mask.showMaskGraphic).")]
        [SerializeField] private bool m_ShowMask;

        private static readonly int s_SoftMaskTex = Shader.PropertyToID("_SoftMaskTex");
        private static readonly int s_SoftMaskMatrix = Shader.PropertyToID("_SoftMaskMatrix");
        private static readonly int s_SoftMaskRect = Shader.PropertyToID("_SoftMaskRect");
        private static readonly int s_SoftMaskInvert = Shader.PropertyToID("_SoftMaskInvert");
        private static readonly int s_ColorMask = Shader.PropertyToID("_ColorMask");

        private const string k_ShaderName = "UI/UIMaskSoft";
        private const string k_TmpShaderName = "TextMeshPro/UIMaskSoft";

        private RectTransform m_RectTransform;
        private Canvas m_RootCanvas;
        private Graphic m_MaskGraphic;
        private Material m_Material;
        private Material m_HideMaterial;
        private Material m_HideSource;
        private Shader m_TmpShader;
        private bool m_TmpShaderLookedUp;
        private readonly HashSet<Material> m_ActiveMaterials = new HashSet<Material>();
        private readonly List<UIMaskSoftTarget> m_Targets = new List<UIMaskSoftTarget>();

        public RectTransform RectTransform => m_RectTransform != null
            ? m_RectTransform
            : (m_RectTransform = (RectTransform)transform);

        /// <summary>The Image/RawImage that supplies the mask texture (same GameObject).</summary>
        public Graphic MaskGraphic => m_MaskGraphic != null
            ? m_MaskGraphic
            : (m_MaskGraphic = GetComponent<Graphic>());

        /// <summary>The grayscale texture used as the mask, taken from the mask graphic.</summary>
        public Texture MaskTexture => MaskGraphic != null ? MaskGraphic.mainTexture : null;

        /// <summary>Shared material handed to standard (non-TMP) masked children. Created lazily.</summary>
        public Material MaskMaterial
        {
            get
            {
                if (m_Material == null)
                {
                    var shader = Shader.Find(k_ShaderName);
                    if (shader == null)
                    {
                        Debug.LogError($"[UIMaskSoft] Shader '{k_ShaderName}' not found. Ensure it stays under a Resources folder.", this);
                        return null;
                    }

                    m_Material = new Material(shader)
                    {
                        name = "UIMaskSoft (Instance)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    RegisterDynamicMaterial(m_Material);
                }

                return m_Material;
            }
        }

        /// <summary>TMP-compatible mask shader, or null if TextMeshPro is not present.</summary>
        public Shader TmpMaskShader
        {
            get
            {
                if (!m_TmpShaderLookedUp)
                {
                    m_TmpShader = Shader.Find(k_TmpShaderName);
                    m_TmpShaderLookedUp = true;
                }
                return m_TmpShader;
            }
        }

        public bool Invert
        {
            get => m_Invert;
            set
            {
                if (m_Invert == value) return;
                m_Invert = value;
                ApplyAll();
            }
        }

        public bool ShowMask
        {
            get => m_ShowMask;
            set
            {
                if (m_ShowMask == value) return;
                m_ShowMask = value;
                if (MaskGraphic != null) MaskGraphic.SetMaterialDirty();
            }
        }

        private void OnEnable()
        {
            m_RectTransform = (RectTransform)transform;
            m_RootCanvas = null;
            m_MaskGraphic = GetComponent<Graphic>();
            if (m_MaskGraphic == null)
                Debug.LogWarning("[UIMaskSoft] No Image or RawImage on this object; add one to supply the mask texture.", this);
            else
                m_MaskGraphic.SetMaterialDirty();

            RefreshTargets();
            ApplyAll();
        }

        private void OnDisable()
        {
            ClearTargets();
            DestroyMaterial(ref m_Material);
            DestroyMaterial(ref m_HideMaterial);
            m_HideSource = null;
            if (m_MaskGraphic != null) m_MaskGraphic.SetMaterialDirty();
        }

        private void OnCanvasHierarchyChanged()
        {
            m_RootCanvas = null;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) RefreshTargets();
        }

        private void LateUpdate()
        {
            ApplyAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ApplyAll();
            // Immediate: re-run our material modifier so Show Mask / Invert update without lag.
            if (MaskGraphic != null) MaskGraphic.SetMaterialDirty();
            // Deferred: AddComponent on children is not allowed inside OnValidate.
            UnityEditor.EditorApplication.delayCall += DeferredEditorRefresh;
        }

        private void DeferredEditorRefresh()
        {
            if (this == null || !isActiveAndEnabled) return;
            RefreshTargets();
        }
#endif

        // IMaterialModifier on the mask graphic itself: hide it (render nothing) when
        // Show Mask is off, exactly like Unity's Mask does with showMaskGraphic.
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || m_ShowMask) return baseMaterial;

            if (m_HideMaterial == null || m_HideSource != baseMaterial)
            {
                DestroyMaterial(ref m_HideMaterial);
                m_HideMaterial = new Material(baseMaterial)
                {
                    name = "UIMaskSoft Hidden (Instance)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                m_HideMaterial.SetFloat(s_ColorMask, 0f);
                m_HideSource = baseMaterial;
            }

            return m_HideMaterial;
        }

        /// <summary>Rescan children and (un)assign the mask material. Call after deep hierarchy changes.</summary>
        public void RefreshTargets()
        {
            var graphics = GetComponentsInChildren<MaskableGraphic>(true);
            var seen = new HashSet<Graphic>();

            foreach (var graphic in graphics)
            {
                if (graphic.transform == transform) continue; // never mask the mask graphic

                seen.Add(graphic);
                var target = graphic.GetComponent<UIMaskSoftTarget>();
                if (target == null) target = graphic.gameObject.AddComponent<UIMaskSoftTarget>();
                target.Bind(this);
                if (!m_Targets.Contains(target)) m_Targets.Add(target);

                graphic.SetMaterialDirty();
            }

            for (int i = m_Targets.Count - 1; i >= 0; i--)
            {
                var target = m_Targets[i];
                if (target == null)
                {
                    m_Targets.RemoveAt(i);
                    continue;
                }

                if (!seen.Contains(target.Graphic))
                {
                    target.Release();
                    m_Targets.RemoveAt(i);
                }
            }
        }

        /// <summary>Add a per-child material (e.g. a TMP variant) to the set kept in sync each frame.</summary>
        public void RegisterDynamicMaterial(Material material)
        {
            if (material == null) return;
            if (m_ActiveMaterials.Add(material)) ApplyTo(material);
        }

        public void UnregisterDynamicMaterial(Material material)
        {
            if (material != null) m_ActiveMaterials.Remove(material);
        }

        private void ApplyAll()
        {
            m_ActiveMaterials.RemoveWhere(m => m == null);
            foreach (var material in m_ActiveMaterials) ApplyTo(material);
        }

        /// <summary>Push the current mask texture, invert flag and mapping onto one material.</summary>
        public void ApplyTo(Material material)
        {
            if (material == null) return;
            var tex = MaskTexture;
            material.SetTexture(s_SoftMaskTex, tex != null ? tex : Texture2D.whiteTexture);
            material.SetFloat(s_SoftMaskInvert, m_Invert ? 1f : 0f);
            material.SetMatrix(s_SoftMaskMatrix, CalcMaskMatrix());
            material.SetVector(s_SoftMaskRect, CalcMaskRect());
        }

        private Matrix4x4 CalcMaskMatrix()
        {
            if (m_RootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) m_RootCanvas = canvas.rootCanvas;
            }

            // UI vertices reach the shader in root-canvas local space (the space RectMask2D
            // clips in). Map that back to world, then into this rect's local space.
            Matrix4x4 canvasLocalToWorld = m_RootCanvas != null
                ? m_RootCanvas.transform.localToWorldMatrix
                : Matrix4x4.identity;
            return RectTransform.worldToLocalMatrix * canvasLocalToWorld;
        }

        private Vector4 CalcMaskRect()
        {
            var rect = RectTransform.rect;
            return new Vector4(rect.xMin, rect.yMin, rect.width, rect.height);
        }

        private void ClearTargets()
        {
            foreach (var target in m_Targets)
                if (target != null) target.Release();
            m_Targets.Clear();
        }

        private void DestroyMaterial(ref Material material)
        {
            UnregisterDynamicMaterial(material);
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }
    }
}
