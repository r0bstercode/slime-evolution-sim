using UnityEngine;
using UnityEngine.InputSystem;

public partial class SimulationManager
{
    private bool showBody = false;
    private bool showMovement = false;
    private bool showSensors = false;
    private bool showChemistry = false;
    private bool showReproduction = false;
    private bool showDiet = false;
    private bool showAppearance = false;

    private GUIStyle hudStyle;
    private GUIStyle hudSmallStyle;
    private GUIStyle hudSectionStyle;
    private GUIStyle hudButtonStyle;
    private GUIStyle hudValueStyle;

    private const float LeftPanelWidth = 400f;
    private const float RowButtonHeight = 28f;

    private void OnGUI()
    {
        if (agents == null || runtimeSpecies == null)
            return;

        if (cachedSpeciesCounts == null || cachedSpeciesCounts.Length != activeSpeciesCount)
            RefreshCachedStats();

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        BuildHUDStyles(style);

        GUILayout.BeginArea(new Rect(20, 20, LeftPanelWidth, Screen.height - 40));

        DrawSimulationPanel(style, cachedTotalAlive);
        DrawEvolutionPanel(style);
        DrawGraphPanel(style);
        DrawEnergyPanel(style);
        DrawFoodPanel(style);
        DrawToolsPanel(style);

        GUILayout.EndArea();

        DrawSpeciesSidebar(style, cachedSpeciesCounts);
        DrawPerformanceOverlay();
    }

    private void BuildHUDStyles(GUIStyle baseStyle)
    {
        hudStyle = new GUIStyle(baseStyle);
        hudStyle.fontSize = 13;
        hudStyle.normal.textColor = Color.white;

        hudSmallStyle = new GUIStyle(baseStyle);
        hudSmallStyle.fontSize = 11;
        hudSmallStyle.normal.textColor = new Color(0.82f, 0.82f, 0.82f);

        hudSectionStyle = new GUIStyle(baseStyle);
        hudSectionStyle.fontSize = 11;
        hudSectionStyle.fontStyle = FontStyle.Bold;
        hudSectionStyle.normal.textColor = Color.gray;

        hudButtonStyle = new GUIStyle(GUI.skin.button);
        hudButtonStyle.fontSize = 11;
        hudButtonStyle.fontStyle = FontStyle.Bold;
        hudButtonStyle.alignment = TextAnchor.MiddleCenter;
        hudButtonStyle.normal.textColor = Color.white;
        hudButtonStyle.hover.textColor = Color.white;
        hudButtonStyle.active.textColor = Color.white;

        hudValueStyle = new GUIStyle(hudStyle);
        hudValueStyle.alignment = TextAnchor.MiddleRight;
    }

    private void DrawSectionHeader(string label)
    {
        GUILayout.Space(5);
        GUILayout.Label(label.ToUpperInvariant(), hudSectionStyle);
    }

    private string FormatEnergy(float value)
    {
        float abs = Mathf.Abs(value);

        if (abs >= 1000000f)
            return (value / 1000000f).ToString("0.##") + "M";

        if (abs >= 1000f)
            return (value / 1000f).ToString("0.#") + "k";

        return value.ToString("0");
    }

    private void DrawSimulationPanel(GUIStyle style, int totalAlive)
    {
        DrawSectionHeader("SIMULATION");

        GUILayout.BeginHorizontal();

        DrawRunPauseButton();
        DrawSpeedButton("0.5x", 0.5f);
        DrawSpeedButton("1x", 1f);
        DrawSpeedButton("2x", 2f);
        DrawSpeedButton("5x", 5f);
        DrawSpeedButton("10x",10f);

        if (GUILayout.Button("RESET", hudButtonStyle, GUILayout.Width(64), GUILayout.Height(RowButtonHeight)))
            ResetSimulation();

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Age: " + worldAge.ToString("0") + "s", hudSmallStyle, GUILayout.Width(150));
        GUILayout.Label("Pop: " + totalAlive + " / " + agents.Length, hudSmallStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawEvolutionPanel(GUIStyle style)
    {
        DrawSectionHeader("EVOLUTION");

        GUILayout.BeginHorizontal();

        Color oldBg = GUI.backgroundColor;

        GUI.backgroundColor = mutationEnabled ? Color.green : Color.red;
        if (GUILayout.Button(mutationEnabled ? "MUTATION ON" : "MUTATION OFF", hudButtonStyle, GUILayout.Width(132), GUILayout.Height(RowButtonHeight)))
            mutationEnabled = !mutationEnabled;

        GUI.backgroundColor = speciationEnabled ? Color.green : Color.red;
        if (GUILayout.Button(speciationEnabled ? "SPECIATION ON" : "SPECIATION OFF", hudButtonStyle, GUILayout.Width(145), GUILayout.Height(RowButtonHeight)))
            speciationEnabled = !speciationEnabled;

        GUI.backgroundColor = oldBg;

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Mut: " + mutationEventCount, hudSmallStyle, GUILayout.Width(105));
        GUILayout.Label("Spec: " + speciationEventCount, hudSmallStyle, GUILayout.Width(105));
        GUILayout.Label("Ext: " + extinctionEventCount, hudSmallStyle);
        GUILayout.EndHorizontal();

        mutationStrength = DrawCompactFloatEditor("Amplitude", "", mutationStrength, 0f, 0.005f, 0f, 1f);
        speciationThreshold = DrawCompactFloatEditor("Threshold", "", speciationThreshold, 1.5f, 0.1f, 0f, 10f);
    }

    private void DrawGraphPanel(GUIStyle style)
    {
        DrawSectionHeader("GRAPH");
        DrawPopulationGraph(style);
    }

    private void DrawEnergyPanel(GUIStyle style)
    {
        DrawSectionHeader("ENERGY");

        DrawEnergyRow("Total", cachedEcoEnergyTotal, true);
        DrawEnergyRow("Agents", cachedAgentEnergy, false);

        for (int i = 0; i < 3; i++)
        {
            int s = cachedTopEnergySpecies[i];

            if (s < 0 || s >= activeSpeciesCount || runtimeSpecies[s] == null)
                continue;

            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label((i + 1) + ". " + runtimeSpecies[s].speciesName, hudSmallStyle, GUILayout.Width(230));
            GUILayout.Label(FormatEnergy(cachedTopEnergyAmounts[i]), hudValueStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }

        DrawEnergyRow("Green", cachedGreenEnergy, false);
        DrawEnergyRow("Brown", cachedBrownEnergy, false);
        DrawEnergyRow("Corpses", cachedCorpseEnergy, false);
    }

    private void DrawEnergyRow(string label, float value, bool highlight)
    {
        GUIStyle rowStyle = highlight ? hudStyle : hudSmallStyle;

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.Width(160));
        GUILayout.Label(FormatEnergy(value), hudValueStyle, GUILayout.Width(90));
        GUILayout.EndHorizontal();
    }

    private void DrawFoodPanel(GUIStyle style)
    {
        if (foodGrid == null)
            return;

        GetFoodStats(out float totalFood, out float coveragePercent);

        DrawSectionHeader("FOOD");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Total Food", hudSmallStyle, GUILayout.Width(120));
        GUILayout.Label(FormatEnergy(totalFood) + " E", hudValueStyle, GUILayout.Width(90));
        GUILayout.Label("Coverage", hudSmallStyle, GUILayout.Width(70));
        GUILayout.Label(coveragePercent.ToString("0") + "%", hudValueStyle, GUILayout.Width(45));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Total Corpses", hudSmallStyle, GUILayout.Width(120));
        GUILayout.Label(GetActiveCorpseCount().ToString(), hudValueStyle, GUILayout.Width(90));
        GUILayout.EndHorizontal();

        if (foodTypes != null && foodTypes.Length > 0)
        {
            for (int i = 0; i < foodTypes.Length; i++)
            {
                FoodType food = foodTypes[i];

                if (food == null)
                    continue;

                DrawFoodTypeCompact(i, food);
            }

            foodGrowthRate = foodTypes[0].growthRate;
        }
    }

    private void DrawFoodTypeCompact(int foodIndex, FoodType food)
    {
        float sharePercent = GetFoodSharePercent(foodIndex);
        bool selected = currentTool == ToolMode.FoodPaint && selectedFoodType == foodIndex;

        GUIStyle rowStyle = new GUIStyle(GUI.skin.button);
        rowStyle.fontSize = 12;
        rowStyle.fontStyle = FontStyle.Bold;
        rowStyle.alignment = TextAnchor.MiddleLeft;
        rowStyle.richText = true;
        rowStyle.normal.textColor = Color.white;
        rowStyle.hover.textColor = Color.white;
        rowStyle.active.textColor = Color.white;

        Color oldBg = GUI.backgroundColor;
        Color uiColor = GetBrightUIColor(food.color);

        GUI.backgroundColor = selected
            ? uiColor
            : new Color(uiColor.r * 0.7f, uiColor.g * 0.7f, uiColor.b * 0.7f, 1f);

        string marker = selected ? "■ " : "● ";
        string coloredLabel =
            "<color=#" +
            ColorUtility.ToHtmlStringRGB(food.color) +
            ">" +
            marker +
            "</color>" +
            food.foodName +
            "  " +
            sharePercent.ToString("0") +
            "%";

        if (GUILayout.Button(coloredLabel, rowStyle, GUILayout.Width(380), GUILayout.Height(RowButtonHeight)))
        {
            if (selected)
                currentTool = ToolMode.None;
            else
            {
                selectedFoodType = foodIndex;
                currentTool = ToolMode.FoodPaint;
            }
        }

        GUI.backgroundColor = oldBg;

        food.growthRate = DrawCompactFloatEditor("Rate", "E/s", food.growthRate, food.growthRate, 0.02f, 0f, 1f);
        food.energyValue = DrawCompactFloatEditor("Value", "E", food.energyValue, food.energyValue, 0.1f, 0f, 100f);
    }

    private void DrawToolsPanel(GUIStyle style)
    {
        DrawSectionHeader("TOOLS");

        Color spawnColor = Color.white;

        if (selectedSpeciesIndex >= 0 && selectedSpeciesIndex < activeSpeciesCount && runtimeSpecies[selectedSpeciesIndex] != null)
            spawnColor = runtimeSpecies[selectedSpeciesIndex].color;

        GUILayout.BeginHorizontal();
        DrawToolButton("Wall", ToolMode.Obstacle, Color.gray, style);
        DrawToolButton("Erase", ToolMode.Erase, Color.red, style);
        DrawToolButton("Spawn", ToolMode.Spawn, spawnColor, style);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Brush " + brushRadius.ToString("0.0"), hudSmallStyle, GUILayout.Width(90));

        if (GUILayout.Button("-", hudButtonStyle, GUILayout.Width(30), GUILayout.Height(22)))
            brushRadius = Mathf.Max(0.5f, brushRadius - 0.5f);

        if (GUILayout.Button("+", hudButtonStyle, GUILayout.Width(30), GUILayout.Height(22)))
            brushRadius = Mathf.Min(20f, brushRadius + 0.5f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Spawn " + spawnCountPerClick, hudSmallStyle, GUILayout.Width(90));

        if (GUILayout.Button("1", hudButtonStyle, GUILayout.Width(35), GUILayout.Height(22)))
            spawnCountPerClick = 1;

        if (GUILayout.Button("+10", hudButtonStyle, GUILayout.Width(45), GUILayout.Height(22)))
            spawnCountPerClick += 10;

        if (GUILayout.Button("+100", hudButtonStyle, GUILayout.Width(55), GUILayout.Height(22)))
            spawnCountPerClick += 100;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(
            panicMode ? "PANIC" : "NORMAL",
            GUILayout.Width(90),
            GUILayout.Height(24)))
        {
            panicMode = !panicMode;
        }
        GUILayout.EndHorizontal();
    }

    private float DrawCompactFloatEditor(
        string label,
        string unit,
        float value,
        float baseValue,
        float step,
        float min,
        float max)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label + ":", hudSmallStyle, GUILayout.Width(110));

        string valueText = value.ToString("0.###");

        if (!string.IsNullOrEmpty(unit))
            valueText += " " + unit;

        GUILayout.Label(valueText, hudValueStyle, GUILayout.Width(75));

        if (GUILayout.Button("-", hudButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
            value -= step * GetRepeatMultiplier();

        if (GUILayout.Button("+", hudButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
            value += step * GetRepeatMultiplier();

        if (Mathf.Abs(value - baseValue) > 0.001f)
        {
            GUIStyle baseStyle = new GUIStyle(hudSmallStyle);
            baseStyle.normal.textColor = Color.gray;
            GUILayout.Label("base " + baseValue.ToString("0.###"), baseStyle, GUILayout.Width(75));
        }

        GUILayout.EndHorizontal();

        return Mathf.Clamp(value, min, max);
    }

    private void DrawPerformanceOverlay()
    {
        if (!showPerformanceStats)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 13;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea(new Rect(11, Screen.height - 230, 360, 210));

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(180));
        DrawPerfRow("FPS", cachedFps, 60f, style);
        DrawPerfRow("Frame", cachedFrameMs, 100f, style);
        DrawPerfRow("Sim", cachedSimMs, 100f, style);
        GUILayout.Space(3);
        DrawPerfRow("Grid", cachedPreyGridMs, 10f, style);
        DrawPerfRow("Agents", cachedAgentsMs, 50f, style);
        DrawPerfRow("Corps", cachedCorpsesMs, 10f, style);
        DrawPerfRow("Trails", cachedTrailsMs, 30f, style);
        DrawPerfRow("Food", cachedFoodMs, 10f, style);
        GUILayout.Space(3);
        GUILayout.EndVertical();


        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawPerfRow(string label, float value, float maxValue, GUIStyle style)
    {
        GUIStyle rowStyle = new GUIStyle(style);
        rowStyle.margin = new RectOffset(0, 0, 0, 0);
        rowStyle.padding = new RectOffset(0, 0, 0, 0);

        GUILayout.BeginHorizontal(GUILayout.Height(10));

        GUILayout.Label(label, rowStyle, GUILayout.Width(45));

        GUIStyle valueStyle = new GUIStyle(rowStyle);
        valueStyle.alignment = TextAnchor.MiddleRight;

        GUILayout.Label(value.ToString("0.0"), valueStyle, GUILayout.Width(32));

        GUILayout.Label("", GUILayout.Width(55), GUILayout.Height(9));
        Rect rect = GUILayoutUtility.GetLastRect();

        Rect barRect = new Rect(rect.x, rect.y + 2, rect.width, 6);
        GUI.Box(barRect, "");

        float fill = Mathf.Clamp01(value / maxValue);

        Rect fillRect = new Rect(
            barRect.x + 1,
            barRect.y + 1,
            (barRect.width - 2) * fill,
            barRect.height - 2
        );

        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUILayout.EndHorizontal();
    }

    private void DrawSpeciesList(GUIStyle style, int[] counts)
    {
        style.normal.textColor = Color.white;
        GUILayout.Label("=== SPECIES ===", style);
        if (counts == null || counts.Length < activeSpeciesCount)
            RefreshCachedStats();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Herb", GUILayout.Width(65), GUILayout.Height(24)))
            AddPresetSpecies("Herbivore", 20);

        if (GUILayout.Button("+ Pred", GUILayout.Width(65), GUILayout.Height(24)))
            AddPresetSpecies("Predator", 12);

        if (GUILayout.Button("+ Scav", GUILayout.Width(65), GUILayout.Height(24)))
            AddPresetSpecies("Scavenger", 16);

        if (GUILayout.Button("+ Omni", GUILayout.Width(65), GUILayout.Height(24)))
            AddPresetSpecies("Omnivore", 16);

        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        for (int i = 0; i < activeSpeciesCount; i++)
        {
            if (runtimeSpecies[i] == null || !runtimeSpecies[i].active)
                continue;

            int count = counts != null && i < counts.Length ? counts[i] : 0;
            float births = 0f;
            float deaths = 0f;

            if (cachedSpeciesBirthRate != null && i < cachedSpeciesBirthRate.Length)
                births = cachedSpeciesBirthRate[i];

            if (cachedSpeciesDeathRate != null && i < cachedSpeciesDeathRate.Length)
                deaths = cachedSpeciesDeathRate[i];

            string label =
                runtimeSpecies[i].speciesName +
                ": " + count;

            bool showDelete =
                GetLiveSpeciesCount(i) <= 0 &&
                runtimeSpecies[i] != null &&
                i >= 4;

            if (DrawSpeciesRowButton(i, label, style, showDelete))
            {
                if (selectedSpeciesIndex == i)
                    selectedSpeciesIndex = -1;
                else
                    selectedSpeciesIndex = i;
            }

            if (selectedSpeciesIndex == i)
            {
                DrawSpeciesInspector(style, counts);
            }
        }

        GUILayout.Space(2);
    }

    private int GetLiveSpeciesCount(int speciesIndex)
    {
        int live = 0;

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (agents[i].speciesIndex == speciesIndex)
                live++;
        }

        return live;
    }

    private bool DrawSpeciesRowButton(
    int speciesIndex,
    string label,
    GUIStyle baseStyle,
    bool showDelete
)
    {
        RuntimeSpecies s = runtimeSpecies[speciesIndex];

        bool selected = selectedSpeciesIndex == speciesIndex;

        GUIStyle rowStyle = new GUIStyle(GUI.skin.button);
        rowStyle.fontSize = baseStyle.fontSize;
        rowStyle.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        rowStyle.alignment = TextAnchor.MiddleLeft;
        rowStyle.richText = true;
        rowStyle.normal.textColor = Color.white;
        rowStyle.hover.textColor = Color.white;
        rowStyle.active.textColor = Color.white;

        Color oldBg = GUI.backgroundColor;
        Color uiColor = GetBrightUIColor(s.color);

        GUI.backgroundColor = selected
            ? uiColor
            : new Color(
                uiColor.r * 0.7f,
                uiColor.g * 0.7f,
                uiColor.b * 0.7f,
                1f
            );

        string marker = selected ? "■ " : "● ";

        string coloredLabel =
            "<color=#" +
            ColorUtility.ToHtmlStringRGB(s.color) +
            ">" +
            marker +
            "</color>" +
            label;

        bool clicked = false;
        bool deleteClicked = false;

        GUILayout.BeginHorizontal();

        clicked = GUILayout.Button(
            coloredLabel,
            rowStyle,
            GUILayout.Width(showDelete ? 345 : 380),
            GUILayout.Height(28)
        );

        if (showDelete)
        {
            GUI.backgroundColor = new Color(0.35f, 0.1f, 0.1f);

            deleteClicked = GUILayout.Button(
                "X",
                GUILayout.Width(28),
                GUILayout.Height(28)
            );
        }

        GUILayout.EndHorizontal();

        GUI.backgroundColor = oldBg;

        if (deleteClicked)
        {
            RemoveSpecies(speciesIndex);
            return false;
        }

        return clicked;
    }

    private void DrawSpeciesInspector(GUIStyle style, int[] counts)
    {
        if (selectedSpeciesIndex < 0 || selectedSpeciesIndex >= activeSpeciesCount)
            return;

        RuntimeSpecies s = runtimeSpecies[selectedSpeciesIndex];

        if (s == null)
            return;

        GUIStyle compactStyle = new GUIStyle(style);
        compactStyle.fontSize = 13;
        compactStyle.normal.textColor = Color.white;

        GUIStyle sectionStyle = new GUIStyle(style);
        sectionStyle.fontSize = 10;
        sectionStyle.normal.textColor = Color.gray;
        sectionStyle.fontStyle = FontStyle.Bold;

        GUIStyle sectionButtonStyle = new GUIStyle(GUI.skin.button);
        sectionButtonStyle.fontSize = 11;
        sectionButtonStyle.fontStyle = FontStyle.Bold;
        sectionButtonStyle.alignment = TextAnchor.MiddleLeft;
        sectionButtonStyle.normal.textColor = Color.gray;
        sectionButtonStyle.hover.textColor = Color.white;
        sectionButtonStyle.active.textColor = Color.white;

        SpeciesDNA baseDNA = s.sourceDNA;

        float baseMaxAge = baseDNA != null ? baseDNA.maxAge : s.maxAge;
        float baseStartEnergy = baseDNA != null ? baseDNA.startEnergy : s.startEnergy;
        float baseCapacity = baseDNA != null ? baseDNA.startEnergy * 3f : s.energyCapacity;
        float baseSpeed = baseDNA != null ? baseDNA.speed : s.speed;
        float baseTurnSpeed = baseDNA != null ? baseDNA.turnSpeed : s.turnSpeed;
        float baseMoveCost = baseDNA != null ? baseDNA.movementEnergyCost : s.movementEnergyCost;
        float baseTrailCost = baseDNA != null ? baseDNA.trailEnergyCost : s.trailEnergyCost;
        float basePauseMin = baseDNA != null ? baseDNA.eatPauseMin : s.eatPauseMin;
        float basePauseMax = baseDNA != null ? baseDNA.eatPauseMax : s.eatPauseMax;
        float baseSensorDistance = baseDNA != null ? baseDNA.sensorDistance : s.sensorDistance;
        float baseSensorAngle = baseDNA != null ? baseDNA.sensorAngle : s.sensorAngle;
        float baseTrailStrength = baseDNA != null ? baseDNA.trailStrength : s.trailStrength;
        float baseOwnAffinity = baseDNA != null ? baseDNA.ownTrailAttraction : s.ownTrailAttraction;
        float baseForeignAversion = baseDNA != null ? baseDNA.foreignTrailRepulsion : s.foreignTrailRepulsion;
        float baseBreedEnergy = baseDNA != null ? baseDNA.reproductionThreshold : s.reproductionThreshold;
        float baseMutationChance = baseDNA != null ? baseDNA.mutationChance : s.mutationChance;
        float baseMinReproAge = baseDNA != null ? baseDNA.minReproductionAge : s.minReproductionAge;
        Color baseColor = baseDNA != null ? baseDNA.speciesColor : s.color;

        GUILayout.Space(2);
        GUILayout.Label("OVERVIEW", sectionStyle);

        int pop = 0;
        if (counts != null && selectedSpeciesIndex < counts.Length)
            pop = counts[selectedSpeciesIndex];

        GUILayout.BeginHorizontal();
        GUILayout.Label("Pop: " + pop, compactStyle, GUILayout.Width(110));
        GUILayout.Label("Gen: " + s.generation, compactStyle, GUILayout.Width(90));
        GUILayout.Label("MutDist: " + s.mutationDistance.ToString("0.##"), compactStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(2);
        if (GUILayout.Button((showBody ? "▼ " : "▶ ") + "BODY", sectionButtonStyle, GUILayout.Height(22)))
            showBody = !showBody;

        if (showBody)
        {
            s.maxAge = DrawFloatEditor("Lifespan", "s", s.maxAge, baseMaxAge, 10f, 1f, 10000f, compactStyle);
            s.startEnergy = DrawFloatEditor("Start Energy", "E", s.startEnergy, baseStartEnergy, 10f, 0f, 10000f, compactStyle);
            s.energyCapacity = DrawFloatEditor("Capacity", "E", s.energyCapacity, baseCapacity, 10f, s.startEnergy, 50000f, compactStyle);
            s.hungerThreshold = DrawFloatEditor("Hunger", "%", s.hungerThreshold * 100f, 20f, 5f, 0f, 100f, compactStyle) / 100f;
            s.satiationThreshold = DrawFloatEditor("Satiation", "%", s.satiationThreshold * 100f, 90f, 5f, 0f, 100f, compactStyle) / 100f;
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showMovement ? "▼ " : "▶ ") + "MOVEMENT", sectionButtonStyle, GUILayout.Height(22)))
            showMovement = !showMovement;

        if (showMovement)
        {
            s.speed = DrawFloatEditor("Speed", "wu/s", s.speed, baseSpeed, 0.1f, 0f, 20f, compactStyle);
            s.turnSpeed = DrawFloatEditor("Turn", "°/s", s.turnSpeed, baseTurnSpeed, 5f, 0f, 720f, compactStyle);
            s.movementEnergyCost = DrawFloatEditor("Move Cost", "E/s", s.movementEnergyCost, baseMoveCost, 0.005f, 0f, 10f, compactStyle);
            s.eatPauseMin = DrawFloatEditor("Pause Min", "s", s.eatPauseMin, basePauseMin, 0.02f, 0f, 10f, compactStyle);
            s.eatPauseMax = DrawFloatEditor("Pause Max", "s", s.eatPauseMax, basePauseMax, 0.02f, 0f, 10f, compactStyle);

            if (s.eatPauseMax < s.eatPauseMin)
                s.eatPauseMax = s.eatPauseMin;
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showSensors ? "▼ " : "▶ ") + "SENSORS", sectionButtonStyle, GUILayout.Height(22)))
            showSensors = !showSensors;

        if (showSensors)
        {
            s.sensorDistance = DrawFloatEditor("Range", "wu", s.sensorDistance, baseSensorDistance, 0.1f, 0f, 20f, compactStyle);
            s.sensorAngle = DrawFloatEditor("Arc", "°", s.sensorAngle, baseSensorAngle, 1f, 0f, 180f, compactStyle);
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showChemistry ? "▼ " : "▶ ") + "CHEMISTRY", sectionButtonStyle, GUILayout.Height(22)))
            showChemistry = !showChemistry;

        if (showChemistry)
        {
            s.trailStrength = DrawFloatEditor("Trail Out", "ch/s", s.trailStrength, baseTrailStrength, 0.1f, 0f, 10f, compactStyle);
            s.ownTrailAttraction = DrawFloatEditor("Self Affinity", "χ", s.ownTrailAttraction, baseOwnAffinity, 0.1f, -10f, 10f, compactStyle);
            s.foreignTrailRepulsion = DrawFloatEditor("Foreign Aversion", "χ", s.foreignTrailRepulsion, baseForeignAversion, 0.1f, -10f, 10f, compactStyle);
            s.trailEnergyCost = DrawFloatEditor("Trail Cost", "E/s", s.trailEnergyCost, baseTrailCost, 0.005f, 0f, 10f, compactStyle);
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showReproduction ? "▼ " : "▶ ") + "REPRODUCTION", sectionButtonStyle, GUILayout.Height(22)))
            showReproduction = !showReproduction;

        if (showReproduction)
        {
            s.reproductionThreshold = DrawFloatEditor("Breed Energy", "E", s.reproductionThreshold, baseBreedEnergy, 10f, 0f, 10000f, compactStyle);
            s.minReproductionAge = DrawFloatEditor("Min Age", "s", s.minReproductionAge, baseMinReproAge, 1f, 0f, 1000f, compactStyle);
            s.mutationChance = DrawFloatEditor("Mut Rate", "", s.mutationChance, baseMutationChance, 0.005f, 0f, 1f, compactStyle);
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showDiet ? "▼ " : "▶ ") + "DIET", sectionButtonStyle, GUILayout.Height(22)))
            showDiet = !showDiet;

        if (showDiet)
        {
            if (s.foodPreferences == null || s.foodPreferences.Length != foodTypes.Length)
            {
                s.foodPreferences = new float[foodTypes.Length];

                for (int i = 0; i < s.foodPreferences.Length; i++)
                    s.foodPreferences[i] = 1f;
            }

            for (int i = 0; i < foodTypes.Length; i++)
            {
                FoodType food = foodTypes[i];

                if (food == null)
                    continue;

                s.foodPreferences[i] = DrawFloatEditor(food.foodName, "pref", s.foodPreferences[i], 1f, 0.1f, -10f, 10f, compactStyle);
            }

            s.corpsePreference = DrawFloatEditor("Corpse", "pref", s.corpsePreference, 1f, 0.1f, 0f, 10f, compactStyle);
            s.preyPreference = DrawFloatEditor("Prey", "pref", s.preyPreference, 0f, 0.1f, 0f, 10f, compactStyle);
        }

        GUILayout.Space(2);
        if (GUILayout.Button((showAppearance ? "▼ " : "▶ ") + "APPEARANCE", sectionButtonStyle, GUILayout.Height(22)))
            showAppearance = !showAppearance;

        if (showAppearance)
        {
            s.color.r = DrawFloatEditor("Red", "", s.color.r, baseColor.r, 0.05f, 0f, 1f, compactStyle);
            s.color.g = DrawFloatEditor("Green", "", s.color.g, baseColor.g, 0.05f, 0f, 1f, compactStyle);
            s.color.b = DrawFloatEditor("Blue", "", s.color.b, baseColor.b, 0.05f, 0f, 1f, compactStyle);
            s.color.a = 1f;
        }
    }


    private float DrawFloatEditor(
    string label,
    string unit,
    float value,
    float baseValue,
    float step,
    float min,
    float max,
    GUIStyle style)
    {
        GUILayout.BeginHorizontal();

        style.normal.textColor = Color.white;

        GUILayout.Label(label + ":", style, GUILayout.Width(155));

        GUIStyle valueStyle = new GUIStyle(style);
        valueStyle.alignment = TextAnchor.MiddleRight;

        string valueText = value.ToString("0.###");

        if (!string.IsNullOrEmpty(unit))
            valueText += " " + unit;

        GUILayout.Label(valueText, valueStyle, GUILayout.Width(75));

        if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(20)))
            value -= step * GetRepeatMultiplier();

        if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(20)))
            value += step * GetRepeatMultiplier();

        if (Mathf.Abs(value - baseValue) > 0.001f)
        {
            GUIStyle baseStyle = new GUIStyle(style);
            baseStyle.normal.textColor = Color.gray;

            string baseText = "base " + baseValue.ToString("0.###");

            if (!string.IsNullOrEmpty(unit))
                baseText += " " + unit;

            GUILayout.Label(baseText, baseStyle, GUILayout.Width(110));
        }

        GUILayout.EndHorizontal();

        return Mathf.Clamp(value, min, max);
    }

    private float GetRepeatMultiplier()
    {
        if (Keyboard.current == null)
            return 1f;

        if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
            return 10f;

        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)
            return 0.1f;

        return 1f;
    }

    private void DrawRunPauseButton()
    {
        GUIStyle buttonStyle = new GUIStyle(hudButtonStyle);
        buttonStyle.fontStyle = FontStyle.Bold;

        string label;
        Color bg;

        if (simulationPaused)
        {
            label = worldAge <= 0.01f ? "START" : "PAUSED";
            bg = new Color(1f, 0.55f, 0.1f);
        }
        else
        {
            label = "RUNNING";
            bg = Color.green;
        }

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = bg;

        if (GUILayout.Button(label, buttonStyle, GUILayout.Width(76), GUILayout.Height(RowButtonHeight)))
            simulationPaused = !simulationPaused;

        GUI.backgroundColor = oldBg;
    }


    private void DrawSpeedButton(string label, float speed)
    {
        Color oldBg = GUI.backgroundColor;

        if (Mathf.Approximately(simulationSpeed, speed))
            GUI.backgroundColor = new Color(0.25f, 0.55f, 1f);

        if (GUILayout.Button(label, hudButtonStyle, GUILayout.Width(43), GUILayout.Height(RowButtonHeight)))
            simulationSpeed = speed;

        GUI.backgroundColor = oldBg;
    }


    private void DrawToolButton(string label, ToolMode tool, Color color, GUIStyle baseStyle)
    {
        GUIStyle buttonStyle = new GUIStyle(hudButtonStyle);
        buttonStyle.fontStyle = FontStyle.Bold;

        bool isActive = currentTool == tool;

        Color oldBg = GUI.backgroundColor;

        Color activeColor = GetBrightUIColor(color);
        Color inactiveColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        GUI.backgroundColor = isActive ? activeColor : inactiveColor;

        string buttonLabel = isActive ? "■ " + label.ToUpperInvariant() : "● " + label;

        if (GUILayout.Button(buttonLabel, buttonStyle, GUILayout.Width(95), GUILayout.Height(RowButtonHeight)))
            ToggleTool(tool);

        GUI.backgroundColor = oldBg;
    }


    private Color GetBrightUIColor(Color baseColor)
    {
        return Color.Lerp(baseColor, Color.white, 0.22f);
    }

    private void DrawPopulationGraph(GUIStyle style)
    {
        if (!showPopulationGraph)
            return;

        if (populationHistory == null)
            return;

        Rect rect = GUILayoutUtility.GetRect(
            380,
            populationGraphHeight,
            GUILayout.Width(380),
            GUILayout.Height(populationGraphHeight)
        );

        GUI.Box(rect, "");

        int maxPop = 1;

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            if (runtimeSpecies[s] == null)
                continue;

            for (int i = 0; i < populationGraphSamples; i++)
                maxPop = Mathf.Max(maxPop, populationHistory[s, i]);
        }

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            if (runtimeSpecies[s] == null)
                continue;

            Color oldColor = GUI.color;
            GUI.color = runtimeSpecies[s].color;

            Vector2 previous = Vector2.zero;
            bool hasPrevious = false;

            for (int i = 0; i < populationGraphSamples; i++)
            {
                int sampleIndex = (populationGraphIndex + i) % populationGraphSamples;
                int pop = populationHistory[s, sampleIndex];

                float x = rect.x + (i / (float)(populationGraphSamples - 1)) * rect.width;
                float y = rect.yMax - (pop / (float)maxPop) * rect.height;

                Vector2 current = new Vector2(x, y);

                if (hasPrevious)
                    DrawUILine(previous, current, GUI.color, 2f);

                previous = current;
                hasPrevious = true;
            }

            GUI.color = oldColor;
        }
    }


    private void DrawUILine(Vector2 start, Vector2 end, Color color, float width)
    {
        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;

        GUI.color = color;

        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;

        GUIUtility.RotateAroundPivot(angle, start);

        GUI.DrawTexture(
            new Rect(start.x, start.y - width * 0.5f, length, width),
            Texture2D.whiteTexture
        );

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawInlineSpeciesInspector(RuntimeSpecies s, GUIStyle style)
    {
        GUIStyle small = new GUIStyle(style);
        small.fontSize = 11;
        small.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        GUILayout.BeginVertical("box");

        GUILayout.Label("Speed: " + s.speed.ToString("0.0"), small);
        GUILayout.Label("Sensor: " + s.sensorDistance.ToString("0.0"), small);

        GUILayout.Label(
            "Breed: " +
            s.reproductionThreshold.ToString("0") +
            " / Age " +
            s.minReproductionAge.ToString("0"),
            small
        );

        GUILayout.Label(
            "Diet G:" + s.foodPreferences[0].ToString("0.0") +
            " B:" + s.foodPreferences[1].ToString("0.0"),
            small
        );

        GUILayout.Label(
            "Corpse: " + s.corpsePreference.ToString("0.0") +
            " Prey: " + s.preyPreference.ToString("0.0"),
            small
        );

        GUILayout.EndVertical();
    }

    private Vector2 speciesScroll;

    private void DrawSpeciesSidebar(GUIStyle style, int[] counts)
    {
        GUILayout.BeginArea(new Rect(Screen.width - 420, 20, 400, Screen.height - 40));

        speciesScroll = GUILayout.BeginScrollView(
            speciesScroll,
            GUILayout.Width(400),
            GUILayout.Height(Screen.height - 40)
        );

        DrawSpeciesList(style, counts);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

}