using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheForest.Building.Core
{
    // ── Non-generic registry — Unity không cho [RuntimeInitializeOnLoad] trong generic class ──
    internal static class EventBusResetRegistry
    {
        static readonly List<Action> _resets = new();

        internal static void Register(Action reset)
        {
            if (!_resets.Contains(reset)) _resets.Add(reset);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnDomainReload()
        {
            foreach (var r in _resets) r?.Invoke();
            _resets.Clear();
        }
    }

    public static class EventBus<TEvent> where TEvent : struct
    {
        static readonly List<Action<TEvent>> _handlers = new(8);
        static readonly List<Action<TEvent>> _toAdd = new(4);
        static readonly List<Action<TEvent>> _toRemove = new(4);
        static bool _busy;

        // Đăng ký reset vào registry khi class lần đầu được dùng
        static EventBus()
        {
            EventBusResetRegistry.Register(DomainReset);
        }

        public static void Subscribe(Action<TEvent> h)
        {
            if (h == null) return;
            if (_busy) { if (!_toAdd.Contains(h)) _toAdd.Add(h); return; }
            if (!_handlers.Contains(h)) _handlers.Add(h);
        }

        public static void Unsubscribe(Action<TEvent> h)
        {
            if (h == null) return;
            if (_busy) { if (!_toRemove.Contains(h)) _toRemove.Add(h); return; }
            _handlers.Remove(h);
        }

        public static void Raise(in TEvent e)
        {
            _busy = true;
            for (int i = 0, n = _handlers.Count; i < n; i++)
            {
                try { _handlers[i](e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _busy = false;
            foreach (var h in _toRemove) _handlers.Remove(h);
            _toRemove.Clear();
            foreach (var h in _toAdd) { if (!_handlers.Contains(h)) _handlers.Add(h); }
            _toAdd.Clear();
        }

        public static int SubscriberCount => _handlers.Count;

        // Không còn [RuntimeInitializeOnLoad] ở đây nữa
        static void DomainReset()
        {
            _handlers.Clear();
            _toAdd.Clear();
            _toRemove.Clear();
            _busy = false;
        }
    }
}