using UnityEngine;
using UnityEngine.InputSystem;

public partial class SimulationManager
{
    private void OnDrawGizmos()
    {
        if (runtimeSpecies == null || agents == null)
            return;
        float halfX = simulationArea.x * 0.5f;
        float halfY = simulationArea.y * 0.5f;

        Gizmos.color = Color.white;

        Vector3 topLeft = new Vector3(-halfX, halfY, 0f);
        Vector3 topRight = new Vector3(halfX, halfY, 0f);
        Vector3 bottomLeft = new Vector3(-halfX, -halfY, 0f);
        Vector3 bottomRight = new Vector3(halfX, -halfY, 0f);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);

        if (showObstacles && obstacleGrid != null)
        {
            float cellWidth = simulationArea.x / gridWidth;
            float cellHeight = simulationArea.y / gridHeight;

            Gizmos.color = Color.gray;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (!obstacleGrid[x, y])
                        continue;

                    Vector3 pos = new Vector3(
                        -simulationArea.x * 0.5f + x * cellWidth + cellWidth * 0.5f,
                        -simulationArea.y * 0.5f + y * cellHeight + cellHeight * 0.5f,
                        0.1f
                    );

                    Gizmos.DrawCube(pos, new Vector3(cellWidth, cellHeight, 0.05f));
                }
            }


        }

        if (showTrails && trailGrid != null && runtimeSpecies != null)
        {
            float cellWidth = simulationArea.x / gridWidth;
            float cellHeight = simulationArea.y / gridHeight;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    int dominantSpecies = -1;
                    float strongestTrail = 0f;

                    for (int s = 0; s < activeSpeciesCount; s++)
                    {
                        float trail = trailGrid[x, y, s];

                        if (trail <= trailMinimumVisible)
                            continue;

                        if (trail > strongestTrail)
                        {
                            strongestTrail = trail;
                            dominantSpecies = s;
                        }
                    }

                    if (dominantSpecies < 0)
                        continue;

                    if (runtimeSpecies[dominantSpecies] == null)
                        continue;

                    Color trailColor = runtimeSpecies[dominantSpecies].color;
                    trailColor.a = Mathf.Clamp01(strongestTrail * trailAlpha);

                    Gizmos.color = trailColor;

                    Vector3 pos = new Vector3(
                        -simulationArea.x * 0.5f + x * cellWidth + cellWidth * 0.5f,
                        -simulationArea.y * 0.5f + y * cellHeight + cellHeight * 0.5f,
                        0.15f
                    );

                    Gizmos.DrawCube(pos, new Vector3(cellWidth, cellHeight, 0.04f));
                }
            }
        }

        if (showFood && foodGrid != null)
        {
            float cellWidth = simulationArea.x / gridWidth;
            float cellHeight = simulationArea.y / gridHeight;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float totalFood = GetTotalFoodAt(x, y);

                    if (totalFood <= 0.01f)
                        continue;

                    Color mixedColor = Color.black;
                    float totalWeight = 0f;

                    for (int f = 0; f < foodTypes.Length; f++)
                    {
                        float amount = foodGrid[x, y, f];

                        if (amount <= 0.01f)
                            continue;

                        float normalized = amount / foodTypes[f].maxDensity;
                        float weight = Mathf.Clamp01(normalized);

                        mixedColor += foodTypes[f].color * weight;
                        totalWeight += weight;
                    }

                    if (totalWeight <= 0f)
                        continue;

                    mixedColor /= totalWeight;
                    mixedColor.a = foodAlpha;

                    Vector3 pos = new Vector3(
                         -simulationArea.x * 0.5f + x * cellWidth + cellWidth * 0.5f,
                         -simulationArea.y * 0.5f + y * cellHeight + cellHeight * 0.5f,
                         0.2f
                    );

                    Gizmos.color = mixedColor;
                    Gizmos.DrawCube(pos, new Vector3(cellWidth, cellHeight, 0.02f));
                }
            }
        }

        if (corpses != null && corpseGrid != null)
        {
            Color corpseColor = new Color(0.55f, 0.12f, 0.06f, 0.85f);

            for (int i = 0; i < corpses.Length; i++)
            {
                if (!corpses[i].active)
                    continue;

                if (!WorldToGrid(corpses[i].position, out int gx, out int gy))
                    continue;

                if (corpseGrid[gx, gy] <= 0.01f)
                    continue;

                float size = Mathf.Clamp(
                    corpses[i].energy * 0.002f,
                    0.04f,
                    0.18f
                );

                Gizmos.color = corpseColor;

                Vector3 pos = new Vector3(
                    corpses[i].position.x,
                    corpses[i].position.y,
                    0.25f
                );

                Gizmos.DrawSphere(pos, size);
            }
        }

        if (showAgents && agents != null && runtimeSpecies != null)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (!agents[i].alive)
                    continue;

                int speciesIndex = agents[i].speciesIndex;

                if (speciesIndex < 0 || speciesIndex >= runtimeSpecies.Length)
                    continue;

                RuntimeSpecies dna = runtimeSpecies[speciesIndex];

                if (dna == null)
                    continue;
                Gizmos.color = dna.color;

                Vector3 pos = new Vector3(
                    agents[i].position.x,
                    agents[i].position.y,
                    0f
                );

                Gizmos.DrawSphere(pos, agentDrawSize);
            }
        }

        DrawBrushPreview();
    }

    private void DrawBrushPreview()
    {
        if (!mouseToolsEnabled || Camera.main == null || Mouse.current == null)
            return;

        if (currentTool == ToolMode.None)
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(Camera.main.transform.position.z))
        );

        Vector3 center = new Vector3(mouseWorld.x, mouseWorld.y, 0f);

        Gizmos.color = GetToolColor();
        Gizmos.DrawWireSphere(center, brushRadius);
    }
}