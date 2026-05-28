using UnityEngine;

public partial class SimulationManager
{
    private void SimulateAgents()
    {
        int stride = Mathf.Max(1, agentUpdateStride);
        agentUpdateOffset = (agentUpdateOffset + 1) % stride;

        simulationSenseTick++;

        for (int i = agentUpdateOffset; i < agents.Length; i += stride)
        {
            SlimeAgent agent = agents[i];

            if (!agent.alive)
                continue;

            RuntimeSpecies dna = runtimeSpecies[agent.speciesIndex];

            float dt = fixedSimulationDt;

            bool hungry = agent.energy < dna.satiationThreshold;
            bool resting = !hungry && !panicMode;
            float activityMultiplier = resting ? 0.25f : 1f;

            if (agent.pauseTimer > 0f)
            {
                agent.pauseTimer -= dt;
                agents[i] = agent;
                continue;
            }

            if (agent.lockedCorpseIndex >= 0)
            {
                EatLockedCorpse(ref agent, dna, dt);
                agents[i] = agent;
                continue;
            }

            Vector2 forwardDirection = AngleToDirection(agent.angle);

            if (hungry)
            {
                int senseInterval = dna.canHuntPrey ? 2 : 3;

                bool shouldRescan =
                    !agent.senseCacheValid ||
                    ((i + simulationSenseTick) % senseInterval) == 0;

                if (shouldRescan)
                {
                    Vector2 leftDirection = RotateDirectionCached(
                        forwardDirection,
                        dna.sensorCos,
                        -dna.sensorSin
                    );

                    Vector2 rightDirection = RotateDirectionCached(
                        forwardDirection,
                        dna.sensorCos,
                        dna.sensorSin
                    );

                    agent.cachedLeftSense =
                        SenseEnvironment(agent, dna, leftDirection);

                    agent.cachedForwardSense =
                        SenseEnvironment(agent, dna, forwardDirection);

                    agent.cachedRightSense =
                        SenseEnvironment(agent, dna, rightDirection);

                    agent.senseCacheValid = true;
                }

                if (
                    agent.cachedLeftSense > agent.cachedForwardSense &&
                    agent.cachedLeftSense > agent.cachedRightSense
                )
                    agent.angle -= dna.turnSpeed * dt;
                else if (
                    agent.cachedRightSense > agent.cachedForwardSense &&
                    agent.cachedRightSense > agent.cachedLeftSense
                )
                    agent.angle += dna.turnSpeed * dt;

                forwardDirection = AngleToDirection(agent.angle);
            }

            float injuryMultiplier = 1f;

            if (agent.slowTimer > 0f)
            {
                injuryMultiplier = agent.slowMultiplier;
                agent.slowTimer -= dt;
            }

            Vector2 oldPosition = agent.position;
            agent.position +=
                forwardDirection *
                dna.speed *
                activityMultiplier *
                injuryMultiplier *
                dt;

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
            EatFood(ref agent, dna, dt);
            agent.energy -= dna.movementEnergyCost * activityMultiplier * dt;
            agent.energy -= dna.trailEnergyCost * activityMultiplier * dt;

            TryReproduce(ref agent, dna);

            agent.age += dt;

            float naturalDeathThreshold = 20f;

            if (agent.energy <= naturalDeathThreshold || agent.hp <= 0f || agent.age >= dna.maxAge)
            {
                float corpseEnergy = Mathf.Max(0f, agent.energy);

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
        float hungerMultiplier =
            agent.energy < dna.hungerThreshold ? 3f : 1f;

        Vector2 sensorPosition =
            agent.position + sensorDirection * dna.sensorDistance;

        if (!WorldToGrid(sensorPosition, out int x, out int y))
            return -10f;

        if (obstacleGrid[x, y])
            return -10f;

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
            -foreignTrail * dna.foreignTrailRepulsion;

        float foodSignal = 0f;

        switch (dna.feedingMode)
        {
            case FeedingMode.Food:
                foodSignal += SampleFoodSignalRadius(x, y, dna, 2) * hungerMultiplier;
                break;

            case FeedingMode.Corpses:
                foodSignal += SampleCorpseSignalRadius(x, y, dna, 2) * hungerMultiplier;
                break;

            case FeedingMode.Prey:
                foodSignal += SamplePreySignalRadius(x, y, dna, agent.speciesIndex, 2) * hungerMultiplier;
                break;

            case FeedingMode.Food | FeedingMode.Corpses:
                foodSignal += SampleFoodSignalRadius(x, y, dna, 2) * hungerMultiplier;
                foodSignal += SampleCorpseSignalRadius(x, y, dna, 2) * hungerMultiplier;
                break;

            case FeedingMode.Food | FeedingMode.Prey:
                foodSignal += SampleFoodSignalRadius(x, y, dna, 2) * hungerMultiplier;
                foodSignal += SamplePreySignalRadius(x, y, dna, agent.speciesIndex, 2) * hungerMultiplier;
                break;

            case FeedingMode.Corpses | FeedingMode.Prey:
                foodSignal += SampleCorpseSignalRadius(x, y, dna, 2) * hungerMultiplier;
                foodSignal += SamplePreySignalRadius(x, y, dna, agent.speciesIndex, 2) * hungerMultiplier;
                break;

            case FeedingMode.Food | FeedingMode.Corpses | FeedingMode.Prey:
                foodSignal += SampleFoodSignalRadius(x, y, dna, 2) * hungerMultiplier;
                foodSignal += SampleCorpseSignalRadius(x, y, dna, 2) * hungerMultiplier;
                foodSignal += SamplePreySignalRadius(x, y, dna, agent.speciesIndex, 2) * hungerMultiplier;
                break;
        }

        float dangerSignal = GetDangerSignalAt(x, y, dna);

        return trailSignal * 0.3f + foodSignal - dangerSignal;
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

    private void EatFood(ref SlimeAgent agent, RuntimeSpecies dna, float dt)
    {
        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        if (obstacleGrid[x, y])
            return;

        if (agent.speciesIndex < 0 || agent.speciesIndex >= activeSpeciesCount)
            return;

        if (agent.energy >= dna.satiationThreshold)
            return;

        int bestFoodType = GetBestFoodTypeAt(x, y, dna);

        if (bestFoodType < 0 || bestFoodType >= foodGrid.GetLength(2))
            return;

        float available = foodGrid[x, y, bestFoodType];
        float eaten = Mathf.Min(
            foodEatAmount,
            available
        );

        if (eaten <= 0f)
            return;

        foodGrid[x, y, bestFoodType] -= eaten;

        agent.energy = Mathf.Min(
            dna.energyCapacity,
            agent.energy + eaten
        );

        agent.angle += 180f + Random.Range(-30f, 30f);
        agent.pauseTimer = Random.Range(dna.eatPauseMin, dna.eatPauseMax);
    }

    private void EatLockedCorpse(ref SlimeAgent agent, RuntimeSpecies dna, float dt)
    {
        int index = agent.lockedCorpseIndex;

        if (index < 0 || index >= corpses.Length)
        {
            agent.lockedCorpseIndex = -1;
            return;
        }

        if (!corpses[index].active)
        {
            agent.lockedCorpseIndex = -1;
            return;
        }

        if (agent.energy >= dna.satiationThreshold)
        {
            agent.lockedCorpseIndex = -1;
            return;
        }

        Corpse corpse = corpses[index];

        agent.position = corpse.position;

        float eaten = Mathf.Min(
            corpseEatAmount,
            corpse.energy
        );

        if (eaten <= 0f)
        {
            agent.lockedCorpseIndex = -1;
            return;
        }

        corpse.energy -= eaten;

        if (WorldToGrid(corpse.position, out int x, out int y))
        {
            corpseGrid[x, y] = Mathf.Max(
                0f,
                corpseGrid[x, y] - eaten
            );
        }

        if (corpse.energy <= 0.01f)
        {
            corpse.active = false;
            agent.lockedCorpseIndex = -1;
        }

        corpses[index] = corpse;

        agent.energy = Mathf.Min(
            dna.energyCapacity,
            agent.energy + eaten
        );

        agent.pauseTimer = Random.Range(
            dna.eatPauseMin,
            dna.eatPauseMax
        );
    }

    private int GetBestFoodTypeAt(int x, int y, RuntimeSpecies speciesData)
{
    float greenValue =
        foodGrid[x, y, 0] *
        foodTypes[0].energyValue *
        speciesData.foodPreferences[0];

    float brownValue =
        foodGrid[x, y, 1] *
        foodTypes[1].energyValue *
        speciesData.foodPreferences[1];

    if (greenValue <= 0f && brownValue <= 0f)
        return -1;

    return greenValue >= brownValue ? 0 : 1;
}

    private float GetPreferredFoodSignalAt(
        int x,
        int y,
        RuntimeSpecies speciesData
    )
    {
        return
            foodGrid[x, y, 0] *
            foodTypes[0].energyValue *
            speciesData.foodPreferences[0]
            +
            foodGrid[x, y, 1] *
            foodTypes[1].energyValue *
            speciesData.foodPreferences[1];
    }

    private float SampleFoodSignalRadius(
    int centerX,
    int centerY,
    RuntimeSpecies dna,
    int radius
)
    {
        float signal = 0f;

        for (int ox = -radius; ox <= radius; ox++)
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                int x = centerX + ox;
                int y = centerY + oy;

                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                if (obstacleGrid[x, y])
                    continue;

                float distance = Mathf.Sqrt(ox * ox + oy * oy);
                if (distance > radius)
                    continue;

                float weight = 1f / (1f + distance);

                signal += GetPreferredFoodSignalAt(x, y, dna) * weight;
            }
        }

        return signal;
    }

    private float SampleCorpseSignalRadius(
        int centerX,
        int centerY,
        RuntimeSpecies dna,
        int radius
    )
    {
        float signal = 0f;

        for (int ox = -radius; ox <= radius; ox++)
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                int x = centerX + ox;
                int y = centerY + oy;

                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                if (obstacleGrid[x, y])
                    continue;

                float distance = Mathf.Sqrt(ox * ox + oy * oy);
                if (distance > radius)
                    continue;

                float weight = 1f / (1f + distance);

                signal += GetCorpseSignalAt(x, y, dna) * weight;
            }
        }

        return signal;
    }

    private float SamplePreySignalRadius(
        int centerX,
        int centerY,
        RuntimeSpecies dna,
        int ownSpeciesIndex,
        int radius
    )
    {
        float signal = 0f;

        for (int ox = -radius; ox <= radius; ox++)
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                int x = centerX + ox;
                int y = centerY + oy;

                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                if (obstacleGrid[x, y])
                    continue;

                float distance = Mathf.Sqrt(ox * ox + oy * oy);
                if (distance > radius)
                    continue;

                float weight = 1f / (1f + distance);

                signal += GetPreySignalAt(x, y, dna, ownSpeciesIndex) * weight;
            }
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
            pauseTimer = 0f,
            hp = 1f,
            slowTimer = 0f,
            slowMultiplier = 1f,
            lockedCorpseIndex = -1,

            cachedLeftSense = 0f,
            cachedForwardSense = 0f,
            cachedRightSense = 0f,
            senseCacheValid = false
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

        if (hunter.energy >= dna.satiationThreshold)
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

        float damage = preyAttackDamage * dt;

        prey.hp -= damage;
        prey.slowTimer = preyAttackSlowDuration;
        prey.slowMultiplier = preyAttackSlowMultiplier;

        hunter.energy -= preyAttackEnergyCost * dt;
        hunter.energy = Mathf.Max(0f, hunter.energy);

        if (prey.hp <= 0f)
        {
            float corpseEnergy = Mathf.Max(0f, prey.energy);

            int corpseIndex = SpawnCorpse(
                prey.position,
                corpseEnergy,
                prey.speciesIndex
            );

            totalSpeciesDeaths[prey.speciesIndex]++;
            cachedSpeciesDeaths[prey.speciesIndex]++;

            prey.alive = false;
            agents[bestTarget] = prey;

            if (corpseIndex >= 0)
            {
                hunter.position = prey.position;
                hunter.lockedCorpseIndex = corpseIndex;
                hunter.pauseTimer = 0f;
            }

            return;
        }

        agents[bestTarget] = prey;

        hunter.pauseTimer = Random.Range(
            dna.eatPauseMin,
            dna.eatPauseMax
        );
    }

    private float GetPreySignalAt(
    int x,
    int y,
    RuntimeSpecies dna,
    int ownSpeciesIndex
)
    {
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

    private Vector2 RotateDirectionCached(Vector2 dir, float cos, float sin)
    {
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