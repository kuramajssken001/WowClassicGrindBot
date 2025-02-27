﻿using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core;

public sealed partial class KeyAction : IDisposable
{
    public float Cost { get; set; } = 18;
    public string Name { get; set; } = string.Empty;
    public bool HasCastBar { get; set; }
    public ConsoleKey ConsoleKey { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Slot { get; set; }
    public int SlotIndex { get; private set; }
    public int PressDuration { get; set; } = 50;
    public string Form { get; set; } = string.Empty;
    public Form FormEnum { get; set; } = Core.Form.None;
    public bool FormAction { get; private set; }
    public int Cooldown { get; set; } = CastingHandler.SPELL_QUEUE;

    private int _charge;
    public int Charge { get; set; } = 1;
    public SchoolMask School { get; set; } = SchoolMask.None;
    public int MinMana { get; set; }
    public int MinRage { get; set; }
    public int MinEnergy { get; set; }
    public int MinRunicPower { get; set; }
    public int MinRuneBlood { get; set; }
    public int MinRuneFrost { get; set; }
    public int MinRuneUnholy { get; set; }
    public int MinComboPoints { get; set; }

    public int MinCost { get; set; }

    public Func<int> FormCost = null!;

    public bool HasFormRequirement { get; private set; }

    public string Requirement { get; set; } = string.Empty;
    public List<string> Requirements { get; } = new();
    public Requirement[] RequirementsRuntime { get; set; } = Array.Empty<Requirement>();

    public string Interrupt { get; set; } = string.Empty;
    public List<string> Interrupts { get; } = new();
    public Requirement[] InterruptsRuntime { get; set; } = Array.Empty<Requirement>();

    public bool WhenUsable { get; set; }

    public bool ResetOnNewTarget { get; set; }

    public bool Log { get; set; } = true;

    public bool BaseAction { get; set; }

    public bool Item { get; set; }

    public int BeforeCastDelay { get; set; }
    public bool BeforeCastStop { get; set; }
    public bool BeforeCastDismount { get; set; } = true;

    public int AfterCastDelay { get; set; }
    public bool AfterCastWaitMeleeRange { get; set; }
    public bool AfterCastWaitBuff { get; set; }
    public bool AfterCastWaitBag { get; set; }
    public bool AfterCastWaitSwing { get; set; }
    public bool AfterCastWaitCastbar { get; set; }
    public bool AfterCastWaitCombat { get; set; }
    public bool AfterCastWaitGCD { get; set; }
    public bool AfterCastAuraExpected { get; set; }
    public int AfterCastStepBack { get; set; }

    public string InCombat { get; set; } = "false";

    public bool? UseWhenTargetIsCasting { get; set; }

    public string PathFilename { get; set; } = string.Empty;
    public Vector3[] Path { get; set; } = Array.Empty<Vector3>();

    public int ConsoleKeyFormHash { private set; get; }

    private DateTime LastClicked = DateTime.UtcNow.AddDays(-1);

    private static int LastKey;
    private static DateTime LastKeyTime;

    public static int LastKeyClicked()
    {
        return (DateTime.UtcNow - LastKeyTime).TotalSeconds > 2
            ? (int)ConsoleKey.NoName
            : LastKey;
    }

    private ActionBarCostReader costReader = null!;
    private ILogger logger = null!;

    private RecordInt globalTime = null!;
    private int canRunMemoTime;
    private bool canRun;

    public void InitialiseSlot(ILogger logger)
    {
        if (!KeyReader.ReadKey(logger, this) && !BaseAction)
        {
            LogInputNoValidKey(logger, Name, Key, ConsoleKey);
        }
        else if (Slot == 0)
        {
            LogInputNonActionbar(logger, Name, Key, ConsoleKey);
        }
    }

    public void InitDynamicBinding(RequirementFactory requirementFactory)
    {
        requirementFactory.InitDynamicBindings(this);
    }

    public void Initialise(ClassConfiguration config, AddonReader addonReader,
        RequirementFactory requirementFactory, ILogger logger, bool globalLog)
    {
        this.costReader = addonReader.ActionBarCostReader;
        this.logger = logger;

        globalTime = addonReader.GlobalTime;
        this.canRunMemoTime = globalTime.Value;

        FormCost = GetMinCost;

        if (!globalLog)
            Log = false;

        ResetCharges();

        InitialiseSlot(logger);

        if (!string.IsNullOrEmpty(Requirement))
        {
            Requirements.Add(Requirement);
        }

        if (!string.IsNullOrEmpty(Interrupt))
        {
            Interrupts.Add(Interrupt);
        }

        HasFormRequirement = !string.IsNullOrEmpty(Form);

        if (HasFormRequirement)
        {
            if (Enum.TryParse(Form, out Form desiredForm))
            {
                this.FormEnum = desiredForm;
                LogFormRequired(logger, Name, FormEnum.ToStringF());

                if (!FormAction)
                {
                    for (int i = 0; i < config.Form.Length; i++)
                    {
                        if (config.Form[i].FormEnum == FormEnum)
                        {
                            FormCost = config.Form[i].FormCost;
                        }
                    }
                }
            }
            else
            {
                throw new Exception($"[{Name}] Unknown form: {Form}");
            }
        }

        if (Slot > 0)
        {
            this.SlotIndex = Stance.ToSlot(this, addonReader.PlayerReader) - 1;
            LogInputActionbar(logger, Name, Key, Slot, SlotIndex);
        }

        ConsoleKeyFormHash = ((int)FormEnum * 1000) + (int)ConsoleKey;
        ResetCooldown();

        if (Slot > 0)
            InitMinPowerType(addonReader.ActionBarCostReader);

        requirementFactory.InitialiseRequirements(this);

        if (!string.IsNullOrEmpty(PathFilename))
        {
            LogPath(logger, Name, PathFilename);
        }
    }

    public void Dispose()
    {
        costReader.OnActionCostChanged -= ActionBarCostReader_OnActionCostChanged;
        costReader.OnActionCostReset -= ResetCosts;
    }

    public void InitialiseForm(ClassConfiguration config, AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, bool globalLog)
    {
        FormAction = true;
        Initialise(config, addonReader, requirementFactory, logger, globalLog);

        logger.LogInformation($"[{Name}] Added {FormEnum} to FormCost with {MinCost}");
    }

    public void ResetCosts()
    {
        MinCost = 0;

        MinMana = 0;
        MinRage = 0;
        MinEnergy = 0;
        MinRunicPower = 0;

        MinRuneBlood = 0;
        MinRuneFrost = 0;
        MinRuneUnholy = 0;
    }

    public int GetRemainingCooldown()
    {
        return Math.Max(Cooldown - SinceLastClickMs, 0);
    }

    public void SetClicked(double offset = 0)
    {
        LastKey = ConsoleKeyFormHash;
        LastKeyTime = LastClicked = DateTime.UtcNow.AddMilliseconds(offset);
    }

    public int SinceLastClickMs =>
        (int)(DateTime.UtcNow - LastClicked).TotalMilliseconds;

    public void ResetCooldown()
    {
        LastClicked = DateTime.UtcNow.AddDays(-1);
    }

    public int GetChargeRemaining()
    {
        return _charge;
    }

    public void ConsumeCharge()
    {
        if (Charge > 1)
        {
            _charge--;
            if (_charge > 0)
            {
                ResetCooldown();
            }
            else
            {
                ResetCharges();
                SetClicked();
            }
        }
    }

    public void ResetCharges()
    {
        _charge = Charge;
    }

    public bool CanRun()
    {
        if (canRunMemoTime == globalTime.Value)
            return canRun;

        canRunMemoTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = RequirementsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canRun = false;
        }

        return canRun = true;
    }

    public bool CanBeInterrupted()
    {
        ReadOnlySpan<Requirement> span = InterruptsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return false;
        }

        return true;
    }

    private void InitMinPowerType(ActionBarCostReader actionBarCostReader)
    {
        for (int i = 0; i < ActionBar.NUM_OF_COST; i++)
        {
            ActionBarCost abc = actionBarCostReader.Get(this, i);
            if (abc.Cost == 0)
                continue;

            int oldValue = 0;
            switch (abc.PowerType)
            {
                case PowerType.Mana:
                    oldValue = MinMana;
                    MinMana = abc.Cost;
                    break;
                case PowerType.Rage:
                    oldValue = MinRage;
                    MinRage = abc.Cost;
                    break;
                case PowerType.Energy:
                    oldValue = MinEnergy;
                    MinEnergy = abc.Cost;
                    break;
                case PowerType.RunicPower:
                    oldValue = MinRunicPower;
                    MinRunicPower = abc.Cost;
                    break;
                case PowerType.RuneBlood:
                    oldValue = MinRuneBlood;
                    MinRuneBlood = abc.Cost;
                    break;
                case PowerType.RuneFrost:
                    oldValue = MinRuneFrost;
                    MinRuneFrost = abc.Cost;
                    break;
                case PowerType.RuneUnholy:
                    oldValue = MinRuneUnholy;
                    MinRuneUnholy = abc.Cost;
                    break;
            }

            MinCost = abc.Cost;

            LogPowerCostChange(logger, Name, abc.PowerType.ToStringF(), abc.Cost, oldValue);
            if (HasFormRequirement && FormEnum != Core.Form.None)
            {
                int formCost = FormCost();
                if (formCost > 0)
                {
                    logger.LogInformation($"[{Name}] +{formCost} Mana to change into {FormEnum.ToStringF()}");
                }
            }
        }
        actionBarCostReader.OnActionCostChanged += ActionBarCostReader_OnActionCostChanged;
        actionBarCostReader.OnActionCostReset += ResetCosts;
    }

    private void ActionBarCostReader_OnActionCostChanged(object? sender, ActionBarCostEventArgs e)
    {
        if (Slot != e.Slot) return;

        MinCost = e.ActionBarCost.Cost;

        int oldValue = 0;
        switch (e.ActionBarCost.PowerType)
        {
            case PowerType.Mana:
                oldValue = MinMana;
                MinMana = e.ActionBarCost.Cost;
                break;
            case PowerType.Rage:
                oldValue = MinRage;
                MinRage = e.ActionBarCost.Cost;
                break;
            case PowerType.Energy:
                oldValue = MinEnergy;
                MinEnergy = e.ActionBarCost.Cost;
                break;
            case PowerType.RunicPower:
                oldValue = MinRunicPower;
                MinRunicPower = e.ActionBarCost.Cost;
                break;
            case PowerType.RuneBlood:
                oldValue = MinRuneBlood;
                MinRuneBlood = e.ActionBarCost.Cost;
                break;
            case PowerType.RuneFrost:
                oldValue = MinRuneFrost;
                MinRuneFrost = e.ActionBarCost.Cost;
                break;
            case PowerType.RuneUnholy:
                oldValue = MinRuneUnholy;
                MinRuneUnholy = e.ActionBarCost.Cost;
                break;
        }

        if (e.ActionBarCost.Cost != oldValue)
        {
            LogPowerCostChange(logger, Name, e.ActionBarCost.PowerType.ToStringF(), e.ActionBarCost.Cost, oldValue);
        }
    }

    private int GetMinCost()
    {
        return MinCost;
    }

    #region Logging

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "[{name}] Path: {path}")]
    static partial void LogPath(ILogger logger, string name, string path);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "[{name}] Required Form: {form}")]
    static partial void LogFormRequired(ILogger logger, string name, string form);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "[{name}] Actionbar Key:{key} -> Actionbar:{slot} -> Index:{slotIndex}")]
    static partial void LogInputActionbar(ILogger logger, string name, string key, int slot, int slotIndex);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "[{name}] Non Actionbar {key} -> {consoleKey}")]
    static partial void LogInputNonActionbar(ILogger logger, string name, string key, ConsoleKey consoleKey);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "[{name}] has no valid Key={key} or ConsoleKey={consoleKey}")]
    static partial void LogInputNoValidKey(ILogger logger, string name, string key, ConsoleKey consoleKey);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "[{name}] Update {type} cost to {newCost} from {oldCost}")]
    static partial void LogPowerCostChange(ILogger logger, string name, string type, int newCost, int oldCost);

    #endregion
}