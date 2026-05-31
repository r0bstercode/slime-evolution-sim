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
            UpdateAgentState(ref agent, dna);

            float dt = fixedSimulationDt;

            bool shouldSense =
                agent.mode == AgentMode.Foraging ||
                agent.mode == AgentMode.ReturningHome;

            bool resting =
                agent.mode == AgentMode.RestingAtNest;

            float activityMultiplier = resting ? 0f : 1f;

            if (agent.mode == AgentMode.Foraging && !IsObstacleAt(agent.position))
            {
                DepositHomeTrail(agent, dna, fixedSimulationDt, 1f);
            }

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

            if (agent.mode == AgentMode.EatingFood)
            {
                agent.foodTrailStrength = 1f;
                DepositFoodTrail(agent, dna, dt, 1f);
                EatLockedFood(ref agent, dna);
                agents[i] = agent;
                continue;
            }

            Vector2 forwardDirection = AngleToDirection(agent.angle);

            if (shouldSense)
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

                float bestSense = agent.cachedForwardSense;
                float turnDirection = 0f;

                if (agent.cachedLeftSense > bestSense)
                {
                    bestSense = agent.cachedLeftSense;
                    turnDirection = -1f;
                }

                if (agent.cachedRightSense > bestSense)
                {
                    bestSense = agent.cachedRightSense;
                    turnDirection = 1f;
                }

                if (turnDirection != 0f)
                {
                    float turnBoost =
                        agent.mode == AgentMode.ReturningHome ? 4f : 2f;

                    agent.angle += turnDirection * dna.turnSpeed * turnBoost * dt;
                }

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
            bool movementBlocked = false;

            if (KeepAgentInsideBounds(ref agent))
            {
                movementBlocked = true;
                ClearNavigationTrailsAt(agent.position, agent.speciesIndex, 3);
            }

            

            if (IsObstacleAt(agent.position))
            {
                agent.position = oldPosition;
                agent.angle += 180f + Random.Range(-120f, 120f);
                agent.senseCacheValid = false;
                movementBlocked = true;

                ClearNavigationTrailsAt(agent.position, agent.speciesIndex, 2);
            }

            float trailActivityMultiplier = resting ? 0.25f : 1f;

            if (!movementBlocked)
            {
                DepositTrail(agent, dna, dt, trailActivityMultiplier);

                if (agent.mode == AgentMode.ReturningHome)
                {
                    if (agent.foodTrailStrength > 0.001f)
                    {
                        DepositFoodTrail(agent, dna, dt, trailActivityMultiplier);
                        agent.foodTrailStrength *= 0.995f;
                    }
                }
            }

            DepositDanger(agent, dna, dt);
            TryAttackPrey(ref agent, dna, dt);
            EatCorpse(ref agent, dna, dt);
            EatFood(ref agent, dna, dt);
            agent.energy -= dna.movementEnergyCost * activityMultiplier * dt;
            agent.energy -= dna.trailEnergyCost * activityMultiplier * dt;

            //TryReproduce(ref agent, dna);

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

    private void UpdateAgentState(ref SlimeAgent agent, RuntimeSpecies dna)
    {
        switch (agent.mode)
        {
            case AgentMode.Foraging:

                if (agent.energy >= nestReturnEnergyThreshold)
                {
                    agent.mode = AgentMode.ReturningHome;
                    agent.senseCacheValid = false;
                }

                break;

            case AgentMode.ReturningHome:

                if (agent.homeNestIndex < 0 ||
                    agent.homeNestIndex >= nests.Length)
                {
                    agent.mode = AgentMode.Foraging;
                    return;
                }

                Nest nest = nests[agent.homeNestIndex];

                if (!nest.active)
                {
                    agent.mode = AgentMode.Foraging;
                    return;
                }

                float dist =
                    Vector2.Distance(agent.position, nest.position);

                if (dist <= nestRadius)
                {
                    float depositedEnergy = Mathf.Max(
                        0f,
                        agent.energy - dna.startEnergy
                    );

                    nest.storedEnergy += depositedEnergy;

                    nests[agent.homeNestIndex] = nest;

                    agent.energy -= depositedEnergy;

                    agent.mode = AgentMode.RestingAtNest;
                    agent.pauseTimer = nestRestTime;

                    agent.senseCacheValid = false;
                }

                break;

            case AgentMode.RestingAtNest:

                if (agent.pauseTimer <= 0f)
                {
                    agent.mode = AgentMode.Foraging;

                    agent.lockedFoodX = -1;
                    agent.lockedFoodY = -1;
                    agent.lockedFoodType = -1;
                    agent.lockedCorpseIndex = -1;

                    agent.foodTrailTimer = 0f;
                    agent.senseCacheValid = false;
                }

                break;
        }
    }

    //    private void MoveTowardsNest(
    //    ref SlimeAgent agent,
    //    RuntimeSpecies dna,
    //    float dt
    //)
    //    {
    //        if (agent.homeNestIndex < 0 ||
    //            agent.homeNestIndex >= nests.Length)
    //            return;

    //        Nest nest = nests[agent.homeNestIndex];

    //        if (!nest.active)
    //            return;

    //        Vector2 dir =
    //            (nest.position - agent.position).normalized;

    //        float targetAngle =
    //            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

    //        agent.angle = Mathf.LerpAngle(
    //            agent.angle,
    //            targetAngle,
    //            dna.turnSpeed * dt * 0.08f
    //        );
    //    }

    private void ClearNavigationTrailsAt(Vector2 worldPos, int speciesIndex, int radius = 1)
    {
        if (!WorldToGrid(worldPos, out int cx, out int cy))
            return;

        for (int ox = -radius; ox <= radius; ox++)
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                int x = cx + ox;
                int y = cy + oy;

                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                if (homeTrailGrid != null)
                    homeTrailGrid[x, y, speciesIndex] = 0f;

                if (foodTrailGrid != null)
                    foodTrailGrid[x, y, speciesIndex] = 0f;

                if (homeDistanceGrid != null)
                    homeDistanceGrid[x, y, speciesIndex] = 0f;
            }
        }
    }

    private void DiffuseNavigationTrails(float dt)
    {
        navTrailUpdateTick++;

        if (navTrailUpdateTick < navTrailUpdateEveryNSteps)
            return;

        navTrailUpdateTick = 0;

        float scaledDt = dt * navTrailUpdateEveryNSteps;

        DiffuseTrailGrid(
            homeTrailGrid,
            nextHomeTrailGrid,
            homeTrailDecayRate,
            navTrailDiffusionRate * 0.2f,
            scaledDt,
            true
        );

        DecayFoodTrailGrid(scaledDt);
    }

    private void DecayFoodTrailGrid(float dt)
    {
        if (foodTrailGrid == null)
            return;

        float decayAmount = foodTrailDecayRate * dt;

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (obstacleGrid[x, y])
                    {
                        foodTrailGrid[x, y, s] = 0f;
                        continue;
                    }

                    float value = foodTrailGrid[x, y, s];

                    if (value > 0.15f)
                    {
                        value -= decayAmount;
                    }
                    else
                    {
                        value -= decayAmount * 0.05f;
                    }

                    foodTrailGrid[x, y, s] -= foodTrailDecayRate * dt;

                    if (foodTrailGrid[x, y, s] < 0.02f)
                        foodTrailGrid[x, y, s] = 0f;
                }
            }
        }
    }

    private void DiffuseTrailGrid(
    float[,,] source,
    float[,,] target,
    float decayRate,
    float diffusionRate,
    float dt,
    bool hardFade = false
    )
    {
        if (source == null || target == null)
            return;

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (obstacleGrid[x, y])
                    {
                        target[x, y, s] = 0f;
                        continue;
                    }

                    float center = source[x, y, s];

                    float neighborAverage =
                        (
                            source[x - 1, y, s] +
                            source[x + 1, y, s] +
                            source[x, y - 1, s] +
                            source[x, y + 1, s]
                        ) * 0.25f;

                    float value = Mathf.Lerp(
                        center,
                        neighborAverage,
                        diffusionRate * dt
                    );

                    if (decayRate >= 1f)
                    {
                        value -= decayRate * dt;
                    }
                    else
                    {
                        value *= Mathf.Exp(-decayRate * dt);
                    }

                    if (value < 0.02f)
                        value = 0f;

                    target[x, y, s] = Mathf.Clamp01(value);

                    target[x, y, s] = Mathf.Clamp01(value);
                }
            }
        }

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    source[x, y, s] = target[x, y, s];
                }
            }
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

        float dangerSignal = GetDangerSignalAt(x, y, dna);
        float foodSignal = 0f;
        float navTrailSignal = 0f;

        if (agent.speciesIndex >= 0 && agent.speciesIndex < activeSpeciesCount)
        {
            if (agent.mode == AgentMode.Foraging)
            {
                navTrailSignal = SampleTrailSignalRadius(
                    foodTrailGrid,
                    x,
                    y,
                    agent.speciesIndex,
                    4
                );
            }
            else if (agent.mode == AgentMode.ReturningHome)
            {
                navTrailSignal = SampleHomeTrailSignalRadius(
                    x,
                    y,
                    agent.speciesIndex,
                    8
                );

                if (navTrailSignal <= 0.001f)
                    return -100f;

                return navTrailSignal * 100f - dangerSignal;
            }
        }

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

        return
            navTrailSignal * 0.2f +
            foodSignal * 3f -
            dangerSignal;
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

        trailGrid[x, y, s] += dna.trailStrength * 0.1f * multiplier * dt;
        trailGrid[x, y, s] = Mathf.Clamp(trailGrid[x, y, s], 0f, 0.35f);
    }

    private void DepositHomeTrail(
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

        float add = dna.trailStrength * 0.05f * multiplier * dt;

        homeTrailGrid[x, y, s] += add * (1f - homeTrailGrid[x, y, s]);
        homeTrailGrid[x, y, s] = Mathf.Clamp01(homeTrailGrid[x, y, s]);
        float distanceToNest = 999999f;

        if (agent.homeNestIndex >= 0 &&
            agent.homeNestIndex < nests.Length &&
            nests[agent.homeNestIndex].active)
        {
            distanceToNest = Vector2.Distance(
                agent.position,
                nests[agent.homeNestIndex].position
            );
        }

        if (homeDistanceGrid != null)
        {
            float oldDistance = homeDistanceGrid[x, y, s];

            if (oldDistance <= 0f)
                homeDistanceGrid[x, y, s] = distanceToNest;
            else
                homeDistanceGrid[x, y, s] = Mathf.Min(oldDistance, distanceToNest);
        }
    }

    private void DepositFoodTrail(
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

        float value = agent.foodTrailStrength;

        if (value <= 0.001f)
            return;

        foodTrailGrid[x, y, s] = Mathf.Max(
            foodTrailGrid[x, y, s],
            value
        );
    }

    private void EatFood(ref SlimeAgent agent, RuntimeSpecies dna, float dt)
    {
        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        if (obstacleGrid[x, y])
            return;

        if (agent.speciesIndex < 0 || agent.speciesIndex >= activeSpeciesCount)
            return;

        if (agent.energy >= dna.energyCapacity * 0.95f)
        {
            agent.mode = AgentMode.ReturningHome;
            return;
        }

        int bestFoodType = GetBestFoodTypeAt(x, y, dna);

        if (bestFoodType < 0 || bestFoodType >= foodGrid.GetLength(2))
            return;

        float available = foodGrid[x, y, bestFoodType];

        if (available <= 0.01f)
            return;

        agent.mode = AgentMode.EatingFood;
        agent.lockedFoodX = x;
        agent.lockedFoodY = y;
        agent.lockedFoodType = bestFoodType;
        agent.senseCacheValid = false;
    }

    private void EatLockedFood(ref SlimeAgent agent, RuntimeSpecies dna)
    {
        if (agent.lockedFoodX < 0 ||
            agent.lockedFoodY < 0 ||
            agent.lockedFoodType < 0)
        {
            agent.mode = AgentMode.Foraging;
            return;
        }

        int x = agent.lockedFoodX;
        int y = agent.lockedFoodY;
        int foodType = agent.lockedFoodType;

        if (x >= gridWidth || y >= gridHeight || foodType >= foodGrid.GetLength(2))
        {
            agent.mode = AgentMode.Foraging;
            return;
        }

        if (agent.energy >= dna.energyCapacity * 0.95f)
        {
            agent.mode = AgentMode.ReturningHome;
            agent.angle += 180f + Random.Range(-20f, 20f);
            agent.foodTrailStrength = 1f;
            agent.senseCacheValid = false;
            return;
        }

        float available = foodGrid[x, y, foodType];

        if (available <= 0.01f)
        {
            if (agent.energy >= dna.energyCapacity * 0.95f)
            {
                agent.mode = AgentMode.ReturningHome;
                agent.angle += 180f + Random.Range(-20f, 20f);
                agent.foodTrailStrength = 1f;
            }
            else
            {
                agent.mode = AgentMode.Foraging;
            }

            agent.lockedFoodX = -1;
            agent.lockedFoodY = -1;
            agent.lockedFoodType = -1;
            agent.senseCacheValid = false;
            return;
        }

        float eaten = Mathf.Min(
            foodEatAmount,
            available,
            dna.energyCapacity - agent.energy
        );

        if (eaten <= 0f)
        {
            if (agent.energy >= dna.energyCapacity * 0.95f)
            {
                agent.mode = AgentMode.ReturningHome;
                agent.angle += 180f + Random.Range(-20f, 20f);
                agent.foodTrailStrength = 1f;
            }
            else
            {
                agent.mode = AgentMode.Foraging;
            }

            agent.lockedFoodX = -1;
            agent.lockedFoodY = -1;
            agent.lockedFoodType = -1;
            agent.senseCacheValid = false;
            return;
        }

        foodGrid[x, y, foodType] -= eaten;

        agent.energy = Mathf.Min(
            dna.energyCapacity,
            agent.energy + eaten
        );

        if (agent.energy >= dna.energyCapacity * 0.95f)
        {
            agent.mode = AgentMode.ReturningHome;
            agent.angle += 180f + Random.Range(-20f, 20f);
            agent.foodTrailStrength = 1f;

            agent.lockedFoodX = -1;
            agent.lockedFoodY = -1;
            agent.lockedFoodType = -1;
            agent.senseCacheValid = false;
        }

        agent.pauseTimer = Random.Range(
            dna.eatPauseMin,
            dna.eatPauseMax
        );
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

        if (agent.energy >= dna.energyCapacity * 0.95f)
        {
            agent.mode = AgentMode.ReturningHome;
            agent.lockedCorpseIndex = -1;
            agent.senseCacheValid = false;
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

        if (agent.energy >= dna.energyCapacity * 0.95f)
        {
            agent.mode = AgentMode.ReturningHome;
            agent.lockedCorpseIndex = -1;
            agent.senseCacheValid = false;
        }

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

    private float SampleHomeTrailSignalRadius(
    int centerX,
    int centerY,
    int speciesIndex,
    int radius
)
    {
        if (homeTrailGrid == null || homeDistanceGrid == null)
            return 0f;

        float bestScore = 0f;

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

                float localDistance = Mathf.Sqrt(ox * ox + oy * oy);

                if (localDistance > radius)
                    continue;

                float trail = homeTrailGrid[x, y, speciesIndex];

                if (trail <= 0.0001f)
                    continue;

                float nestDistance = homeDistanceGrid[x, y, speciesIndex];

                if (nestDistance <= 0f)
                    continue;

                float score =
                    1000f -
                    nestDistance * 100f -
                    localDistance + 2f;

                if (score > bestScore)
                    bestScore = score;
            }
        }

        return bestScore;
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

    private void DepositNestHomeTrails(float dt)
    {
        if (nests == null || homeTrailGrid == null)
            return;

        for (int i = 0; i < nests.Length; i++)
        {
            if (!nests[i].active)
                continue;

            if (!WorldToGrid(nests[i].position, out int cx, out int cy))
                continue;

            int s = nests[i].speciesIndex;

            if (s < 0 || s >= activeSpeciesCount)
                continue;

            int radius = 4;

            for (int ox = -radius; ox <= radius; ox++)
            {
                for (int oy = -radius; oy <= radius; oy++)
                {
                    int x = cx + ox;
                    int y = cy + oy;

                    if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                        continue;

                    if (obstacleGrid[x, y])
                        continue;

                    float dist = Mathf.Sqrt(ox * ox + oy * oy);

                    if (dist > radius)
                        continue;

                    float strength = 1f - dist / radius;

                    homeTrailGrid[x, y, s] = Mathf.Clamp01(
                        homeTrailGrid[x, y, s] + strength * dt * 20f
                    );
                }
            }
        }
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

    private float SampleTrailSignalRadius(
    float[,,] grid,
    int centerX,
    int centerY,
    int speciesIndex,
    int radius
)
    {
        if (grid == null)
            return 0f;

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

                signal += grid[x, y, speciesIndex] * weight;
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
            lockedFoodX = -1,
            lockedFoodY = -1,
            lockedFoodType = -1,
            mode = AgentMode.Foraging,
            homeNestIndex = -1,
            foodTrailTimer = 0f,
            foodTrailStrength = 0f,

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

    private bool KeepAgentInsideBounds(ref SlimeAgent agent)
    {
        bool clamped = false;

        float halfX = simulationArea.x * 0.5f;
        float halfY = simulationArea.y * 0.5f;

        Vector2 pos = agent.position;

        if (pos.x < -halfX)
        {
            pos.x = -halfX;
            clamped = true;
        }
        else if (pos.x > halfX)
        {
            pos.x = halfX;
            clamped = true;
        }

        if (pos.y < -halfY)
        {
            pos.y = -halfY;
            clamped = true;
        }
        else if (pos.y > halfY)
        {
            pos.y = halfY;
            clamped = true;
        }

        agent.position = pos;

        if (clamped)
        {
            agent.angle += 180f + Random.Range(-120f, 120f);
            agent.senseCacheValid = false;
        }

        return clamped;
    }

    private Vector2 AngleToDirection(float angle)
    {
        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
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

    private void UpdateNestReproduction(float dt)
    {
        nestReproductionTimer -= dt;

        if (nestReproductionTimer > 0f)
            return;

        nestReproductionTimer = nestReproductionInterval;

        for (int i = 0; i < nests.Length; i++)
        {
            if (!nests[i].active)
                continue;

            int speciesIndex = nests[i].speciesIndex;

            if (GetLiveSpeciesCount(speciesIndex) >= nestMaxAgentsPerSpecies)
                continue;

            if (nests[i].storedEnergy < nestSpawnEnergyCost)
                continue;

            if (SpawnAgentFromNest(speciesIndex))
            {
                Nest nest = nests[i];
                nest.storedEnergy -= nestSpawnEnergyCost;
                nests[i] = nest;
            }
        }
    }
}