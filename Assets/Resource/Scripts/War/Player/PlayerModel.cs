using System;
using UnityEngine;

[Serializable]
public class PlayerModel : ModelBase
{
    public float hp = 100f;
    public float maxHp = 100f;
    public Vector3 position;
    public Quaternion rotation;
    public bool isDead;
    public bool isCrouching;
    public bool isSprinting;
    public float invincibleTimeLeft;

    public float HpRatio => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;

    public override void Reset()
    {
        hp = maxHp;
        isDead = false;
        isCrouching = false;
        isSprinting = false;
        invincibleTimeLeft = 0f;
    }
}
