using System;
using UnityEngine;

[Serializable]
public class TutorialModel : ModelBase
{
    public int defeated;
    public int totalEnemies;
    public int shots;
    public int echoesCreated;
    public int activeEchoes;
    public float echoCooldown;
    public float recordedSeconds;
    public float hitFeedback;
    public float[] enemyHealth;
    public bool captured = true;
    public bool moved;
    public bool complete;
    public override void Reset()
    {
        defeated = shots = echoesCreated = activeEchoes = 0;
        echoCooldown = recordedSeconds = hitFeedback = 0;
        moved = complete = false;
        captured = true;
        enemyHealth = new float[totalEnemies];
        for (int i = 0; i < totalEnemies; i++) enemyHealth[i] = 60;
    }
}

public struct TutorialPose
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 origin;
    public Vector3 direction;
    public bool fired;
}
