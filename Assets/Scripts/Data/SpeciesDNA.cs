using UnityEngine;

[CreateAssetMenu(fileName = "SpeciesDNA", menuName = "Terrarium/Species DNA")]
public class SpeciesDNA : ScriptableObject
{
    [Header("Movement")]
    public float speed = 2f;
    public float turnSpeed = 90f;

    [Header("Sensors")]
    public float sensorDistance = 1.5f;
    public float sensorAngle = 35f;

    [Header("Trail")]
    public float trailStrength = 1f;
    public float ownTrailAttraction = 1f;
    public float foreignTrailRepulsion = 0.5f;

    [Header("Life")]
    public float maxAge = 120f;
    public float startEnergy = 100f;
    public int startingPopulation = 1000;
    public float movementEnergyCost = 0.1f;
    public float trailEnergyCost = 0.05f;
    public float eatPauseMin = 0.05f;
    public float eatPauseMax = 0.15f;


    [Header("Diet")]
    public float greenPreference = 1f;
    public float brownPreference = 1f;
    public float corpsePreference = 0f;
    public float preyPreference = 0f;

    [Header("Reproduction")]
    public float reproductionThreshold = 200f;
    public float mutationChance = 0.02f;
    public float minReproductionAge = 10f;

    [Header("Visual")]
    public Color speciesColor = Color.cyan;
}