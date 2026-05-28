using UnityEngine;

[System.Flags]
public enum FeedingMode
{
    None = 0,
    Food = 1,
    Corpses = 2,
    Prey = 4
}

[System.Serializable]
public class RuntimeSpecies
{
    public SpeciesDNA sourceDNA;
    public bool active;
    public string speciesName;
    public Color color;

    [Header("Movement")]
    public float speed;
    public float turnSpeed;

    [Header("Sensors")]
    public float sensorDistance;
    public float sensorAngle;
    public bool canEatFood;
    public bool canEatCorpses;
    public bool canHuntPrey;
    public float sensorCos;
    public float sensorSin;

    [Header("Trail")]
    public float trailStrength;
    public float ownTrailAttraction;
    public float foreignTrailRepulsion;

    [Header("Life")]
    public float maxAge;
    public float startEnergy;
    public float energyCapacity;
    public float movementEnergyCost;
    public float trailEnergyCost;
    public float eatPauseMin;
    public float eatPauseMax;
    public float hungerThreshold;
    public float satiationThreshold;

    [Header("Reproduction")]
    public float reproductionThreshold;
    public float mutationChance;
    public float minReproductionAge;

    [Header("Food Preference")]
    public float[] foodPreferences;

    [Header("Corpse Feeding")]
    public float corpsePreference;
    public float preyPreference;
    public FeedingMode feedingMode;

    [Header("Evolution Debug")]
    public int generation;
    public float mutationDistance;

    public void CopyFromDNA(SpeciesDNA dna)
    {
        sourceDNA = dna;
        
        active = true;
        speciesName = dna.name;
        color = dna.speciesColor;

        speed = dna.speed;
        turnSpeed = dna.turnSpeed;

        sensorDistance = dna.sensorDistance;
        sensorAngle = dna.sensorAngle;
        trailStrength = dna.trailStrength;

        ownTrailAttraction = dna.ownTrailAttraction;
        foreignTrailRepulsion = dna.foreignTrailRepulsion;

        maxAge = dna.maxAge;
        startEnergy = dna.startEnergy;
        energyCapacity = dna.startEnergy * 3f;
        movementEnergyCost = dna.movementEnergyCost;
        trailEnergyCost = dna.trailEnergyCost;
        eatPauseMin = dna.eatPauseMin;
        eatPauseMax = dna.eatPauseMax;

        reproductionThreshold = dna.reproductionThreshold;
        minReproductionAge = dna.minReproductionAge;
        mutationChance = dna.mutationChance;
        hungerThreshold = 60f;
        satiationThreshold = 100f;

        foodPreferences = new float[2];
        foodPreferences[0] = dna.greenPreference;
        foodPreferences[1] = dna.brownPreference;

        corpsePreference = dna.corpsePreference;
        preyPreference = dna.preyPreference;

        RefreshCachedValues();

 
        if (reproductionThreshold <= startEnergy)
            reproductionThreshold = startEnergy * 1.5f;

        reproductionThreshold = Mathf.Min(
            reproductionThreshold,
            energyCapacity * 0.9f
        );

        generation = 0;
        mutationDistance = 0f;
    }

    public void RefreshCachedValues()
    {
        float sensorRad = sensorAngle * Mathf.Deg2Rad;

        sensorCos = Mathf.Cos(sensorRad);
        sensorSin = Mathf.Sin(sensorRad);

        canEatFood =
            foodPreferences != null &&
            foodPreferences.Length >= 2 &&
            (
                foodPreferences[0] > 0.01f ||
                foodPreferences[1] > 0.01f
            );

        canEatCorpses =
            corpsePreference >= 1.5f ||
            preyPreference >= 1.5f;
        canHuntPrey = preyPreference >= 1.5f;

        feedingMode = FeedingMode.None;

        if (canEatFood)
            feedingMode |= FeedingMode.Food;

        if (canEatCorpses)
            feedingMode |= FeedingMode.Corpses;

        if (canHuntPrey)
            feedingMode |= FeedingMode.Prey;
    }
}