using UnityEngine;

public partial class SimulationManager
{
    private void SimulateAgents()
    {
        int stride = Mathf.Max(1, agentUpdateStride);
        agentUpdateOffset = (agentUpdateOffset + 1) % stride;

        for (int i = agentUpdateOffset; i < agents.Length; i += stride)
        {
            SlimeAgent agent = agents[i];

            if (!agent.alive)
                continue;

            RuntimeSpecies dna = runtimeSpecies[agent.speciesIndex];

            float dt = fixedSimulationDt;
            float energyRatio = agent.energy / dna.energyCapacity;
            bool hungry = energyRatio < dna.satiationThreshold;
            bool resting = !hungry && !panicMode;
            float activityMultiplier = resting ? 0.25f : 1f;

            if (agent.pauseTimer > 0f)
            {
                agent.pauseTimer -= dt;
                agents[i] = agent;
                continue;
            }

            Vector2 forwardDirection = AngleToDirection(agent.angle);

            if (hungry)
            {
                Vector2 leftDirection = RotateDirection(forwardDirection, -dna.sensorAngle);
                Vector2 rightDirection = RotateDirection(forwardDirection, dna.sensorAngle);

                float leftSense = SenseEnvironment(agent, dna, leftDirection);
                float forwardSense = SenseEnvironment(agent, dna, forwardDirection);
                float rightSense = SenseEnvironment(agent, dna, rightDirection);

                if (leftSense > forwardSense && leftSense > rightSense)
                    agent.angle -= dna.turnSpeed * dt;
                else if (rightSense > forwardSense && rightSense > leftSense)
                    agent.angle += dna.turnSpeed * dt;

                forwardDirection = AngleToDirection(agent.angle);
            }

            Vector2 oldPosition = agent.position;
            agent.position += forwardDirection * dna.speed * activityMultiplier * dt;

            KeepAgentInsideBounds(ref agent);

            if (IsObstacleAt(agent.position))
            {
                agent.position = oldPosition;
                agent.angle += 180f + Random.Range(-60f, 60f);
            }

            float trailActivityMultiplier = resting ? 0.25f : 1f;
            DepositTrail(agent, dna, dt, trailActivityMultiplier);
            DepositDanger(agent, dna, dt);

            TryAttackPrey(ref agent, dna, dt);
            EatCorpse(ref agent, dna, dt);
            EatFood(ref agent, dt);

            agent.energy -= dna.movementEnergyCost * activityMultiplier * dt;
            agent.energy -= dna.trailEnergyCost * activityMultiplier * dt;

            TryReproduce(ref agent, dna);

            agent.age += dt;

            if (agent.energy <= 0f || agent.age >= dna.maxAge)
            {
                float corpseEnergy = Mathf.Max(
                    agent.energy,
                    dna.startEnergy * corpseBodyEnergyMultiplier,
                    minimumCorpseEnergy
                );

                SpawnCorpse(agent.position, corpseEnergy, agent.speciesIndex);
                totalSpeciesDeaths[agent.speciesIndex]++;
                cachedSpeciesDeaths[agent.speciesIndex]++;
                agent.alive = false;

                UpdateSpeciesExtinctionState(agent.speciesIndex);
            }

            agents[i] = agent;
        }
    }

    private float SenseEnvironment(
        SlimeAgent agent,
        RuntimeSpecies dna,
        Vector2 sensorDirection
    )
    {
        if (agent.speciesIndex < 0 || agent.speciesIndex >= activeSpeciesCount)
            return 0f;

        if (dna == null || !dna.active)
            return 0f;

        if (dna.foodPreferences == null)
            return 0f;

        float energyRatio = agent.energy / dna.energyCapacity;

        if (energyRatio >= dna.satiationThreshold)
            return 0f;

        Vector2 sensorPosition =
            agent.position + sensorDirection * dna.sensorDistance;

        if (!WorldToGrid(sensorPosition, out int x, out int y))
            return -10f;

        if (obstacleGrid[x, y])
            return -10f;

        float hungerMultiplier =
            energyRatio < dna.hungerThreshold ? 3f : 1f;

        float ownTrail = trailGrid[x, y, agent.speciesIndex];
        float foreignTrail = 0f;

        if (Mathf.Abs(dna.foreignTrailRepulsion) > 0.001f)
        {
            for (int s = 0; s < activeSpeciesCount; s++)
            {
                if (s == agent.speciesIndex)
                    continue;

                foreignTrail += trailGrid[x, y, s];
            }
        }

        float trailSignal =
            ownTrail * dna.ownTrailAttraction -
            foreignTrail * dna.foreignTrailRepulsion;

        float foodSignal = 0f;

        if (dna.canEatFood)
            foodSignal += GetPreferredFoodSignalAt(x, y, dna) * hungerMultiplier;

        if (dna.canEatCorpses)
            foodSignal += GetCorpseSignalAt(sensorPosition, dna) * hungerMultiplier;

        if (dna.canHuntPrey)
            foodSignal += GetPreySignalAt(
                sensorPosition,
                dna,
                agent.speciesIndex
            ) * hungerMultiplier;

        float dangerSignal = GetDangerSignalAt(sensorPosition, dna);

        return trailSignal + foodSignal - dangerSignal;
    }

    private void DepositTrail(
        SlimeAgent agent,
        RuntimeSpecies dna,
        float dt,
        float multiplier
    )
    {
        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        if (obstacleGrid[x, y])
            return;

        int s = agent.speciesIndex;

        if (s < 0 || s >= activeSpeciesCount)
            return;

        trailGrid[x, y, s] += dna.trailStrength * multiplier * dt;
        trailGrid[x, y, s] = Mathf.Clamp01(trailGrid[x, y, s]);
    }

    private void EatFood(ref SlimeAgent agent, float dt)
    {
        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        if (obstacleGrid[x, y])
            return;

        if (agent.speciesIndex < 0 || agent.speciesIndex >= activeSpeciesCount)
            return;

        RuntimeSpecies dna = runtimeSpecies[agent.speciesIndex];

        if (dna == null || !dna.active)
            return;

        float energyRatio = agent.energy / dna.energyCapacity;

        if (energyRatio >= dna.satiationThreshold)
            return;

        int bestFoodType = GetBestFoodTypeAt(x, y, dna);

        if (bestFoodType < 0 || bestFoodType >= foodGrid.GetLength(2))
            return;

        FoodType foodType = foodTypes[bestFoodType];

        float available = foodGrid[x, y, bestFoodType];
        float eaten = Mathf.Min(foodEatAmount * dt, available);

        foodGrid[x, y, bestFoodType] -= eaten;

        agent.energy = Mathf.Min(
            dna.energyCapacity,
            agent.energy + eaten * foodGain * foodType.energyValue
        );

        if (eaten > 0f)
        {
            agent.angle += 180f + Random.Range(-30f, 30f);
            agent.pauseTimer = Random.Range(dna.eatPauseMin, dna.eatPauseMax);
        }
    }

    private int GetBestFoodTypeAt(int x, int y, RuntimeSpecies speciesData)
    {
        int bestFoodType = -1;
        float bestValue = 0f;

        for (int f = 0; f < foodTypes.Length; f++)
        {
            float value =
                foodGrid[x, y, f] *
                foodTypes[f].energyValue *
                speciesData.foodPreferences[f];

            if (value > bestValue)
            {
                bestValue = value;
                bestFoodType = f;
            }
        }

        return bestValue > 0f ? bestFoodType : -1;
    }

    private float GetPreferredFoodSignalAt(
        int x,
        int y,
        RuntimeSpecies speciesData
    )
    {
        float signal = 0f;

        for (int f = 0; f < foodTypes.Length; f++)
        {
            signal +=
                foodGrid[x, y, f] *
                foodTypes[f].energyValue *
                speciesData.foodPreferences[f];
        }

        return signal;
    }

    private int FindFreeAgentSlot()
    {
        if (agents == null || agents.Length == 0)
            return -1;

        int length = agents.Length;

        for (int step = 0; step < length; step++)
        {
            int index = (nextFreeAgentSearchIndex + step) % length;

            if (!agents[index].alive)
            {
                nextFreeAgentSearchIndex = (index + 1) % length;
                return index;
            }
        }

        return -1;
    }

    private void TryReproduce(ref SlimeAgent parent, RuntimeSpecies dna)
    {
        if (parent.energy < dna.reproductionThreshold)
            return;

        if (parent.age < dna.minReproductionAge)
            return;

        if (dna.reproductionThreshold <= dna.startEnergy)
            return;

        int i = FindFreeAgentSlot();

        if (i < 0)
            return;

        parent.energy *= 0.5f;

        Vector2 childPosition =
            parent.position + Random.insideUnitCircle * 0.5f;

        if (IsObstacleAt(childPosition))
            childPosition = parent.position;

        agents[i] = new SlimeAgent
        {
            position = childPosition,
            angle = Random.Range(0f, 360f),
            speciesIndex = parent.speciesIndex,
            age = 0f,
            energy = parent.energy,
            alive = true,
            pauseTimer = 0f
        };

        totalSpeciesBirths[parent.speciesIndex]++;
        cachedSpeciesBirths[parent.speciesIndex]++;

        if (mutationEnabled && Random.value < dna.mutationChance)
        {
            RuntimeSpecies childSpecies = CloneSpecies(dna);

            MutateSpecies(childSpecies);
            mutationEventCount++;

            bool canSpeciate =
                speciationEnabled &&
                childSpecies.mutationDistance >= speciationThreshold &&
                globalSpeciationTimer <= 0f &&
                speciesSpeciationCooldowns[parent.speciesIndex] <= 0f;

            if (canSpeciate)
            {
                int newSlot = FindFreeSpeciesSlot();

                if (newSlot >= 0)
                {
                    childSpecies.active = true;
                    childSpecies.speciesName =
                        GenerateSpeciesName(childSpecies);
                    childSpecies.color = GetMutatedColor(dna.color);

                    runtimeSpecies[newSlot] = childSpecies;
                    agents[i].speciesIndex = newSlot;

                    if (newSlot >= activeSpeciesCount)
                        activeSpeciesCount = newSlot + 1;

                    agents[i].energy = Mathf.Min(
                        childSpecies.energyCapacity,
                        childSpecies.startEnergy * founderEnergyMultiplier
                    );

                    SpawnAgentsAt(
                        agents[i].position,
                        newSlot,
                        founderExtraCount,
                        1.5f
                    );

                    speciationEventCount++;

                    globalSpeciationTimer = globalSpeciationCooldown;
                    speciesSpeciationCooldowns[parent.speciesIndex] =
                        speciesSpeciationCooldown;
                }
            }
        }
    }

    private void TryAttackPrey(
        ref SlimeAgent hunter,
        RuntimeSpecies dna,
        float dt
    )
    {
        if (dna.preyPreference < preyScavengerThreshold)
            return;

        float energyRatio = hunter.energy / dna.energyCapacity;

        if (energyRatio >= dna.satiationThreshold)
            return;

        if (!WorldToGrid(hunter.position, out int cx, out int cy))
            return;

        float nearbyPreyEnergy = 0f;

        for (int x = cx - 1; x <= cx + 1; x++)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                nearbyPreyEnergy += totalPreyGrid[x, y];
            }
        }

        if (nearbyPreyEnergy <= 0.01f)
            return;

        int radiusCells = 2;
        int bestTarget = -1;
        float bestDistanceSqr = preyAttackRange * preyAttackRange;

        for (int x = cx - radiusCells; x <= cx + radiusCells; x++)
        {
            for (int y = cy - radiusCells; y <= cy + radiusCells; y++)
            {
                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                int targetIndex = agentIndexGrid[x, y];

                if (targetIndex < 0)
                    continue;

                if (!agents[targetIndex].alive)
                    continue;

                if (agents[targetIndex].speciesIndex == hunter.speciesIndex)
                    continue;

                float distanceSqr =
                    (hunter.position - agents[targetIndex].position)
                    .sqrMagnitude;

                if (distanceSqr > bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                bestTarget = targetIndex;
            }
        }

        if (bestTarget < 0)
            return;

        SlimeAgent prey = agents[bestTarget];

        float stolen = preyAttackAmount * dt;
        stolen = Mathf.Min(stolen, prey.energy);

        prey.energy -= stolen;

        hunter.energy = Mathf.Min(
            dna.energyCapacity,
            hunter.energy + stolen * preyEnergyValue
        );

        hunter.energy -= preyAttackEnergyCost * dt;
        hunter.energy = Mathf.Max(0f, hunter.energy);

        if (prey.energy <= preyKillThreshold)
        {
            RuntimeSpecies preyDna = runtimeSpecies[prey.speciesIndex];

            float corpseEnergy = Mathf.Max(
                prey.energy,
                preyDna.startEnergy * corpseBodyEnergyMultiplier,
                minimumCorpseEnergy
            );

            SpawnCorpse(prey.position, corpseEnergy, prey.speciesIndex);
            totalSpeciesDeaths[prey.speciesIndex]++;
            cachedSpeciesDeaths[prey.speciesIndex]++;
            prey.alive = false;
        }

        agents[bestTarget] = prey;

        hunter.pauseTimer = Random.Range(
            dna.eatPauseMin,
            dna.eatPauseMax
        );
    }

    private float GetPreySignalAt(
        Vector2 sensorPosition,
        RuntimeSpecies dna,
        int ownSpeciesIndex
    )
    {
        if (!WorldToGrid(sensorPosition, out int x, out int y))
            return 0f;

        float preyEnergy = totalPreyGrid[x, y];

        if (ownSpeciesIndex >= 0 && ownSpeciesIndex < activeSpeciesCount)
            preyEnergy -= preyGrid[x, y, ownSpeciesIndex];

        return Mathf.Max(0f, preyEnergy) * dna.preyPreference;
    }

    private void KeepAgentInsideBounds(ref SlimeAgent agent)
    {
        float halfX = simulationArea.x * 0.5f;
        float halfY = simulationArea.y * 0.5f;

        bool hitWall = false;

        if (agent.position.x < -halfX)
        {
            agent.position.x = -halfX;
            hitWall = true;
        }
        else if (agent.position.x > halfX)
        {
            agent.position.x = halfX;
            hitWall = true;
        }

        if (agent.position.y < -halfY)
        {
            agent.position.y = -halfY;
            hitWall = true;
        }
        else if (agent.position.y > halfY)
        {
            agent.position.y = halfY;
            hitWall = true;
        }

        if (hitWall)
            agent.angle = Random.Range(0f, 360f);
    }

    private Vector2 AngleToDirection(float angle)
    {
        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );
    }

    private Vector2 RotateDirection(Vector2 dir, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            dir.x * cos - dir.y * sin,
            dir.x * sin + dir.y * cos
        );
    }

    private void RebuildPreyGrid()
    {
        if (preyGrid == null || agentIndexGrid == null || agents == null)
            return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                agentIndexGrid[x, y] = -1;
                totalPreyGrid[x, y] = 0f;

                for (int s = 0; s < activeSpeciesCount; s++)
                    preyGrid[x, y, s] = 0f;
            }
        }

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (!WorldToGrid(agents[i].position, out int x, out int y))
                continue;

            int speciesIndex = agents[i].speciesIndex;

            if (speciesIndex < 0 || speciesIndex >= MaxSpecies)
                continue;

            totalPreyGrid[x, y] += agents[i].energy;
            preyGrid[x, y, speciesIndex] += agents[i].energy;

            if (agentIndexGrid[x, y] < 0)
                agentIndexGrid[x, y] = i;
        }
    }
}