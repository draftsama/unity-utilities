using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Modules.Utilities
{
    public class UIJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform m_BaseRectTransform;
        [SerializeField] private RectTransform m_HandleRectTransform;
        [SerializeField] private CanvasGroup m_CanvasGroup;

        [SerializeField] private float m_Radius = 100f;
        [SerializeField] private bool m_FollowPosition;
        [SerializeField] private bool m_ResetPosition;
        [SerializeField] private float m_IdleAlpha = 0.5f;

        private RectTransform _RectTransform;

        private int _PointerId;
        private Vector2 _BeginDragPos;
        private Vector2 _StartPos;

        private UnityEvent<Vector2> _JoystickDirectionEvent = new UnityEvent<Vector2>();

        void Start()
        {
            _RectTransform = transform as RectTransform;
            _StartPos = m_BaseRectTransform.anchoredPosition;
            if (m_CanvasGroup != null) m_CanvasGroup.alpha = m_IdleAlpha;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _PointerId = eventData.pointerId;

            if (m_FollowPosition)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_RectTransform, eventData.position, null,
                    out Vector2 localPoint);
                m_BaseRectTransform.anchoredPosition = localPoint;
            }

            _BeginDragPos = m_HandleRectTransform.anchoredPosition;

            if (m_CanvasGroup != null) m_CanvasGroup.alpha = 1;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_PointerId != eventData.pointerId) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_BaseRectTransform, eventData.position, null,
                out Vector2 localPoint);

            localPoint = Vector2.ClampMagnitude(localPoint, m_Radius);
            m_HandleRectTransform.anchoredPosition = localPoint;
            var dir = (m_HandleRectTransform.anchoredPosition - _BeginDragPos).normalized;
            _JoystickDirectionEvent?.Invoke(dir);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_PointerId != eventData.pointerId) return;

            _JoystickDirectionEvent?.Invoke(Vector2.zero);
            if (m_ResetPosition)
            {
                m_HandleRectTransform.anchoredPosition = Vector2.zero;
                m_BaseRectTransform.anchoredPosition = _StartPos;
            }
            else
            {
                m_HandleRectTransform.anchoredPosition = _BeginDragPos;
            }

            if (m_CanvasGroup != null) m_CanvasGroup.alpha = m_IdleAlpha;
        }
        


         public IUniTaskAsyncEnumerable<Vector2> OnJoystickDirection(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<Vector2>(_JoystickDirectionEvent, _token);
        }
    }
}