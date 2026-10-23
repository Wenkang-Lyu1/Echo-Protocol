using UnityEngine;

/// <summary>Stationary training target. Health belongs to TutorialControl, animation belongs here.</summary>
public sealed class TutorialEnemyView : MonoBehaviour, IDamageable
{
    private TutorialControl _owner;
    private int _index;
    private float _flash;
    private Renderer[] _renderers;
    private Collider _collider;
    private MaterialPropertyBlock _block;
    public float Hp => _owner == null ? 60 : _owner.GetModel().enemyHealth[_index];
    public bool IsDead => Hp <= 0;
    public void Bind(TutorialControl owner, int index)
    {
        _owner = owner; _index = index;
        _block = new MaterialPropertyBlock();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _collider = GetComponent<Collider>();
    }
    public void ApplyDamage(float amount) => _owner.ApplyEnemyDamage(_index, amount);
    public void ShowHit(bool dead)
    {
        _flash = .12f;
        if (dead) { _collider.enabled = false; foreach (var r in _renderers) r.enabled = false; }
    }
    public void Present(float dt)
    {
        _flash = Mathf.Max(0, _flash - dt);
        _block.SetColor("_Color", _flash > 0 ? new Color(1, .35f, .25f) : Color.white);
        foreach (var r in _renderers) r.SetPropertyBlock(_block);
    }
    public void Restore()
    {
        _flash = 0;
        _collider.enabled = true;
        foreach (var r in _renderers) r.enabled = true;
    }
}
