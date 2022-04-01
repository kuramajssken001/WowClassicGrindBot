﻿using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading;
using SharedLib.Extensions;

namespace Core
{
    public class PlayerDirection : IPlayerDirection, IDisposable
    {
        private readonly bool debug = false;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly CancellationTokenSource _cts;
        private readonly float RADIAN = MathF.PI * 2;

        private const int DefaultIgnoreDistance = 10;

        public DateTime LastSetDirection { get; private set; }

        public PlayerDirection(ILogger logger, ConfigurableInput input, PlayerReader playerReader)
        {
            this.logger = logger;
            this.input = input;
            this.playerReader = playerReader;
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cts.Dispose();
        }

        public void SetDirection(float desiredDirection, Vector3 point, string source)
        {
            SetDirection(desiredDirection, point, source, DefaultIgnoreDistance, _cts);
        }

        public void SetDirection(float desiredDirection, Vector3 point, string source, int ignoreDistance, CancellationTokenSource cts)
        {
            float distance = playerReader.PlayerLocation.DistanceXYTo(point);
            if (distance < ignoreDistance)
            {
                if(debug)
                    Log($"Too close, ignoring direction change. {distance} < {ignoreDistance}");

                return;
            }

            if(debug)
                Log($"SetDirection: {source}-- Current: {playerReader.Direction:0.000} -> Target: {desiredDirection:0.000} - Distance: {distance:0.000}");

            input.KeyPressSleep(GetDirectionKeyToPress(desiredDirection),
                TurnDuration(desiredDirection),
                cts);

            LastSetDirection = DateTime.UtcNow;
        }

        private float TurnAmount(float desiredDirection)
        {
            var result = (RADIAN + desiredDirection - playerReader.Direction) % RADIAN;
            if (result > MathF.PI) { result = RADIAN - result; }
            return result;
        }

        private int TurnDuration(float desiredDirection)
        {
            return (int)(TurnAmount(desiredDirection) * 1000 / MathF.PI);
        }

        private ConsoleKey GetDirectionKeyToPress(float desiredDirection)
        {
            return (RADIAN + desiredDirection - playerReader.Direction) % RADIAN < MathF.PI
                ? input.TurnLeftKey : input.TurnRightKey;
        }

        private void Log(string text)
        {
            logger.LogInformation($"[{nameof(PlayerDirection)}]: {text}");
        }
    }
}