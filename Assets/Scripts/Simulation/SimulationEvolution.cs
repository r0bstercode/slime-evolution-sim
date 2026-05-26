using UnityEngine;

public partial class SimulationManager
{
    private void MutateSpecies(RuntimeSpecies child)
    {
        if (!mutationEnabled)
            return;

        child.speed += Random.Range(-mutationStrength, mutationStrength);
        child.turnSpeed += Random.Range(-mutationStrength * 100f, mutationStrength * 100f);

        child.energyCapacity += Random.Range(
           -mutationStrength * 100f,
            mutationStrength * 100f
        );

        child.energyCapacity = Mathf.Clamp(
            child.energyCapacity,
            child.startEnergy,
            50000f
        );

        child.sensorDistance += Random.Range(-mutationStrength, mutationStrength);
        child.sensorAngle += Random.Range(-mutationStrength * 30f, mutationStrength * 30f);

        child.trailStrength += Random.Range(-mutationStrength, mutationStrength);
        child.ownTrailAttraction += Random.Range(-mutationStrength, mutationStrength);
        child.foreignTrailRepulsion += Random.Range(-mutationStrength, mutationStrength);

        child.maxAge += Random.Range(-mutationStrength * 100f, mutationStrength * 100f);
        child.startEnergy += Random.Range(-mutationStrength * 50f, mutationStrength * 50f);

        child.movementEnergyCost += Random.Range(-mutationStrength * 0.05f, mutationStrength * 0.05f);
        child.trailEnergyCost += Random.Range(-mutationStrength * 0.05f, mutationStrength * 0.05f);

        child.eatPauseMin += Random.Range(-mutationStrength * 0.2f, mutationStrength * 0.2f);
        child.eatPauseMax += Random.Range(-mutationStrength * 0.2f, mutationStrength * 0.2f);

        child.reproductionThreshold += Random.Range(
            -mutationStrength * 100f,
            mutationStrength * 100f
        );

        child.minReproductionAge += Random.Range(
            -mutationStrength * 20f,
            mutationStrength * 20f
        );

        if (child.foodPreferences != null)
        {
            for (int i = 0; i < child.foodPreferences.Length; i++)
            {
                child.foodPreferences[i] += Random.Range(
                    -mutationStrength,
                    mutationStrength
                );

                child.foodPreferences[i] = Mathf.Clamp(
                    child.foodPreferences[i],
                    0f,
                    5f
                );
            }
        }

        child.corpsePreference += Random.Range(
            -mutationStrength,
            mutationStrength
        );

        child.corpsePreference = Mathf.Clamp(
            child.corpsePreference,
            0f,
            10f
        );

        child.preyPreference += Random.Range(
            -mutationStrength,
            mutationStrength
        );

        child.preyPreference = Mathf.Clamp(
            child.preyPreference,
            0f,
            10f
        );

        ClampSpeciesTraits(child);
        CalculateMutationDistance(child);
        child.generation += 1;
        child.canEatFood =
            child.foodPreferences != null &&
            (
                child.foodPreferences[0] > 0.01f ||
                child.foodPreferences[1] > 0.01f
            );

        child.canEatCorpses =
            child.corpsePreference >= 1.5f;

        child.canHuntPrey =
            child.preyPreference >= 1.5f;
    }

    private void ClampSpeciesTraits(RuntimeSpecies child)
    {
        child.speed = Mathf.Clamp(child.speed, 0.2f, 8f);
        child.turnSpeed = Mathf.Clamp(child.turnSpeed, 20f, 360f);

        child.sensorDistance = Mathf.Clamp(child.sensorDistance, 0.5f, 20f);
        child.sensorAngle = Mathf.Clamp(child.sensorAngle, 5f, 180f);

        child.trailStrength = Mathf.Clamp(child.trailStrength, 0f, 2f);
        child.ownTrailAttraction = Mathf.Clamp(child.ownTrailAttraction, -2f, 2f);
        child.foreignTrailRepulsion = Mathf.Clamp(child.foreignTrailRepulsion, 0f, 3f);

        child.maxAge = Mathf.Clamp(child.maxAge, 30f, 5000f);
        child.startEnergy = Mathf.Clamp(child.startEnergy, 10f, 1000f);

        child.energyCapacity = Mathf.Clamp(
            child.energyCapacity,
            child.startEnergy * 1.5f,
            50000f
        );

        child.movementEnergyCost = Mathf.Clamp(child.movementEnergyCost, 0.001f, 5f);
        child.trailEnergyCost = Mathf.Clamp(child.trailEnergyCost, 0f, 2f);

        child.eatPauseMin = Mathf.Clamp(child.eatPauseMin, 0f, 10f);
        child.eatPauseMax = Mathf.Clamp(child.eatPauseMax, child.eatPauseMin, 20f);

        child.minReproductionAge = Mathf.Clamp(child.minReproductionAge, 1f, 300f);

        child.reproductionThreshold = Mathf.Clamp(
            child.reproductionThreshold,
            child.startEnergy * 1.2f,
            child.energyCapacity * 0.9f
        );

        child.mutationChance = Mathf.Clamp01(child.mutationChance);

        child.hungerThreshold = Mathf.Clamp(child.hungerThreshold, 0.05f, 0.8f);

        child.satiationThreshold = Mathf.Clamp(
            child.satiationThreshold,
            child.hungerThreshold + 0.05f,
            0.98f
        );

        if (child.foodPreferences != null)
        {
            for (int i = 0; i < child.foodPreferences.Length; i++)
            {
                child.foodPreferences[i] = Mathf.Clamp(
                    child.foodPreferences[i],
                    0f,
                    5f
                );
            }
        }

        child.corpsePreference = Mathf.Clamp(child.corpsePreference, 0f, 10f);
        child.preyPreference = Mathf.Clamp(child.preyPreference, 0f, 10f);
    }

    private float CalculateMutationDistance(RuntimeSpecies speciesData)
    {
        if (speciesData == null || speciesData.sourceDNA == null)
            return 0f;

        float distance = 0f;

        distance += Mathf.Abs(speciesData.speed - speciesData.sourceDNA.speed);
        distance += Mathf.Abs(speciesData.turnSpeed - speciesData.sourceDNA.turnSpeed) / 100f;

        distance += Mathf.Abs(speciesData.sensorDistance - speciesData.sourceDNA.sensorDistance);
        distance += Mathf.Abs(speciesData.sensorAngle - speciesData.sourceDNA.sensorAngle) / 30f;

        distance += Mathf.Abs(speciesData.trailStrength - speciesData.sourceDNA.trailStrength);
        distance += Mathf.Abs(speciesData.ownTrailAttraction - speciesData.sourceDNA.ownTrailAttraction);
        distance += Mathf.Abs(speciesData.foreignTrailRepulsion - speciesData.sourceDNA.foreignTrailRepulsion);

        distance += Mathf.Abs(speciesData.maxAge - speciesData.sourceDNA.maxAge) / 100f;
        distance += Mathf.Abs(speciesData.startEnergy - speciesData.sourceDNA.startEnergy) / 50f;

        distance += Mathf.Abs(speciesData.movementEnergyCost - speciesData.sourceDNA.movementEnergyCost) * 20f;
        distance += Mathf.Abs(speciesData.trailEnergyCost - speciesData.sourceDNA.trailEnergyCost) * 20f;

        distance += Mathf.Abs(speciesData.reproductionThreshold - speciesData.sourceDNA.reproductionThreshold) / 100f;

        speciesData.mutationDistance = distance;
        return distance;
    }

    private RuntimeSpecies CloneSpecies(RuntimeSpecies parent)
    {
        RuntimeSpecies clone = new RuntimeSpecies();

        clone.sourceDNA = parent.sourceDNA;

        clone.active = parent.active;
        clone.speciesName = parent.speciesName;
        clone.color = parent.color;

        clone.speed = parent.speed;
        clone.turnSpeed = parent.turnSpeed;

        clone.sensorDistance = parent.sensorDistance;
        clone.sensorAngle = parent.sensorAngle;

        clone.trailStrength = parent.trailStrength;
        clone.ownTrailAttraction = parent.ownTrailAttraction;
        clone.foreignTrailRepulsion = parent.foreignTrailRepulsion;

        clone.maxAge = parent.maxAge;
        clone.startEnergy = parent.startEnergy;
        clone.movementEnergyCost = parent.movementEnergyCost;
        clone.trailEnergyCost = parent.trailEnergyCost;

        clone.eatPauseMin = parent.eatPauseMin;
        clone.eatPauseMax = parent.eatPauseMax;

        clone.reproductionThreshold = parent.reproductionThreshold;
        clone.mutationChance = parent.mutationChance;

        clone.foodPreferences = parent.foodPreferences != null
            ? (float[])parent.foodPreferences.Clone()
            : null;

        clone.corpsePreference = parent.corpsePreference;
        clone.preyPreference = parent.preyPreference;

        clone.generation = parent.generation;
        clone.mutationDistance = parent.mutationDistance;

        return clone;
    }

    private string GenerateSpeciesName(RuntimeSpecies s)
    {
        float green = GetFoodPref(s, 0);
        float brown = GetFoodPref(s, 1);
        float corpse = s.corpsePreference;
        float prey = s.preyPreference;

        string prefix = GetTraitPrefix(s, green, brown, corpse, prey);
        string role = GetEcologicalRole(green, brown, corpse, prey);

        return prefix + " " + role;
    }

    private float GetFoodPref(RuntimeSpecies s, int index)
    {
        if (s.foodPreferences == null || index < 0 || index >= s.foodPreferences.Length)
            return 0f;

        return s.foodPreferences[index];
    }

    private string GetTraitPrefix(
    RuntimeSpecies s,
    float green,
    float brown,
    float corpse,
    float prey
)
    {
        if (prey >= 2.5f)
            return "Fang";

        if (corpse >= 2.5f)
            return "Carrion";

        if (s.speed >= 6f)
            return "Rapid";

        if (s.speed >= 4f)
            return "Swift";

        if (s.sensorDistance >= 14f)
            return "Keen";

        if (s.sensorDistance >= 10f)
            return "Sharp";

        if (s.sensorDistance <= 3f)
            return "Blind";

        if (brown > green && brown >= 1.2f)
            return "Bog";

        if (green >= brown && green >= 1.2f)
            return "Glow";

        if (corpse >= 1.5f)
            return "Decay";

        if (prey >= 1.5f)
            return "Stalking";

        return "Wild";
    }

    private string GetEcologicalRole(
        float green,
        float brown,
        float corpse,
        float prey
    )
    {
        if (prey >= 1.5f && prey >= corpse && prey >= green && prey >= brown)
            return "Hunter";

        if (corpse >= 1.5f && corpse >= prey && corpse >= green && corpse >= brown)
            return "Scavenger";

        if (green >= 1.2f && green >= brown && green >= corpse && green >= prey)
            return "Grazer";

        if (brown >= 1.2f && brown >= green && brown >= corpse && brown >= prey)
            return "Forager";

        if (prey >= 0.8f && corpse >= 0.8f)
            return "Ravager";

        if (green >= 0.8f && corpse >= 0.8f)
            return "Omnivore";

        return "Wanderer";
    }

    private Color GetMutatedColor(Color baseColor)
    {
        float r = Mathf.Clamp01(baseColor.r + Random.Range(-0.15f, 0.15f));
        float g = Mathf.Clamp01(baseColor.g + Random.Range(-0.15f, 0.15f));
        float b = Mathf.Clamp01(baseColor.b + Random.Range(-0.15f, 0.15f));

        return new Color(r, g, b, 1f);
    }

    private int FindFreeSpeciesSlot()
    {
        for (int i = 0; i < runtimeSpecies.Length; i++)
        {
            if (runtimeSpecies[i] == null)
                return i;

            if (!runtimeSpecies[i].active)
                return i;
        }

        return -1;
    }

    private void UpdateSpeciesExtinctionState(int speciesIndex)
    {
        if (speciesIndex < 0 || speciesIndex >= runtimeSpecies.Length)
            return;

        if (runtimeSpecies[speciesIndex] == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (agents[i].speciesIndex == speciesIndex)
                return;
        }

        runtimeSpecies[speciesIndex].active = false;
        extinctionEventCount++;
    }

    private bool IsSpeciesExtinct(int speciesIndex)
    {
        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (agents[i].speciesIndex == speciesIndex)
                return false;
        }

        return true;
    }

    private void RemoveSpecies(int speciesIndex)
    {
        if (speciesIndex < 0 || speciesIndex >= activeSpeciesCount)
            return;

        if (speciesIndex < 4)
            return; // starter schützen

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (agents[i].speciesIndex == speciesIndex)
                agents[i].alive = false;
        }

        CompactSpeciesSlots();

        if (selectedSpeciesIndex == speciesIndex)
            selectedSpeciesIndex = -1;
    }

    private void CompactSpeciesSlots()
    {
        int write = 0;

        for (int read = 0; read < activeSpeciesCount; read++)
        {
            RuntimeSpecies s = runtimeSpecies[read];

            if (s == null)
                continue;

            bool keepStarter = read < 4;
            bool hasPopulation = GetLiveSpeciesCount(read) > 0;

            if (!keepStarter && !hasPopulation)
                continue;

            if (write != read)
            {
                runtimeSpecies[write] = s;

                for (int a = 0; a < agents.Length; a++)
                {
                    if (!agents[a].alive)
                        continue;

                    if (agents[a].speciesIndex == read)
                        agents[a].speciesIndex = write;
                }
            }

            write++;
        }

        for (int i = write; i < activeSpeciesCount; i++)
        {
            runtimeSpecies[i] = null;
        }

        activeSpeciesCount = write;

        if (selectedSpeciesIndex >= activeSpeciesCount)
            selectedSpeciesIndex = -1;

        RefreshCachedStats();
    }

    private RuntimeSpecies CreatePresetSpecies(string presetName)
    {
        RuntimeSpecies s = new RuntimeSpecies();

        s.sourceDNA = null;
        s.active = true;
        s.speciesName = presetName;
        s.color = Random.ColorHSV(0f, 1f, 0.65f, 1f, 0.75f, 1f);

        s.speed = 3f;
        s.turnSpeed = 120f;
        s.sensorDistance = 5f;
        s.sensorAngle = 35f;

        s.trailStrength = 0.6f;
        s.ownTrailAttraction = 1f;
        s.foreignTrailRepulsion = 0.5f;

        s.maxAge = 1000f;
        s.startEnergy = 100f;
        s.energyCapacity = 300f;
        s.movementEnergyCost = 0.2f;
        s.trailEnergyCost = 0.02f;

        s.eatPauseMin = 0.1f;
        s.eatPauseMax = 0.4f;

        s.hungerThreshold = 0.25f;
        s.satiationThreshold = 0.9f;

        s.reproductionThreshold = 220f;
        s.minReproductionAge = 20f;
        s.mutationChance = 0.05f;

        s.foodPreferences = new float[2];

        if (presetName.Contains("Predator"))
        {
            s.foodPreferences[0] = 0f;
            s.foodPreferences[1] = 0f;
            s.corpsePreference = 0.5f;
            s.preyPreference = 2f;
        }
        else if (presetName.Contains("Scavenger"))
        {
            s.foodPreferences[0] = 0.1f;
            s.foodPreferences[1] = 0.5f;
            s.corpsePreference = 2f;
            s.preyPreference = 0f;
        }
        else if (presetName.Contains("Omnivore"))
        {
            s.foodPreferences[0] = 1.2f;
            s.foodPreferences[1] = 0.4f;
            s.corpsePreference = 0.8f;
            s.preyPreference = 0.8f;
        }
        else
        {
            s.foodPreferences[0] = 2f;
            s.foodPreferences[1] = 0.2f;
            s.corpsePreference = 0f;
            s.preyPreference = 0f;
        }

        ClampSpeciesTraits(s);

        return s;
    }

    private void AddPresetSpecies(string presetName, int founderCount)
    {
        int slot = FindFreeSpeciesSlot();

        if (slot < 0)
            return;

        RuntimeSpecies preset = CreatePresetSpecies(presetName);

        runtimeSpecies[slot] = preset;

        if (slot >= activeSpeciesCount)
            activeSpeciesCount = slot + 1;

        for (int i = 0; i < founderCount; i++)
        {
            SpawnAgentsAt(Vector2.zero, slot, 1, 5f);
        }

        RefreshCachedStats();
    }
}