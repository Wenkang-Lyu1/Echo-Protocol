using TMPro;
using UnityEngine;

// ============================================================
// PlayerView -- visual presentation layer for the player.
// Reads data through PlayerControl.GetModel().
// Must never modify Model.
// ============================================================
public class PlayerView : ViewBase
{
    [SerializeField] private Animator animator;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private UnityEngine.UI.Slider hpBar;
    [SerializeField] private GameObject damageVignette;
    [SerializeField] private float damageFlashDuration = 0.3f;

    PlayerModel _playerModel;
    float _damageFlashTimer;

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.Player_Hurt, OnHurt);
        EventCenter.Instance.AddListener(GameEvent.Player_Died, OnDied);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Player_Hurt, OnHurt);
        EventCenter.Instance.RemoveListener(GameEvent.Player_Died, OnDied);
    }

    protected override void OnRefresh()
    {
        _playerModel = _model as PlayerModel;
        SyncHp();
    }

    void Update()
    {
        SyncAnimator();

        if (_damageFlashTimer > 0f)
        {
            _damageFlashTimer -= Time.deltaTime;
            if (damageVignette != null)
                damageVignette.SetActive(_damageFlashTimer > 0f);
        }
    }

    void SyncHp()
    {
        if (_playerModel == null) return;
        if (hpText != null) hpText.text = $"{Mathf.RoundToInt(_playerModel.hp)}";
        if (hpBar != null) hpBar.value = _playerModel.HpRatio;
    }

    void SyncAnimator()
    {
        if (animator == null || _playerModel == null) return;
        animator.SetBool("Crouch", _playerModel.isCrouching);
        animator.SetBool("Sprint", _playerModel.isSprinting);
        animator.SetBool("Dead", _playerModel.isDead);
    }

    void OnHurt(GameEventArgs args)
    {
        _damageFlashTimer = damageFlashDuration;
        if (damageVignette != null) damageVignette.SetActive(true);
        SyncHp();
    }

    void OnDied(GameEventArgs args)
    {
        if (animator != null) animator.SetTrigger("Die");
    }
}
