using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// AssetLoader -- event-driven asset loading.
//
//     EventCenter.Dispatch(Resource_LoadRequest, new LoadRequest(...))
//     -> Resource_Loaded event + callback, cached when asked
//
// Everything goes through Resources today. Swapping in Addressables later
// means changing this file only - no caller knows how the bytes arrive.
// ============================================================
[DefaultExecutionOrder(-850)]
public class AssetLoader : MonoSingleton<AssetLoader>
{
    [SerializeField] private bool useResources = true;

    private readonly Dictionary<string, UnityEngine.Object> _cache =
        new Dictionary<string, UnityEngine.Object>();

    protected override void Awake()
    {
        base.Awake();
        if (IsDuplicate) return;

        EventCenter.Instance.AddListener(GameEvent.Resource_LoadRequest, OnLoadRequest);
    }

    private void OnLoadRequest(GameEventArgs args)
    {
        if (args.data is LoadRequest req)
        {
            if (req.async) StartCoroutine(LoadAsync(req));
            else LoadSync(req);
        }
    }

    private void LoadSync(LoadRequest req)
    {
        if (_cache.TryGetValue(req.path, out var cached))
        {
            req.callback?.Invoke(cached);
            return;
        }

        UnityEngine.Object asset = null;
        if (useResources)
            asset = Resources.Load(req.path);

        if (asset != null && req.cache)
            _cache[req.path] = asset;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Resource_Loaded,
            data = new LoadResult(req.path, asset, req.userData)
        });
        req.callback?.Invoke(asset);
    }

    private IEnumerator LoadAsync(LoadRequest req)
    {
        if (_cache.TryGetValue(req.path, out var cached))
        {
            req.callback?.Invoke(cached);
            yield break;
        }

        UnityEngine.Object asset = null;
        if (useResources)
        {
            var request = Resources.LoadAsync(req.path);
            yield return request;
            asset = request.asset;
        }

        if (asset != null && req.cache)
            _cache[req.path] = asset;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Resource_Loaded,
            data = new LoadResult(req.path, asset, req.userData)
        });
        req.callback?.Invoke(asset);
    }

    public void Unload(string path)
    {
        if (!_cache.TryGetValue(path, out var asset)) return;
        if (useResources) Resources.UnloadAsset(asset);
        _cache.Remove(path);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Resource_Unload,
            data = new ValueArgs<string>(path)
        });
    }

    public void ClearCache() { _cache.Clear(); }
}

public readonly struct LoadRequest
{
    public readonly string path;
    public readonly bool async;
    public readonly bool cache;
    public readonly object userData;
    public readonly Action<UnityEngine.Object> callback;

    public LoadRequest(string p, bool a, bool c, object ud, Action<UnityEngine.Object> cb)
    {
        path = p; async = a; cache = c; userData = ud; callback = cb;
    }
}

public readonly struct LoadResult
{
    public readonly string path;
    public readonly UnityEngine.Object asset;
    public readonly object userData;
    public LoadResult(string p, UnityEngine.Object a, object ud) { path = p; asset = a; userData = ud; }
    public bool success => asset != null;
}
