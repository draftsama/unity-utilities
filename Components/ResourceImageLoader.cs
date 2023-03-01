using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Modules.Utilities;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

namespace Modules.Utilities
{
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(AspectRatioFitter))]
    public class ResourceImageLoader : MonoBehaviour, ILayoutSelfController
    {

        public string m_FileName;

        private RawImage _RawImage;

        public enum AutoSizeMode
        {
            None, NativeSize, WidthControlHeight, HeightControlWidth
        }

        public AutoSizeMode m_AutoSizeMode;

        private RectTransform _RectTransform;

        private AspectRatioFitter _AspectRatioFitter;
        private void Awake()
        {
            _RawImage = GetComponent<RawImage>();
            _RectTransform = GetComponent<RectTransform>();
            _AspectRatioFitter = GetComponent<AspectRatioFitter>();

            ResourceManager.GetResource(m_FileName, ResourceManager.ResourceResponse.ResourceType.Texture).Subscribe(_ =>
            {

                if (_ != null)
                {

                    _RawImage.texture = _.m_Texture;
                    UpdateLayout();
                }
            }).AddTo(this);


        }

        private void UpdateLayout()
        {
            if (_RawImage == null) _RawImage = GetComponent<RawImage>();
            if (_AspectRatioFitter == null) _AspectRatioFitter = GetComponent<AspectRatioFitter>();
            if (_RectTransform == null) _RectTransform = GetComponent<RectTransform>();
            if (_RawImage == null || _RawImage.texture == null || _AspectRatioFitter == null) return;
            _AspectRatioFitter.aspectRatio = (float)_RawImage.texture.width / _RawImage.texture.height;

            _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.None;

            switch (m_AutoSizeMode)
            {

                case AutoSizeMode.NativeSize:
                    _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _RawImage.texture.width);
                    _RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _RawImage.texture.height);
                    break;
                case AutoSizeMode.WidthControlHeight:
                    _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

                    break;
                case AutoSizeMode.HeightControlWidth:
                    _AspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                    break;


            }
        }
        private void OnValidate()
        {
            UpdateLayout();
        }

        public void SetLayoutHorizontal()
        {
            UpdateLayout();
        }

        public void SetLayoutVertical()
        {
            UpdateLayout();

        }
    }

}
