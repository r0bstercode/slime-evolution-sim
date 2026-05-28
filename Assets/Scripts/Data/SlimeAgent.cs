using UnityEngine;

[System.Serializable]
public struct SlimeAgent
{
    public Vector2 position;
    public float angle;
    public int speciesIndex;

    public float age;
    public float energy;
    public bool alive;

    public float pauseTimer;

    public int lockedCorpseIndex;

    public float hp;
    public float slowTimer;
    public float slowMultiplier;

    public float cachedLeftSense;
    public float cachedForwardSense;
    public float cachedRightSense;
    public bool senseCacheValid;

  



   
}