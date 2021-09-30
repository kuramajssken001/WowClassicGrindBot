﻿using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class CombatGoal : GoapGoal
    {
        public override float CostOfPerformingAction { get => 4f; }

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly CastingHandler castingHandler;
        
        private readonly ClassConfiguration classConfiguration;

        private int lastKilledGuid;

        public CombatGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving,  ClassConfiguration classConfiguration, CastingHandler castingHandler)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            
            this.classConfiguration = classConfiguration;
            this.castingHandler = castingHandler;

            lastKilledGuid = playerReader.LastDeadGuid;

            AddPrecondition(GoapKey.incombat, true);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombatrange, true);

            AddEffect(GoapKey.producedcorpse, true);
            AddEffect(GoapKey.targetisalive, false);
            AddEffect(GoapKey.hastarget, false);

            classConfiguration.Combat.Sequence.Where(k => k != null).ToList().ForEach(key => Keys.Add(key));
        }

        protected async Task Fight()
        {
            if (playerReader.PlayerBitValues.HasPet && !playerReader.PetHasTarget)
            {
                await input.TapPetAttack("");
            }

            foreach (var item in Keys)
            {
                if (!playerReader.HasTarget)
                {
                    logger.LogInformation($"{GetType().Name}: Lost Target!");
                    await stopMoving.Stop();
                    return;
                }

                if (playerReader.IsAutoAttacking)
                {
                    await castingHandler.ReactToLastUIErrorMessage($"{GetType().Name}: Fight AutoAttacking");
                }

                if (await castingHandler.CastIfReady(item, item.DelayBeforeCast))
                {
                    break;
                }
            }
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.newtarget)
            {
                logger.LogInformation("?Reset cooldowns");

                ResetCooldowns();
            }
        }

        private void ResetCooldowns()
        {
            this.classConfiguration.Combat.Sequence
            .Where(i => i.ResetOnNewTarget)
            .ToList()
            .ForEach(item =>
            {
                logger.LogInformation($"Reset cooldown on {item.Name}");
                item.ResetCooldown();
                item.ResetCharges();
            });
        }

        protected bool HasPickedUpAnAdd
        {
            get
            {
                return this.playerReader.PlayerBitValues.PlayerInCombat &&
                    !this.playerReader.PlayerBitValues.TargetOfTargetIsPlayer
                    && this.playerReader.TargetHealthPercentage == 100;
            }
        }

        public override async Task OnEnter()
        {
            await base.OnEnter();

            lastKilledGuid = playerReader.LastDeadGuid;

            if (playerReader.PlayerBitValues.IsMounted)
            {
                await input.TapDismount();
            }

            // this one is important for melees
            // if not waiting a bit, the player will constantly moving forward
            await stopMoving.Stop();
            await wait.Update(1);

            logger.LogInformation($"{GetType().Name}: OnEnter");
            SendActionEvent(new ActionEventArgs(GoapKey.fighting, true));
        }

        public override async Task OnExit()
        {
            await base.OnExit();

            bool killCredit = DidIKilledAnyone();
            logger.LogInformation($"{GetType().Name}: OnExit -> Killed anyone? {killCredit}");

            if (killCredit)
            {
                await CreatureTargetMeOrMyPet();
            }
        }

        public override async Task PerformAction()
        {
            await Fight();

            if (DidIKilledAnyone() && !await CreatureTargetMeOrMyPet())
            {
                logger.LogInformation("Exit CombatGoal!!!");
            }

            await Task.Delay((int)(playerReader.AvgUpdateLatency / 2));
        }

        private bool DidIKilledAnyone()
        {
            if (lastKilledGuid != playerReader.LastDeadGuid
                && playerReader.Targets.Any(x => x.CreatureId == playerReader.LastDeadGuid)
                && playerReader.DamageDone.Any(x => x.CreatureId == playerReader.LastDeadGuid))
            {
                lastKilledGuid = playerReader.LastDeadGuid;
                playerReader.IncrementKillCount();

                logger.LogInformation($"----- Target is dead! Known kills: {playerReader.LastCombatKillCount}");
                SendActionEvent(new ActionEventArgs(GoapKey.producedcorpse, true));
                return true;
            }

            return false;
        }

        private async Task<bool> CreatureTargetMeOrMyPet()
        {
            await wait.Update(1);
            if (playerReader.PetHasTarget && playerReader.LastDeadGuid != playerReader.PetTargetGuid)
            {
                logger.LogWarning("---- My pet has a target!");
                ResetCooldowns();

                await input.TapTargetPet();
                await input.TapTargetOfTarget();
                await wait.Update(1);
                return playerReader.HasTarget;
            }

            await input.TapNearestTarget($"{GetType().Name}: Checking target in front of me");
            await wait.Update(1);
            if (playerReader.HasTarget)
            {
                if (playerReader.PlayerBitValues.TargetInCombat && playerReader.PlayerBitValues.TargetOfTargetIsPlayer)
                {
                    ResetCooldowns();

                    logger.LogWarning("---- Somebody is attacking me!");
                    await input.TapInteractKey("Found new target to attack");
                    await wait.Update(1);
                    return true;
                }

                await input.TapClearTarget();
                await wait.Update(1);
            }
            else
            {
                // threat must be behind me
                var anyDamageTakens = playerReader.DamageTaken.Where(x => (DateTime.Now - x.LastEvent).TotalSeconds < 10 && x.LastKnownHealthPercent > 0);
                if (anyDamageTakens.Any())
                {
                    logger.LogWarning($"---- Possible threats found behind {anyDamageTakens.Count()}. Waiting for my target to change!");
                    await wait.Interrupt(2000, () => playerReader.HasTarget);
                }
            }

            logger.LogWarning("---- No Threat has been found!");

            return false;
        }
    }
}