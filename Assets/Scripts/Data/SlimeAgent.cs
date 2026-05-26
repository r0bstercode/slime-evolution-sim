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
}