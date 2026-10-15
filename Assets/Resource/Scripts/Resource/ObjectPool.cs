using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ObjectPool<T> -- generic object pool.
// Used by Echo / projectiles / VFX.
// ============================================================
public class ObjectPool<T> where T : Component
{
    private readonly Queue<T> _pool = new Queue<T>();
    private readonly T _prefab;
    private readonly int _maxSize;
    private readonly Transform _root;

    public int Count => _pool.Count;

    public ObjectPool(T prefab, int initialSize, Transform root = null, int maxSize = 32)
    {
        _prefab = prefab;
        _maxSize = maxSize;
        _root = root;

        for (var i = 0; i < initialSize; i++)
        {
            var go = UnityEngine.Object.Instantiate(prefab, _root);
            go.gameObject.SetActive(false);
            _pool.Enqueue(go);
        }
    }

    public T Get()
    {
        T item = null;
        while (_pool.Count > 0)
        {
            item = _pool.Dequeue();
            if (item != null) break;
        }

        if (item == null)
            item = UnityEngine.Object.Instantiate(_prefab, _root);

        item.gameObject.SetActive(true);
        return item;
    }

    public void Return(T item)
    {
        if (item == null) return;
        if (_pool.Count >= _maxSize)
        {
            UnityEngine.Object.Destroy(item.gameObject);
            return;
        }
        item.gameObject.SetActive(false);
        _pool.Enqueue(item);
    }
}
