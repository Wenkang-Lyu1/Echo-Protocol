using UnityEngine;

// ============================================================
// EnemyView -- visual presentation layer for an enemy.
// Reads data through EnemyControl.GetModel().
// ============================================================
public class EnemyView : ViewBase
{
    [SerializeField] private Animator animator;
    [SerializeField] private UnityEngine.UI.Slider hpBar;
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color alertColor = new Color(1f, 0.5f, 0.2f);
    [SerializeField] private Color chaseColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color normalColor = new Color(0.5f, 0.5f, 0.5f);

    EnemyModel _enemyModel;
    MaterialPropertyBlock _block;
    static readonly int _colorId = Shader.PropertyToID("_Color");

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.Enemy_Hurt, OnHurt);
        EventCenter.Instance.AddListener(GameEvent.Enemy_Die, OnDie);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Enemy_Hurt, OnHurt);
        EventCenter.Instance.RemoveListener(GameEvent.Enemy_Die, OnDie);
    }

    protected override void OnRefresh()
    {
        _enemyModel = _model as EnemyModel;
        _block = new MaterialPropertyBlock();
        SyncHp();
    }

    void Update()
    {
        SyncStateColor();
        if (hpBarRoot != null)
            hpBarRoot.transform.rotation = Camera.main != null
                ? Camera.main.transform.rotation
                : Quaternion.identity;
    }

    void SyncHp()
    {
        if (hpBar != null && _enemyModel != null)
            hpBar.value = _enemyModel.HpRatio;
    }

    void SyncStateColor()
    {
        if (bodyRenderer == null || _enemyModel == null) return;
        var c = _enemyModel.state == EnemyState.Chase ? chaseColor
            : _enemyModel.state == EnemyState.Alert ? alertColor
            : normalColor;
        _block.SetColor(_colorId, c);
        bodyRenderer.SetPropertyBlock(_block);
    }

    void OnHurt(GameEventArgs args) => SyncHp();

    void OnDie(GameEventArgs args)
    {
        if (hpBarRoot != null) hpBarRoot.SetActive(false);
    }
}
