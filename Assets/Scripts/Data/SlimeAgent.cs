using UnityEngine;

public enum AgentMode
{
    Foraging,
    ReturningHome,
    RestingAtNest,
    EatingFood,
}

[System.Serializable]
public struct SlimeAgent
{
    public Vector2 position;
    public float angle;
    public int speciesIndex;

    public float age;
    public float energy;
    public bool alive;

    public AgentMode mode;
    public int homeNestIndex;

    public float pauseTimer;

    public int lockedCorpseIndex;
    public int lockedFoodX;
    public int lockedFoodY;
    public int lockedFoodType;
    public float foodTrailTimer;
    public float foodTrailStrength;

    public float hp;
    public float slowTimer;
    public float slowMultiplier;

    public float cachedLeftSense;
    public float cachedForwardSense;
    public float cachedRightSense;
    public bool senseCacheValid;

  



   
}